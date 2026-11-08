import type { ScheduledTaskDto, ScheduledTaskRunDto } from '@core/api/schema';
import { describe, expect, it } from 'vitest';
import {
  ACTIVE_POLL,
  ATTENTION_GROUP,
  IDLE_POLL,
  countdownFrom,
  groupByCategory,
  nextPoll,
  runDuration,
  runFailure,
  runProgress,
  stripItems,
  taskPhase,
  taskRunState,
} from './scheduling-presentation';

const NOW = Date.parse('2026-11-08T06:00:00Z');
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
    nextRunAt: at(10 * 60_000),
    runRequestedAt: null,
    cancelRequested: false,
    host: null,
    consecutiveFailures: 0,
    currentRun: null,
    lastRun: null,
    recentRuns: [],
    ...overrides,
  };
}

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
    expect(taskRunState(task({ lastRun: run({ status: 'failed' }) }), 'waiting')).toBe('failed');
    expect(taskRunState(task(), 'waiting')).toBe('idle');
    expect(taskRunState(task(), 'due')).toBe('queued');
  });
});

describe('countdownFrom', () => {
  it('starts at the last finish, but never more than one interval back', () => {
    expect(countdownFrom(task({ lastRun: run({ finishedAt: at(-5 * 60_000) }) }))).toBe(
      at(-5 * 60_000),
    );
    // 暫停兩小時後恢復：只往前推一個間隔。
    expect(countdownFrom(task({ lastRun: run({ finishedAt: at(-2 * 3_600_000) }) }))).toBe(
      at(10 * 60_000 - 3_600_000),
    );
    expect(countdownFrom(task({ intervalSeconds: null, scheduleKind: 'cron' }))).toBeNull();
    expect(countdownFrom(task({ nextRunAt: null }))).toBeNull();
  });
});

describe('runs', () => {
  it('reports progress only when the total is known', () => {
    expect(runProgress(run({ completedUnits: 3, totalUnits: 12 }))).toBe(0.25);
    expect(runProgress(run({ completedUnits: 3 }))).toBeNull();
    expect(runProgress(null)).toBeNull();
  });

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

  it('turns newest-first runs into an oldest-first strip with a readable title', () => {
    const items = stripItems([
      run({ id: 'new', status: 'failed' }),
      run({ id: 'old', summary: '清除 120 筆' }),
    ]);
    expect(items.map((item) => item.id)).toEqual(['old', 'new']);
    expect(items[0].title).toContain('成功');
    expect(items[0].title).toContain('清除 120 筆');
  });
});

describe('groupByCategory', () => {
  it('pulls failing tasks to the front and keeps the server order otherwise', () => {
    const groups = groupByCategory([
      task({ key: 'a', category: '郵件' }),
      task({ key: 'b', category: '檔案' }),
      task({ key: 'c', category: '郵件', consecutiveFailures: 2 }),
    ]);
    expect(groups.map((group) => group.category)).toEqual([ATTENTION_GROUP, '郵件', '檔案']);
    expect(groups[0].tasks.map((item) => item.key)).toEqual(['c']);
    expect(groups[1].tasks.map((item) => item.key)).toEqual(['a']);
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
