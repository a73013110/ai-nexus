import type { ScheduledTaskDto, ScheduledTaskRunDto } from '@core/api/schema';
import { recordedProblem, systemProblem, validIssueCode } from '@core/errors/safe-errors';
import { formatDate, formatDuration } from '@shared/browser/format';
import type { BadgeTone } from '@shared/ui/count-badge';
import type { RunStateKind } from '@shared/ui/run-state';
import type { RunStripItem } from '@shared/ui/run-strip';
import type { TimerRingMode } from '@shared/ui/timer-ring';

export const RUN_STATUSES: Record<string, { label: string; tone: BadgeTone }> = {
  running: { label: '執行中', tone: 'info' },
  succeeded: { label: '成功', tone: 'success' },
  idle: { label: '沒有事做', tone: 'neutral' },
  failed: { label: '失敗', tone: 'danger' },
  cancelled: { label: '已停止', tone: 'neutral' },
  interrupted: { label: '中斷', tone: 'warning' },
};
export const RUN_STATUS_OPTIONS = Object.entries(RUN_STATUSES).map(([value, status]) => ({
  value,
  label: status.label,
}));
export const RUN_TRIGGERS: Record<string, string> = {
  scheduled: '排程',
  manual: '手動',
  startup: '啟動時',
};

export function runStatus(status: string) {
  return RUN_STATUSES[status] ?? { label: status, tone: 'neutral' as BadgeTone };
}

/**
 * 任務此刻的處境，決定圓環、圖示、說明與能按的按鈕。依序判斷：不能執行 → 執行中 → 已要求執行 → 暫停 → 到期 → 等待排程；
 * 啟動時執行的任務跑過後沒有下次時間（`done`）。
 */
export type TaskPhase =
  'unavailable' | 'running' | 'requested' | 'paused' | 'due' | 'waiting' | 'done';

export function taskPhase(task: ScheduledTaskDto, now: number): TaskPhase {
  if (task.unavailableReason) return 'unavailable';
  if (task.currentRun) return 'running';
  if (task.runRequestedAt) return 'requested';
  if (task.isPaused) return 'paused';
  if (!task.nextRunAt) return 'done';
  return Date.parse(task.nextRunAt) <= now ? 'due' : 'waiting';
}

const RING_MODES: Record<TaskPhase, TimerRingMode> = {
  unavailable: 'off',
  running: 'running',
  requested: 'due',
  paused: 'paused',
  due: 'due',
  waiting: 'countdown',
  done: 'off',
};

/** 圓環的模式。 */
export function ringMode(phase: TaskPhase) {
  return RING_MODES[phase];
}

/** 卡片圖示的狀態：處境優先，否則是上一輪的結果；還沒跑過時是「沒有事做」的一橫。 */
export function taskRunState(task: ScheduledTaskDto, phase: TaskPhase): RunStateKind {
  switch (phase) {
    case 'running':
      return 'running';
    case 'requested':
    case 'due':
      return 'queued';
    case 'paused':
      return 'paused';
    default:
      return (task.lastRun?.status as RunStateKind | undefined) ?? 'idle';
  }
}

/**
 * 倒數的時間窗起點：上一輪結束；固定間隔的任務最多往前推一個間隔（暫停很久後恢復時圓環不會只剩一小段）。
 * 每日、每週這種沒有固定間隔的排程，沒跑過時回傳 null，圓環畫成滿的。
 */
export function countdownFrom(task: ScheduledTaskDto) {
  if (!task.nextRunAt) return null;
  const next = Date.parse(task.nextRunAt);
  const finished = task.lastRun?.finishedAt ? Date.parse(task.lastRun.finishedAt) : null;
  const floor = task.intervalSeconds ? next - task.intervalSeconds * 1000 : null;
  const from =
    finished !== null && floor !== null ? Math.max(finished, floor) : (finished ?? floor);
  return from === null || from >= next ? null : new Date(from).toISOString();
}

/** 執行中的進度（0–1）；不知道總量時為 null，圓環繞行。 */
export function runProgress(run: ScheduledTaskRunDto | null) {
  if (!run?.totalUnits || run.completedUnits === null) return null;
  return Math.min(1, run.completedUnits / run.totalUnits);
}

/** 一輪的耗時；還在跑的回傳空字串（畫面改顯示逐秒跳動的經過時間）。 */
export function runDuration(run: Pick<ScheduledTaskRunDto, 'startedAt' | 'finishedAt'>) {
  if (!run.finishedAt) return '';
  return formatDuration(Math.max(0, Date.parse(run.finishedAt) - Date.parse(run.startedAt)));
}

/** 失敗的說明：伺服器存的是審閱過的提示加查證代碼；舊紀錄沒有時退回通用提示。 */
export function runFailure(
  run: Pick<ScheduledTaskRunDto, 'errorMessage' | 'errorCode' | 'issueCode'>,
) {
  if (run.errorMessage) return run.errorMessage;
  return validIssueCode(run.issueCode)
    ? recordedProblem(run.errorCode, run.issueCode)
    : systemProblem(run.issueCode);
}

/** 紀錄條：舊到新，滑過時看時間、結果、耗時與摘要。 */
export function stripItems(runs: readonly ScheduledTaskRunDto[]): RunStripItem[] {
  return [...runs].reverse().map((run) => ({
    id: run.id,
    status: run.status,
    startedAt: run.startedAt,
    finishedAt: run.finishedAt,
    title: [formatDate(run.startedAt), runStatus(run.status).label, runDuration(run), run.summary]
      .filter(Boolean)
      .join('・'),
  }));
}

export const ATTENTION_GROUP = '需要注意';

/**
 * 依分類分組，保留伺服器的順序（分類、名稱）。連續失敗中的任務抽到最前面的「需要注意」：
 * 失敗通知只開到這一頁（不指向單一任務），一打開就要看得到；暫停與不能執行是有意的狀態，留在原分類。
 */
export function groupByCategory(tasks: readonly ScheduledTaskDto[]) {
  const groups = new Map<string, ScheduledTaskDto[]>();
  const failing = tasks.filter((task) => task.consecutiveFailures > 0);
  if (failing.length) groups.set(ATTENTION_GROUP, failing);
  for (const task of tasks) {
    if (task.consecutiveFailures > 0) continue;
    const group = groups.get(task.category);
    if (group) group.push(task);
    else groups.set(task.category, [task]);
  }
  return [...groups].map(([category, items]) => ({ category, tasks: items }));
}

/** 看板上方的計數。 */
export function boardCounts(tasks: readonly ScheduledTaskDto[]) {
  return {
    total: tasks.length,
    running: tasks.filter((task) => task.currentRun).length,
    failing: tasks.filter((task) => task.consecutiveFailures > 0).length,
    paused: tasks.filter((task) => task.isPaused).length,
  };
}

/** 有任務在跑、已要求執行或停止中時的輪詢間隔；平常的間隔。 */
export const ACTIVE_POLL = 2000;
export const IDLE_POLL = 15000;
/** 到期多久還沒開始就不再快速輪詢：開發機不執行排程（`Scheduling:Execute`），到期的任務會一直停在到期。 */
const DUE_GRACE = 30000;

/**
 * 下次讀看板的間隔：有動靜時快讀；否則在最近一個倒數歸零後一點點讀，讓「到期 → 開始執行」不必等滿平常的間隔。
 */
export function nextPoll(tasks: readonly ScheduledTaskDto[] | undefined, now: number) {
  if (!tasks) return IDLE_POLL;
  let delay = IDLE_POLL;
  for (const task of tasks) {
    if (task.currentRun || task.runRequestedAt || task.cancelRequested) return ACTIVE_POLL;
    if (!task.nextRunAt || task.isPaused || task.unavailableReason) continue;
    const until = Date.parse(task.nextRunAt) - now;
    if (until <= 0 && until > -DUE_GRACE) return ACTIVE_POLL;
    if (until > 0) delay = Math.min(delay, until + 1500);
  }
  return delay;
}
