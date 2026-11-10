import type {
  ScheduledTaskBoardDto,
  ScheduledTaskDto,
  ScheduledTaskRunDto,
} from '@core/api/schema';
import { describe, expect, it } from 'vitest';
import {
  ACTIVE_POLL,
  ATTENTION_GROUP,
  IDLE_POLL,
  boardSummary,
  laneGroups,
  laneMarks,
  laneNote,
  laneSummary,
  nextPoll,
  nightShades,
  plannedTimes,
  runDuration,
  runFailure,
  serviceNote,
  serviceState,
  stateLabel,
  taskPhase,
  taskRunState,
} from './scheduling-presentation';

const NOW = Date.parse('2026-11-08T06:00:00Z');
const MINUTE = 60_000;
const HOUR = 60 * MINUTE;
const at = (offset: number) => new Date(NOW + offset).toISOString();

function run(overrides: Partial<ScheduledTaskRunDto> = {}): ScheduledTaskRunDto {
  return {
    id: 'run-1',
    taskKey: 'mail.retention',
    triggerKind: 'scheduled',
    requestedByName: null,
    host: 'web-01',
    startedAt: at(-60_000),
    finishedAt: at(-55_000),
    status: 'succeeded',
    stage: null,
    completedUnits: null,
    totalUnits: null,
    summary: null,
    errorCode: null,
    errorMessage: null,
    issueCode: null,
    ...overrides,
  };
}

function task(overrides: Partial<ScheduledTaskDto> = {}): ScheduledTaskDto {
  return {
    key: 'mail.retention',
    name: '郵件保存期清理',
    category: '郵件',
    description: '刪除超過保存期的寄送紀錄。',
    schedule: '每 1 小時',
    scheduleKind: 'interval',
    intervalSeconds: 3600,
    timeoutSeconds: 1800,
    unavailableReason: null,
    isPaused: false,
    pausedByName: null,
    pausedAt: null,
    nextRunAt: at(10 * MINUTE),
    runRequestedAt: null,
    cancelRequested: false,
    host: null,
    consecutiveFailures: 0,
    currentRun: null,
    lastRun: null,
    timelineRuns: [],
    plannedRuns: [],
    ...overrides,
  };
}

const cron = (overrides: Partial<ScheduledTaskDto> = {}) =>
  task({ scheduleKind: 'cron', intervalSeconds: null, ...overrides });

describe('taskPhase', () => {
  it('puts what blocks a run first, then activity, then the schedule', () => {
    expect(taskPhase(task({ unavailableReason: '未啟用', currentRun: run() }), NOW)).toBe(
      'unavailable',
    );
    expect(taskPhase(task({ currentRun: run({ status: 'running' }), isPaused: true }), NOW)).toBe(
      'running',
    );
    expect(taskPhase(task({ runRequestedAt: at(-1000), isPaused: true }), NOW)).toBe('requested');
    expect(taskPhase(task({ isPaused: true }), NOW)).toBe('paused');
    expect(taskPhase(task({ nextRunAt: at(-1) }), NOW)).toBe('due');
    expect(taskPhase(task(), NOW)).toBe('waiting');
    expect(taskPhase(task({ nextRunAt: null }), NOW)).toBe('done');
  });

  it('shows the last outcome when nothing is happening', () => {
    const failed = task({ lastRun: run({ status: 'failed' }) });
    expect(taskRunState(failed, 'waiting')).toBe('failed');
    expect(stateLabel(failed, 'waiting')).toBe('上一輪失敗');
    expect(taskRunState(task(), 'waiting')).toBe('idle');
    expect(stateLabel(task(), 'waiting')).toBe('還沒有執行過');
    expect(taskRunState(task(), 'due')).toBe('queued');
  });
});

describe('runs', () => {
  it('formats a finished duration and leaves a running one to the ticking clock', () => {
    expect(runDuration(run())).toBe('5.0 秒');
    expect(runDuration(run({ finishedAt: null }))).toBe('');
  });

  it('prefers the reviewed message the server stored', () => {
    expect(runFailure(run({ errorMessage: '外部服務暫時無法使用。查證代碼：X' }))).toBe(
      '外部服務暫時無法使用。查證代碼：X',
    );
    expect(runFailure(run({ errorMessage: null, issueCode: null }))).toContain('本機問題代碼');
  });
});

describe('plannedTimes', () => {
  it('walks an interval forward from the next run', () => {
    expect(plannedTimes(task(), NOW, NOW + 3 * HOUR)).toEqual([
      NOW + 10 * MINUTE,
      NOW + 70 * MINUTE,
      NOW + 130 * MINUTE,
    ]);
  });

  it('never plans behind the now line for a task that came due', () => {
    expect(plannedTimes(task({ nextRunAt: at(-MINUTE) }), NOW, NOW + 90 * MINUTE)).toEqual([
      NOW + HOUR,
    ]);
  });

  it('thins very frequent intervals with a minimum step', () => {
    const everyMinute = task({ intervalSeconds: 60, nextRunAt: at(MINUTE) });
    expect(plannedTimes(everyMinute, NOW, NOW + HOUR, 10 * MINUTE)).toHaveLength(6);
  });

  it('uses the server-resolved times for cron and falls back to the next run', () => {
    expect(
      plannedTimes(cron({ plannedRuns: [at(HOUR), at(2 * HOUR)] }), NOW, NOW + 3 * HOUR),
    ).toEqual([NOW + HOUR, NOW + 2 * HOUR]);
    expect(plannedTimes(cron({ nextRunAt: at(HOUR) }), NOW, NOW + 2 * HOUR)).toEqual([NOW + HOUR]);
    expect(plannedTimes(cron({ nextRunAt: at(3 * HOUR) }), NOW, NOW + 2 * HOUR)).toEqual([]);
  });

  it('plans nothing for tasks that will not run', () => {
    expect(plannedTimes(task({ isPaused: true }), NOW, NOW + HOUR)).toEqual([]);
    expect(plannedTimes(task({ unavailableReason: '未啟用' }), NOW, NOW + HOUR)).toEqual([]);
  });
});

describe('lanes', () => {
  const timeline = (status: string, offset = -HOUR, finished: number | null = offset + 5000) => ({
    startedAt: at(offset),
    finishedAt: finished === null ? null : at(finished),
    status,
    summary: status === 'succeeded' ? '清除 120 筆' : null,
  });

  it('puts results before plans and describes each run', () => {
    const marks = laneMarks(task({ timelineRuns: [timeline('succeeded')] }), [NOW + HOUR]);
    expect(marks.map((mark) => mark.kind)).toEqual(['success', 'planned']);
    expect(marks[0].title).toContain('成功');
    expect(marks[0].title).toContain('清除 120 筆');
    expect(marks[1].label).toBeUndefined();
  });

  it('only shows a run as active while the board has it running', () => {
    const running = timeline('running', -MINUTE, null);
    expect(laneMarks(task({ timelineRuns: [running] }), [])[0].kind).toBe('warning');
    expect(
      laneMarks(task({ timelineRuns: [running], currentRun: run({ status: 'running' }) }), [])[0]
        .kind,
    ).toBe('active');
  });

  it('writes out the time of a few fixed runs', () => {
    expect(laneMarks(cron(), [NOW + HOUR])[0].label).toBe('15:00');
    const many = [1, 2, 3, 4].map((hours) => NOW + hours * HOUR);
    expect(laneMarks(cron(), many)[0].label).toBeUndefined();
  });

  it('summarises a lane for screen readers', () => {
    const lane = task({ timelineRuns: [timeline('succeeded'), timeline('failed', -30 * MINUTE)] });
    expect(laneSummary(lane, [NOW + HOUR])).toBe('區間內 2 輪：成功 1、失敗 1；下次 15:00');
    expect(laneSummary(task(), [])).toBe('區間內沒有執行紀錄；區間內沒有預定');
  });

  it('explains why a lane has no plan', () => {
    expect(laneNote(task({ isPaused: true }))).toContain('暫停');
    expect(laneNote(task({ unavailableReason: '未啟用' }))).toBe('不會執行');
    expect(laneNote(task({ scheduleKind: 'startup', intervalSeconds: null }))).toContain('啟動');
    expect(laneNote(task())).toBe('');
  });

  it('groups by rhythm, failing tasks first, in an order that does not change over time', () => {
    const groups = laneGroups([
      task({ key: 'hourly', intervalSeconds: 3600 }),
      cron({ key: 'fixed', plannedRuns: [at(20 * HOUR)] }),
      task({ key: 'minutes', intervalSeconds: 300 }),
      task({ key: 'failing', intervalSeconds: 300, consecutiveFailures: 2 }),
      task({ key: 'startup', scheduleKind: 'startup', intervalSeconds: null }),
    ]);
    expect(groups.map((group) => group.label)).toEqual([
      ATTENTION_GROUP,
      '每幾分鐘',
      '每幾小時',
      '固定時刻',
      '啟動時',
    ]);
    expect(groups[0].tasks.map((item) => item.key)).toEqual(['failing']);
  });

  it('orders fixed times by clock time and keeps a paused task in place', () => {
    const groups = laneGroups([
      cron({ key: 'late', name: 'A', plannedRuns: [at(9 * HOUR)] }),
      cron({ key: 'early', name: 'B', plannedRuns: [at(2 * HOUR)], isPaused: true }),
    ]);
    expect(groups[0].tasks.map((item) => item.key)).toEqual(['early', 'late']);
  });
});

describe('nightShades', () => {
  it('shades midnight to six in the workspace time zone and clips to the range', () => {
    // 11/08 14:00（台北）起算：前一夜不在區間內，只有 11/09 的 00:00–06:00。
    expect(nightShades(NOW, NOW + 24 * HOUR)).toEqual([
      { from: NOW + 10 * HOUR, to: NOW + 16 * HOUR },
    ]);
    // 區間從 03:00（台北）開始：第一段只剩 03:00–06:00。
    const three = Date.parse('2026-11-07T19:00:00Z');
    expect(nightShades(three, three + 24 * HOUR)[0]).toEqual({ from: three, to: three + 3 * HOUR });
  });
});

describe('boardSummary', () => {
  const board = (tasks: ScheduledTaskDto[]): ScheduledTaskBoardDto => ({
    serverTime: at(0),
    executesHere: true,
    timelineFrom: at(-4 * HOUR),
    timelineTo: at(20 * HOUR),
    tasks,
  });

  it('counts the states and the runs of the coming hour', () => {
    const summary = boardSummary(
      board([
        task({ name: '甲', intervalSeconds: 600, nextRunAt: at(5 * MINUTE) }),
        task({ name: '乙', nextRunAt: at(2 * MINUTE), isPaused: true }),
        task({ name: '丙', nextRunAt: at(30 * MINUTE), consecutiveFailures: 1 }),
        task({ name: '丁', nextRunAt: at(-MINUTE), currentRun: run({ status: 'running' }) }),
      ]),
    );
    expect(summary.next).toEqual({ name: '甲', at: NOW + 5 * MINUTE });
    // 甲 6 輪、丙 1 輪、丁下一輪剛好在一小時整；暫停的乙不算。
    expect(summary.upcoming).toBe(8);
    expect(summary).toMatchObject({ running: 1, failing: 1, paused: 1 });
  });

  it('has no next run when nothing is scheduled', () => {
    expect(boardSummary(board([task({ isPaused: true })])).next).toBeNull();
  });
});

describe('nextPoll', () => {
  it('polls quickly while something is happening or just came due', () => {
    expect(nextPoll([task({ currentRun: run({ status: 'running' }) })], NOW)).toBe(ACTIVE_POLL);
    expect(nextPoll([task({ nextRunAt: at(-1000) })], NOW)).toBe(ACTIVE_POLL);
  });

  it('wakes just after the next countdown ends, and stops hurrying for tasks nobody picks up', () => {
    expect(nextPoll([task({ nextRunAt: at(4000) })], NOW)).toBe(5500);
    expect(nextPoll([task({ nextRunAt: at(-60_000) })], NOW)).toBe(IDLE_POLL);
    expect(nextPoll([task({ nextRunAt: at(4000), isPaused: true })], NOW)).toBe(IDLE_POLL);
    expect(nextPoll(undefined, NOW)).toBe(IDLE_POLL);
  });
});

describe('resident services', () => {
  it('names each state and keeps an unknown one readable', () => {
    expect(serviceState('running')).toEqual({ label: '執行中', kind: 'running' });
    expect(serviceState('inactive').kind).toBe('idle');
    expect(serviceState('stopped').kind).toBe('interrupted');
    expect(serviceState('failed').kind).toBe('failed');
    expect(serviceState('draining')).toEqual({ label: 'draining', kind: 'idle' });
  });

  it('explains only the states that need it', () => {
    expect(serviceNote({ state: 'running', reason: null })).toBe('');
    expect(serviceNote({ state: 'inactive', reason: '這個站台不執行排程。' })).toBe(
      '這個站台不執行排程。',
    );
    expect(serviceNote({ state: 'stopped', reason: null })).toContain('系統日誌');
    expect(serviceNote({ state: 'failed', reason: 'SqlException' })).toContain('（SqlException）');
    expect(serviceNote({ state: 'failed', reason: null })).not.toContain('（');
  });
});
