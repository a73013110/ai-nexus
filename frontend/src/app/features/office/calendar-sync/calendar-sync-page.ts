import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import type { CalendarSyncConnectionDto } from '@core/api/schema';
import { apiResource } from '@core/api/api-resource';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { FeaturePage } from '@core/layout/feature-page';
import { action } from '@shared/browser/action';
import { formatDate, formatNumber, relativeTime } from '@shared/browser/format';
import { ViewScope } from '@shared/browser/view-scope';
import { ConfirmDialog } from '@shared/ui/confirm-dialog';
import type { BadgeTone } from '@shared/ui/count-badge';
import { DataTable, DataTableColumn, TableState } from '@shared/ui/data-table';
import { Icon } from '@shared/ui/icon';
import { Notice } from '@shared/ui/notice';
import { PendingButton } from '@shared/ui/pending-button';
import { StatGrid, StatTile } from '@shared/ui/stat-tile';
import { StatusBadge } from '@shared/ui/status-badge';
import { TableHeader } from '@shared/ui/table-header';
import { endpointTable } from '@shared/ui/table-model';
import { ViewSwitch } from '@shared/ui/view-switch';
import { CalendarSyncApi } from './calendar-sync-api';
import {
  EVENT_KINDS,
  EVENT_STATE_OPTIONS,
  FLOW_STATUSES,
  RUN_STATUS_OPTIONS,
  RUN_TRIGGERS,
  eventState,
  failureText,
  runStatus,
} from './calendar-sync-presentation';

/** 同步進行中或剛按下立即同步時，狀態每 3 秒讀一次；平常 30 秒。 */
const ACTIVE_POLL = 3000;
const IDLE_POLL = 30000;
/** 按下立即同步後最多加快輪詢多久；比一輪同步的時間長，又不會在被略過時一直快速輪詢。 */
const AWAIT_LIMIT = 120000;

const VIEWS = [
  { value: 'events', label: '日曆事件' },
  { value: 'runs', label: '執行紀錄' },
] as const;
type View = (typeof VIEWS)[number]['value'];

interface StatItem {
  key: string;
  label: string;
  value: string | null;
  tone?: BadgeTone;
  exact?: string;
}

/**
 * Google 日曆同步（`calendar-sync`）：上方是狀態與數量，下方切換日曆事件與執行紀錄。
 * 有 `calendar-sync.manage` 時可立即同步、確認被安全閥擋下的移除與測試連線。
 * 一輪結束（`lastCheck.at` 改變）時重讀目前的表格；事件沒有詳情，Google 上的事件以連結開啟。
 */
@Component({
  selector: 'nx-calendar-sync-page',
  imports: [
    ConfirmDialog,
    DataTable,
    DataTableColumn,
    FeaturePage,
    Icon,
    Notice,
    PendingButton,
    StatGrid,
    StatTile,
    StatusBadge,
    TableHeader,
    TableState,
    ViewSwitch,
  ],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: block;
      min-width: 0;
    }
    .calendar-sync-table table {
      table-layout: fixed;
      min-width: 52rem;
    }
    .calendar-sync-time-column {
      --column-width: 13rem;
    }
    .calendar-sync-name-column {
      --column-width: 9rem;
    }
    .calendar-sync-short-column {
      --column-width: 6rem;
    }
    .calendar-sync-state-column {
      --column-width: 14rem;
    }
    .calendar-sync-count-column {
      --column-width: 5rem;
    }
    .calendar-sync-code {
      font-family: var(--font-code);
    }
  `,
  templateUrl: './calendar-sync-page.html',
})
export class CalendarSyncPage {
  readonly session = inject(WorkspaceSession);
  private readonly api = inject(CalendarSyncApi);
  readonly views = VIEWS;
  readonly view = signal<View>('events');
  readonly manage = computed(() => this.session.has('calendar-sync.manage'));
  readonly failureText = failureText;
  readonly formatDate = formatDate;

  /**
   * 按下立即同步後加快輪詢，到下一輪結束（`lastCheck.at` 改變）為止；背景工作可能還在前一輪，所以不看 `running`。
   * 最多加快 {@link AWAIT_LIMIT}：另一個站台持有同步鎖時這一輪會被略過，`lastCheck` 不會變，不能一直每 3 秒讀。
   */
  private readonly awaitingUntil = signal<number | null>(null);
  private readonly statusRead = apiResource({
    feature: 'calendar-sync',
    loader: (_params, signal) => this.api.status(signal),
    poll: (status) =>
      status?.running || (this.awaitingUntil() ?? 0) > Date.now() ? ACTIVE_POLL : IDLE_POLL,
  });
  readonly status = this.statusRead.value;
  readonly statusError = this.statusRead.error;
  readonly lastCheckAt = computed(() => this.status()?.lastCheck?.at ?? null);

  readonly tiles = computed((): StatItem[] => {
    const status = this.status();
    const check = status?.lastCheck;
    const count = (value: number | undefined) => (value === undefined ? null : formatNumber(value));
    return [
      { key: 'pending', label: '待同步', value: count(status?.pending) },
      {
        key: 'failed',
        label: '推送失敗',
        value: count(status?.failed),
        tone: status?.failed ? 'danger' : 'neutral',
      },
      { key: 'synced', label: '已同步', value: count(status?.synced) },
      {
        key: 'check',
        label: '最近檢查',
        value: check ? relativeTime(check.at) : status ? '尚未執行' : null,
        exact: check ? formatDate(check.at) : '',
      },
    ];
  });

  readonly events = endpointTable({
    feature: 'calendar-sync',
    params: () => (this.view() === 'events' ? true : undefined),
    load: (_params, table, signal) => this.api.events(table, signal),
    columns: [
      { id: 'startsAt', label: '期間', hideable: false, sort: 'desc', filter: 'date' },
      { id: 'applicantName', label: '申請人', hideable: false, sort: true, filter: 'text' },
      {
        id: 'kind',
        label: '類別',
        filter: {
          options: Object.entries(EVENT_KINDS).map(([value, label]) => ({ value, label })),
        },
      },
      {
        id: 'flowStatus',
        label: '簽核',
        filter: {
          options: Object.entries(FLOW_STATUSES).map(([value, label]) => ({ value, label })),
        },
      },
      { id: 'state', label: '同步', hideable: false, filter: { options: EVENT_STATE_OPTIONS } },
      { id: 'desNo', label: '文號', sort: true, filter: 'text' },
      { id: 'syncedAt', label: '同步時間', sort: 'desc', filter: 'date' },
    ],
    sort: { column: 'startsAt', direction: 'desc' },
  });
  readonly eventRows = computed(() =>
    this.events.rows().map((row) => ({
      row,
      period: `${formatDate(row.startsAt)} – ${formatDate(row.endsAt)}`,
      kind: EVENT_KINDS[row.kind] ?? row.kind,
      flow: FLOW_STATUSES[row.flowStatus] ?? row.flowStatus,
      state: row.removed && row.state === 'synced' ? eventState('removed') : eventState(row.state),
      problem: row.lastErrorCode ? failureText(row.lastErrorCode) : '',
      synced: row.syncedAt ? formatDate(row.syncedAt) : '',
    })),
  );

  readonly runs = endpointTable({
    feature: 'calendar-sync',
    params: () => (this.view() === 'runs' ? true : undefined),
    load: (_params, table, signal) => this.api.runs(table, signal),
    columns: [
      { id: 'startedAt', label: '時間', hideable: false, sort: 'desc', filter: 'date' },
      {
        id: 'triggerKind',
        label: '觸發',
        filter: {
          options: Object.entries(RUN_TRIGGERS).map(([value, label]) => ({ value, label })),
        },
      },
      { id: 'status', label: '結果', hideable: false, filter: { options: RUN_STATUS_OPTIONS } },
      { id: 'createdCount', label: '新增', sort: 'desc', filter: 'number', numeric: true },
      { id: 'updatedCount', label: '更新', sort: 'desc', filter: 'number', numeric: true },
      { id: 'deletedCount', label: '刪除', sort: 'desc', filter: 'number', numeric: true },
      { id: 'failedCount', label: '失敗', sort: 'desc', filter: 'number', numeric: true },
    ],
    sort: { column: 'startedAt', direction: 'desc' },
  });
  readonly runRows = computed(() =>
    this.runs.rows().map((row) => ({
      row,
      time: formatDate(row.startedAt),
      trigger: RUN_TRIGGERS[row.triggerKind] ?? row.triggerKind,
      status: runStatus(row.status),
      problem: row.errorCode ? failureText(row.errorCode) : '',
    })),
  );

  readonly starting = action();
  readonly testing = action();
  readonly notice = signal('');
  readonly connection = signal<CalendarSyncConnectionDto | null>(null);
  private readonly confirmation = viewChild.required(ConfirmDialog);

  constructor() {
    // 一輪結束就重讀目前的表格與狀態；第一次讀到狀態不算（表格自己會讀）。
    let seen: string | null | undefined;
    effect(() => {
      const at = this.lastCheckAt();
      if (!this.status()) return;
      if (seen !== undefined && at !== seen) {
        untracked(() => {
          this.events.reload();
          this.runs.reload();
          if (this.awaitingUntil() !== null) this.awaitingUntil.set(null);
        });
      }
      seen = at;
    });
  }

  async start(confirmRemovals = false) {
    if (confirmRemovals) {
      const held = this.status()?.lastCheck?.heldCount ?? 0;
      const confirmed = await this.confirmation().ask({
        title: '確認移除事件',
        message: `將從 Google 日曆刪除 ${formatNumber(held)} 個公文系統已沒有的事件。確定要移除嗎？`,
        confirm: '確認移除',
        danger: true,
      });
      if (!confirmed) return;
    }
    const outcome = await this.starting.run(() => this.api.start(confirmRemovals));
    if (!outcome.ok) return;
    this.awaitingUntil.set(Date.now() + AWAIT_LIMIT);
    this.notice.set('已排入同步，完成後下方會自動更新。');
    this.statusRead.reload();
  }

  async testConnection() {
    this.connection.set(null);
    const outcome = await this.testing.run(() => this.api.testConnection());
    if (outcome.ok) this.connection.set(outcome.value);
  }

  reloadStatus() {
    this.statusRead.reload();
  }
}
