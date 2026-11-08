import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import type { QueuedJobDto, ScheduledTaskDto } from '@core/api/schema';
import { apiResource } from '@core/api/api-resource';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { FeaturePage } from '@core/layout/feature-page';
import {
  JOB_KIND_OPTIONS,
  JOB_STATUS_OPTIONS,
  jobFailure,
  jobKindName,
  jobStatus,
} from '@features/work/jobs/job-kind';
import { action } from '@shared/browser/action';
import { formatDate, relativeTime } from '@shared/browser/format';
import { ServerClock } from '@shared/browser/server-clock';
import { ViewScope } from '@shared/browser/view-scope';
import { DataTable, DataTableColumn, TableState } from '@shared/ui/data-table';
import { DetailDrawer } from '@shared/ui/detail-drawer';
import { Icon } from '@shared/ui/icon';
import { Notice } from '@shared/ui/notice';
import { PendingButton } from '@shared/ui/pending-button';
import { RunState } from '@shared/ui/run-state';
import { RunStrip } from '@shared/ui/run-strip';
import { StatusBadge } from '@shared/ui/status-badge';
import { TableHeader } from '@shared/ui/table-header';
import { endpointTable } from '@shared/ui/table-model';
import { TickingTime } from '@shared/ui/ticking-time';
import { TimerRing } from '@shared/ui/timer-ring';
import { ViewSwitch } from '@shared/ui/view-switch';
import { SchedulingApi } from './scheduling-api';
import {
  ACTIVE_POLL,
  ATTENTION_GROUP,
  IDLE_POLL,
  RUN_STATUS_OPTIONS,
  RUN_TRIGGERS,
  boardCounts,
  countdownFrom,
  groupByCategory,
  nextPoll,
  ringMode,
  runDuration,
  runFailure,
  runProgress,
  runStatus,
  stripItems,
  taskPhase,
  taskRunState,
} from './scheduling-presentation';

const VIEWS = [
  { value: 'tasks', label: '排程任務' },
  { value: 'queue', label: '佇列工作' },
] as const;
type View = (typeof VIEWS)[number]['value'];

/**
 * 背景作業（`scheduling`）：所有排程任務一張卡一個，倒數、執行中、上一輪結果與最近幾輪一眼看完；另一個檢視是使用者送出的佇列工作。
 * 看板依處境調整輪詢（{@link nextPoll}），並以回應裡的伺服器時間校正共用時鐘，倒數不受使用者電腦時鐘誤差影響。
 * 有 `scheduling.manage` 時可立即執行、停止、暫停與恢復任務，以及停止佇列工作；都是記下要求就回應，結果靠輪詢更新。
 */
@Component({
  selector: 'nx-scheduling-page',
  imports: [
    DataTable,
    DataTableColumn,
    DetailDrawer,
    FeaturePage,
    Icon,
    Notice,
    PendingButton,
    RunState,
    RunStrip,
    StatusBadge,
    TableHeader,
    TableState,
    TickingTime,
    TimerRing,
    ViewSwitch,
  ],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './scheduling-page.scss',
  templateUrl: './scheduling-page.html',
})
export class SchedulingPage {
  readonly session = inject(WorkspaceSession);
  private readonly api = inject(SchedulingApi);
  private readonly clock = inject(ServerClock);
  readonly views = VIEWS;
  readonly view = signal<View>('tasks');
  readonly manage = computed(() => this.session.has('scheduling.manage'));
  readonly attentionGroup = ATTENTION_GROUP;
  readonly formatDate = formatDate;
  readonly runStatus = runStatus;
  readonly runDuration = runDuration;
  readonly runFailure = runFailure;
  readonly triggers = RUN_TRIGGERS;

  private readonly boardRead = apiResource({
    feature: 'scheduling',
    loader: async (_params, signal) => {
      const board = await this.api.board(signal);
      this.clock.sync(board.serverTime);
      return board;
    },
    poll: (board) => nextPoll(board?.tasks, this.clock.now()),
  });
  readonly board = this.boardRead.value;
  readonly boardError = this.boardRead.error;
  readonly boardLoading = this.boardRead.loading;
  readonly counts = computed(() => boardCounts(this.board()?.tasks ?? []));

  /** 每張卡不隨時間變的顯示資料；只在看板重讀時重算。 */
  readonly groups = computed(() =>
    groupByCategory(this.board()?.tasks ?? []).map((group) => ({
      category: group.category,
      tasks: group.tasks.map((task) => ({
        task,
        from: countdownFrom(task),
        progress: runProgress(task.currentRun),
        strip: stripItems(task.recentRuns),
        last: task.lastRun ? runStatus(task.lastRun.status) : null,
      })),
    })),
  );
  /**
   * 每個任務此刻的處境；跟著時鐘每秒重算，但只有跨過下次執行時間時結果才會變，
   * 字串相同的繫結不會動到畫面，所以卡片的其他部分（紀錄條、說明）不必跟著每秒重算。
   */
  readonly phases = computed(() => {
    const now = this.clock.now();
    return new Map((this.board()?.tasks ?? []).map((task) => [task.key, taskPhase(task, now)]));
  });
  readonly ringMode = ringMode;
  readonly taskRunState = taskRunState;

  readonly running = action({ concurrent: true });

  /** 詳情抽屜：任務設定、上一輪的失敗與完整的執行紀錄。 */
  readonly selectedKey = signal<string | null>(null);
  readonly selected = computed(
    () => this.board()?.tasks.find((task) => task.key === this.selectedKey()) ?? null,
  );
  private readonly drawer = viewChild(DetailDrawer);
  readonly runs = endpointTable({
    feature: 'scheduling',
    params: () => this.selectedKey() ?? undefined,
    load: (key, table, signal) => this.api.runs(key, table, signal),
    columns: [
      { id: 'startedAt', label: '開始', hideable: false, sort: 'desc', filter: 'date' },
      {
        id: 'triggerKind',
        label: '觸發',
        filter: {
          options: Object.entries(RUN_TRIGGERS).map(([value, label]) => ({ value, label })),
        },
      },
      { id: 'status', label: '結果', hideable: false, filter: { options: RUN_STATUS_OPTIONS } },
      { id: 'host', label: '主機', filter: 'text' },
    ],
    sort: { column: 'startedAt', direction: 'desc' },
    poll: () => (this.selected()?.currentRun ? ACTIVE_POLL : IDLE_POLL),
  });
  readonly runColumns = [...this.runs.columns];
  readonly runRows = computed(() =>
    this.runs.rows().map((row) => ({
      row,
      time: formatDate(row.startedAt),
      trigger: RUN_TRIGGERS[row.triggerKind] ?? row.triggerKind,
      status: runStatus(row.status),
      duration: runDuration(row),
      failure: row.status === 'failed' ? runFailure(row) : '',
    })),
  );

  readonly jobs = endpointTable({
    feature: 'scheduling',
    params: () => (this.view() === 'queue' ? true : undefined),
    load: (_params, table, signal) => this.api.jobs(table, signal),
    columns: [
      { id: 'createdAt', label: '建立', hideable: false, sort: 'desc', filter: 'date' },
      { id: 'kind', label: '類型', filter: { options: JOB_KIND_OPTIONS } },
      { id: 'label', label: '內容', hideable: false, sort: true, filter: 'text' },
      { id: 'status', label: '狀態', hideable: false, filter: { options: JOB_STATUS_OPTIONS } },
      { id: 'attempt', label: '嘗試', sort: 'desc', filter: 'number', numeric: true },
      { id: 'updatedAt', label: '更新', sort: 'desc', filter: 'date' },
    ],
    sort: { column: 'createdAt', direction: 'desc' },
    poll: (): number => (this.jobsActive() ? ACTIVE_POLL : IDLE_POLL),
  });
  readonly jobColumns = [...this.jobs.columns];
  private readonly jobsActive = computed<boolean>(() =>
    this.jobs.rows().some((job) => job.status === 'queued' || job.status === 'running'),
  );
  readonly jobRows = computed(() =>
    this.jobs.rows().map((row) => ({
      row,
      kind: jobKindName(row.kind),
      status: jobStatus(row.status),
      created: formatDate(row.createdAt),
      updated: relativeTime(row.updatedAt),
      progress: row.totalUnits ? `${row.completedUnits} / ${row.totalUnits}` : '',
      failure: row.status === 'failed' ? jobFailure(row) : '',
      active: row.status === 'queued' || row.status === 'running',
    })),
  );
  readonly cancellingJob = action({ concurrent: true });

  reloadBoard() {
    this.boardRead.reload();
  }

  open(task: ScheduledTaskDto) {
    this.selectedKey.set(task.key);
    this.drawer()?.open();
  }

  /** 執行、停止、暫停、恢復共用：同一個任務一次只送一個要求；成功後立刻重讀看板，不等下一次輪詢。 */
  async act(task: ScheduledTaskDto, kind: 'run' | 'cancel' | 'pause' | 'resume') {
    const outcome = await this.running.run(() => this.api[kind](task.key), { key: task.key });
    if (outcome.ok) {
      this.boardRead.reload();
      if (this.selectedKey() === task.key) this.runs.reload();
    }
  }

  async cancelJob(job: QueuedJobDto) {
    const outcome = await this.cancellingJob.run(() => this.api.cancelJob(job.id), {
      key: job.id,
    });
    if (outcome.ok) this.jobs.reload();
  }
}
