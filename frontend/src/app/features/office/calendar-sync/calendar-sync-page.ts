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
import { DataTable, DataTableColumn, TableState } from '@shared/ui/data-table';
import { Icon } from '@shared/ui/icon';
import { Notice } from '@shared/ui/notice';
import { PendingButton } from '@shared/ui/pending-button';
import { Select, type SelectOption } from '@shared/ui/select';
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

/**
 * Google 日曆同步（`calendar-sync`）：一張表為主的資料頁，工具列切換日曆事件／執行紀錄與要看的日曆，
 * 待同步與推送失敗的數量放在表格標題旁。開發與正式共用資料庫，預設看本站設定的日曆；選「全部日曆」時表格多一欄日曆。
 * 有 `calendar-sync.manage` 時可立即同步、確認被安全閥擋下的移除與測試連線（這些都只作用在本站設定的日曆）。
 * 一輪結束（`lastCheck.at` 改變）時重讀目前的表格與日曆清單；事件沒有詳情，Google 上的事件以連結開啟。
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
    Select,
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
    /* 日曆 ID 很長：選單限寬、名稱截斷（滑過看全文），選項面板跟著觸發鈕的寬度。 */
    .calendar-sync-calendar {
      max-width: min(22rem, 100%);
    }
    .calendar-sync-calendar-column {
      --column-width: 16rem;
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
  readonly relativeTime = relativeTime;
  readonly pendingTone = eventState('pending').tone;
  readonly failedTone = eventState('failed').tone;

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

  private readonly calendarsRead = apiResource({
    feature: 'calendar-sync',
    loader: (_params, signal) => this.api.calendars(signal),
  });
  readonly calendarsLoading = this.calendarsRead.loading;
  /** 使用者選的日曆；null 表示還沒選，跟著本站設定。空字串是全部日曆。 */
  private readonly chosenCalendar = signal<string | null>(null);
  /** 目前看的日曆；讀到狀態前為 undefined，表格先不讀，免得先讀全部再換成本站的日曆。 */
  readonly calendar = computed(() => {
    const chosen = this.chosenCalendar();
    if (chosen !== null) return chosen;
    const status = this.status();
    return status ? (status.calendarId ?? '') : undefined;
  });
  readonly allCalendars = computed(() => this.calendar() === '');
  readonly calendarOptions = computed((): SelectOption[] => {
    const calendars = this.calendarsRead.value() ?? [];
    const total = calendars.reduce((sum, item) => sum + item.total, 0);
    return [
      ...calendars.map((item) => ({
        value: item.calendarId,
        label: item.calendarId,
        description: `${formatNumber(item.total)} 筆事件`,
        tags: item.configured ? [{ label: '本站設定' }] : undefined,
      })),
      { value: '', label: '全部日曆', description: `${formatNumber(total)} 筆事件` },
    ];
  });
  /** 表格標題旁的待同步與推送失敗數量，跟著目前看的日曆。 */
  readonly counts = computed(() => {
    const calendar = this.calendar();
    const rows = (this.calendarsRead.value() ?? []).filter(
      (item) => !calendar || item.calendarId === calendar,
    );
    return {
      pending: rows.reduce((sum, item) => sum + item.pending, 0),
      failed: rows.reduce((sum, item) => sum + item.failed, 0),
    };
  });

  readonly events = endpointTable({
    feature: 'calendar-sync',
    params: () => (this.view() === 'events' ? this.calendar() : undefined),
    load: (calendar, table, signal) => this.api.events(calendar, table, signal),
    columns: [
      { id: 'startsAt', label: '期間', hideable: false, sort: 'desc', filter: 'date' },
      { id: 'calendarId', label: '日曆', sort: true, filter: 'text' },
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
  /** 只看一個日曆時「日曆」欄沒有資訊，也不列在欄位選擇裡。 */
  readonly eventColumns = computed(() => this.pickable(this.events.columns));
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
    params: () => (this.view() === 'runs' ? this.calendar() : undefined),
    load: (calendar, table, signal) => this.api.runs(calendar, table, signal),
    columns: [
      { id: 'startedAt', label: '時間', hideable: false, sort: 'desc', filter: 'date' },
      { id: 'calendarId', label: '日曆', sort: true, filter: 'text' },
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
  readonly runColumns = computed(() => this.pickable(this.runs.columns));
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
          this.calendarsRead.reload();
          if (this.awaitingUntil() !== null) this.awaitingUntil.set(null);
        });
      }
      seen = at;
    });
  }

  chooseCalendar(calendarId: string) {
    this.chosenCalendar.set(calendarId);
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
    this.calendarsRead.reload();
  }

  async testConnection() {
    this.connection.set(null);
    const outcome = await this.testing.run(() => this.api.testConnection());
    if (outcome.ok) this.connection.set(outcome.value);
  }

  reloadStatus() {
    this.statusRead.reload();
  }

  private pickable<T extends { id: string }>(columns: readonly T[]): T[] {
    return this.allCalendars()
      ? [...columns]
      : columns.filter((column) => column.id !== 'calendarId');
  }
}
