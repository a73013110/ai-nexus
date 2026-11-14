import type {
  ResidentServiceDto,
  ScheduledTaskBoardDto,
  ScheduledTaskDto,
  ScheduledTaskRunDto,
  ScheduledTaskTimelineRunDto,
} from '@core/api/schema';
import { recordedProblem, systemProblem, validIssueCode } from '@core/errors/safe-errors';
import {
  formatDate,
  formatDateTimeInput,
  formatDuration,
  formatTimeOfDay,
  parseDateTimeInput,
} from '@shared/browser/format';
import type { BadgeTone } from '@shared/ui/count-badge';
import { runStateTone, type RunStateKind } from '@shared/ui/run-state';
import type { TimeMark, TimeMarkKind } from '@shared/ui/time-lane';
import type { TimeShade } from '@shared/ui/time-lanes';

const HOUR = 3_600_000;
const DAY = 24 * HOUR;

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
 * 任務此刻的處境，決定圖示、說明與能按的按鈕。依序判斷：不能執行 → 執行中 → 已要求執行 → 暫停 → 到期 → 等待排程；
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

/** 列首圖示的狀態：處境優先，否則是上一輪的結果；還沒跑過時是「沒有事做」的一橫。 */
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

/** 列首圖示的文字（提示與螢幕閱讀器）。 */
export function stateLabel(task: ScheduledTaskDto, phase: TaskPhase) {
  switch (phase) {
    case 'running':
      return '執行中';
    case 'requested':
    case 'due':
      return '等待開始';
    case 'paused':
      return '已暫停';
    default:
      return task.lastRun ? `上一輪${runStatus(task.lastRun.status).label}` : '還沒有執行過';
  }
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

/**
 * 區間內還會執行的時間（舊到新）。固定間隔由下次時間往後推，cron 用伺服器解析好的結果（前端不解析 cron）；
 * 暫停與不能執行的任務不會跑，回傳空的。`minStep` 限制最小間距：每分鐘一輪的任務一天上千點，畫出來只是一條線。
 */
export function plannedTimes(task: ScheduledTaskDto, now: number, until: number, minStep = 0) {
  if (task.isPaused || task.unavailableReason) return [];
  const next = task.nextRunAt ? Date.parse(task.nextRunAt) : null;
  if (task.intervalSeconds) {
    const step = Math.max(task.intervalSeconds * 1000, minStep);
    const times: number[] = [];
    // 到期還沒開始的任務，下一輪最早也要從現在算起，不畫在現在線左邊。
    for (let at = next !== null && next > now ? next : now + step; at <= until; at += step)
      times.push(at);
    return times;
  }
  if (task.plannedRuns.length) return task.plannedRuns.map((at) => Date.parse(at));
  return next !== null && next > now && next <= until ? [next] : [];
}

function runMark(task: ScheduledTaskDto, run: ScheduledTaskTimelineRunDto): TimeMark {
  const kinds: Record<BadgeTone, TimeMarkKind> = {
    info: 'active',
    success: 'success',
    neutral: 'neutral',
    warning: 'warning',
    danger: 'danger',
  };
  // 紀錄還停在執行中、看板卻沒有進行中的一輪：程序中途結束留下的，不畫成正在跑。
  const orphaned = run.status === 'running' && !task.currentRun;
  return {
    at: Date.parse(run.startedAt),
    kind: orphaned ? 'warning' : kinds[runStateTone(run.status)],
    title: [formatDate(run.startedAt), runStatus(run.status).label, runDuration(run), run.summary]
      .filter(Boolean)
      .join('・'),
  };
}

/** 一條泳道的記號（舊到新）。少數幾個固定時刻直接寫出時間，比間隔排程的短線好讀。 */
export function laneMarks(task: ScheduledTaskDto, planned: readonly number[]): TimeMark[] {
  const labelled = !task.intervalSeconds && planned.length <= 3;
  return [
    ...task.timelineRuns.map((run) => runMark(task, run)),
    ...planned.map((at): TimeMark => ({
      at,
      kind: 'planned',
      label: labelled ? formatTimeOfDay(at) : undefined,
    })),
  ];
}

/** 軌道給螢幕閱讀器的摘要。 */
export function laneSummary(task: ScheduledTaskDto, planned: readonly number[]) {
  const counts = new Map<string, number>();
  for (const run of task.timelineRuns) {
    const label = runStatus(run.status).label;
    counts.set(label, (counts.get(label) ?? 0) + 1);
  }
  const past = task.timelineRuns.length
    ? `區間內 ${task.timelineRuns.length} 輪：${[...counts].map(([label, count]) => `${label} ${count}`).join('、')}`
    : '區間內沒有執行紀錄';
  return `${past}。${planned.length ? `下次 ${formatTimeOfDay(planned[0])}` : '區間內沒有預定'}`;
}

/** 沒有預定的原因，寫在現在線右邊。 */
export function laneNote(task: ScheduledTaskDto) {
  if (task.unavailableReason) return '不會執行';
  if (task.isPaused) return '暫停中，不依排程執行';
  return task.scheduleKind === 'startup' ? '只在站台啟動時執行' : '';
}

export const ATTENTION_GROUP = '需要注意';
const GROUPS = [ATTENTION_GROUP, '每幾分鐘', '每幾小時', '固定時刻', '啟動時'];

function groupOf(task: ScheduledTaskDto) {
  if (task.consecutiveFailures > 0) return ATTENTION_GROUP;
  if (task.scheduleKind === 'startup') return '啟動時';
  if (!task.intervalSeconds) return '固定時刻';
  return task.intervalSeconds < 3600 ? '每幾分鐘' : '每幾小時';
}

/** 固定時刻的排序鍵；暫停也照用伺服器算的時間，暫停或恢復時列不跳位。 */
function clockKey(task: ScheduledTaskDto) {
  if (task.intervalSeconds) return '';
  const first = task.plannedRuns[0] ?? task.nextRunAt;
  return first ? formatDateTimeInput(new Date(first)).slice(11) : '';
}

/**
 * 依節奏分組：相近的節奏排在一起，時間軸上才看得出規律與衝突。連續失敗的抽到最前面的「需要注意」：
 * 失敗通知只開到這一頁（不指向單一任務），一打開就要看得到；暫停與不能執行是有意的狀態，留在原組。
 * 排序只用不隨時間變的值，輪詢時列不會跳來跳去。
 */
export function laneGroups(tasks: readonly ScheduledTaskDto[]) {
  const sorted = tasks
    .map((task) => ({ task, clock: clockKey(task) }))
    .sort(
      (a, b) =>
        (a.task.intervalSeconds ?? 0) - (b.task.intervalSeconds ?? 0) ||
        a.clock.localeCompare(b.clock) ||
        a.task.name.localeCompare(b.task.name, 'zh-TW'),
    )
    .map(({ task }) => task);
  return GROUPS.map((label) => ({
    label,
    tasks: sorted.filter((task) => groupOf(task) === label),
  })).filter((group) => group.tasks.length);
}

/** 夜間（工作區時區 00:00–06:00）的底色，裁切到區間內：排程多半集中在這段，一眼對得上。 */
export function nightShades(from: number, to: number): TimeShade[] {
  const shades: TimeShade[] = [];
  const midnight = parseDateTimeInput(
    `${formatDateTimeInput(new Date(from)).slice(0, 10)}T00:00:00`,
  ).getTime();
  for (let day = midnight; day < to; day += DAY) {
    const start = Math.max(day, from);
    const end = Math.min(day + 6 * HOUR, to);
    if (start < end) shades.push({ from: start, to: end });
  }
  return shades;
}

/** 頁首的摘要；一律以看板回應的時間計算，只在重讀時變動，不跟著時鐘每秒重算。 */
export function boardSummary(board: ScheduledTaskBoardDto) {
  const now = Date.parse(board.serverTime);
  let upcoming = 0;
  let next: { name: string; at: number } | null = null;
  for (const task of board.tasks) {
    upcoming += plannedTimes(task, now, now + HOUR).length;
    if (task.isPaused || task.unavailableReason || !task.nextRunAt) continue;
    const at = Date.parse(task.nextRunAt);
    if (at > now && (!next || at < next.at)) next = { name: task.name, at };
  }
  return {
    upcoming,
    next,
    running: board.tasks.filter((task) => task.currentRun).length,
    failing: board.tasks.filter((task) => task.consecutiveFailures > 0).length,
    paused: board.tasks.filter((task) => task.isPaused).length,
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

/** 常駐服務在回應請求的那個站台的狀態；`kind` 沿用執行狀態的圖示與色調。 */
const SERVICE_STATES: Record<string, { label: string; kind: RunStateKind }> = {
  running: { label: '執行中', kind: 'running' },
  inactive: { label: '未啟用', kind: 'idle' },
  stopped: { label: '已停止', kind: 'interrupted' },
  failed: { label: '已中止', kind: 'failed' },
};

export function serviceState(state: string) {
  return SERVICE_STATES[state] ?? { label: state, kind: 'idle' as RunStateKind };
}

/**
 * 狀態下面那一句：未啟用寫設定上的原因；該跑卻停了的兩種狀況寫去哪裡查，
 * 因為這一頁只知道它停了，原因在啟動當時的日誌裡。照常執行時不必說明。
 */
export function serviceNote(service: Pick<ResidentServiceDto, 'state' | 'reason'>) {
  switch (service.state) {
    case 'inactive':
      return service.reason ?? '';
    case 'stopped':
      return '這個站台沒有在執行，請到系統日誌查啟動時的紀錄。';
    case 'failed':
      return `因為發生錯誤而中止${service.reason ? `（${service.reason}）` : ''}，重新啟動站台才會恢復。`;
    default:
      return '';
  }
}
