import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  linkedSignal,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { apiResource } from '@core/api/api-resource';
import { routeOf } from '@core/api/catalog';
import type { MailMessageDto } from '@core/api/schema';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { safeMessage } from '@core/errors/safe-errors';
import { FeaturePage } from '@core/layout/feature-page';
import { formatDate } from '@shared/browser/format';
import { CopyValue } from '@shared/ui/copy-value';
import { DataTable, DataTableColumn, DataTableRow, TableState } from '@shared/ui/data-table';
import { DetailDrawer } from '@shared/ui/detail-drawer';
import { DrawerPager } from '@shared/ui/drawer-pager';
import { Field } from '@shared/ui/field';
import { Icon } from '@shared/ui/icon';
import { Notice } from '@shared/ui/notice';
import { PendingButton } from '@shared/ui/pending-button';
import { QueryBar } from '@shared/ui/query-bar';
import { queryState } from '@shared/ui/query-state';
import { StatusBadge } from '@shared/ui/status-badge';
import { TableHeader } from '@shared/ui/table-header';
import { cursorTable } from '@shared/ui/table-model';
import { TimeRangePicker } from '@shared/ui/time-range-picker';
import { ViewSwitch } from '@shared/ui/view-switch';
import { MailApi } from '../mail-api';
import { MailDiagnosisDialog } from '../transport/mail-diagnosis-dialog';
import {
  MAIL_ACTIVE_POLL,
  MAIL_IDLE_POLL,
  mailError,
  mailOutcome,
  mailPollDelay,
  mailPriority,
  mailStage,
  mailStatus,
  resendBlocked,
} from './mail-presentation';
import { MAIL_RANGES, MAIL_STATUSES, mailFilters, mailQueryOptions } from './mail-query';
import { MailTestDialog } from './mail-test-dialog';
import { mailDetailTour, mailMessagesTour } from './mail-tour';

/**
 * 郵件寄送紀錄（`mail.messages`）：查證「到底有沒有寄出」，連線診斷查「為什麼寄不出」。收件者只顯示遮罩，敏感信看不到內文；
 * 重寄與立即重試另需 `mail.messages.resend`。傳輸停用或暫停寄送時頁首提示並連到監控，不另放健康卡。
 * 列表、詳情與頁首狀態依進行中的信自動更新（`mailPollDelay`），所以沒有「重新整理」：同條件再按「查詢」也會重讀第一頁。
 */
@Component({
  selector: 'nx-mail-messages-page',
  imports: [
    CopyValue,
    DataTable,
    DataTableColumn,
    DataTableRow,
    DetailDrawer,
    DrawerPager,
    FeaturePage,
    Field,
    Icon,
    MailDiagnosisDialog,
    MailTestDialog,
    Notice,
    PendingButton,
    QueryBar,
    RouterLink,
    StatusBadge,
    TableHeader,
    TableState,
    TimeRangePicker,
    ViewSwitch,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: ':host { display: block; min-width: 0; }',
  styleUrl: './mail-messages-page.scss',
  templateUrl: './mail-messages-page.html',
})
export class MailMessagesPage {
  readonly routeOf = routeOf;
  readonly session = inject(WorkspaceSession);
  private readonly api = inject(MailApi);
  readonly statuses = MAIL_STATUSES;
  readonly ranges = MAIL_RANGES;
  readonly query = queryState(mailQueryOptions);
  readonly draft = this.query.draft;
  /** 套用條件時把相對時間換算成起訖；條件不合法時不讀。 */
  private readonly filters = computed(() => {
    const filters = mailFilters(this.query.applied());
    return typeof filters === 'string' ? undefined : filters;
  });
  /** 游標是上一頁最後一封的 ID，只讀比它舊的信；不算總數，本頁滿一頁才表示可能還有。 */
  readonly table = cursorTable({
    feature: 'mail.messages',
    params: this.filters,
    load: async (filters, table, signal) => {
      const before = typeof table.cursor === 'string' ? table.cursor : undefined;
      const items = await this.api.list(filters, table.limit, before, signal);
      return { items, next: items.length === table.limit ? items.at(-1)?.id : null };
    },
    // 只有第一頁會出現新入列的信；較舊的頁面只在有進行中的信時更新。
    poll: (page, index) =>
      page ? mailPollDelay(page.items, index === 0 ? MAIL_IDLE_POLL : null) : null,
    columns: [
      { id: 'time', label: '時間', hideable: false },
      { id: 'status', label: '狀態', hideable: false },
      { id: 'recipient', label: '收件者' },
      { id: 'subject', label: '主旨', hideable: false },
      { id: 'category', label: '分類' },
      { id: 'attempts', label: '嘗試', numeric: true },
    ],
    navigated: () => {
      this.drawer()?.close();
      this.selectedId.set(null);
    },
  });
  private readonly statusRead = apiResource({
    feature: 'mail.messages',
    loader: (_params, signal) => this.api.status(signal),
    poll: (status) =>
      status && (status.pausedUntil || status.queued + status.sending > 0)
        ? MAIL_ACTIVE_POLL
        : MAIL_IDLE_POLL,
  });
  readonly status = this.statusRead.value;
  /**
   * 使用者感覺得到的讀取（換條件、換頁、重試與寫入後重讀）：擋操作、表格淡化。
   * 背景更新（輪詢）不算，不打斷閱讀。
   */
  readonly loading = this.table.refreshing;
  /** 同一頁的背景更新裡新出現的列（新入列的信），進場時有動效；換頁或換條件時清空。 */
  private readonly arrived = linkedSignal<
    { revision: number; rows: readonly MailMessageDto[] },
    ReadonlySet<string>
  >({
    source: () => ({ revision: this.table.revision(), rows: this.table.rows() }),
    computation: (page, previous) => {
      const before = previous?.source;
      if (!before || before.revision !== page.revision || !before.rows.length) return new Set();
      const known = new Set(before.rows.map((row) => row.id));
      return new Set(page.rows.filter((row) => !known.has(row.id)).map((row) => row.id));
    },
  });
  readonly presentedRows = computed(() => {
    const now = Date.now();
    return this.table.rows().map((message) => ({
      message,
      date: formatDate(message.createdAt),
      status: mailStatus(message, now),
      error: mailError(message.errorCode),
      arrived: this.arrived().has(message.id),
    }));
  });
  readonly selectedId = signal<string | null>(null);
  readonly selectedIndex = computed(() =>
    this.presentedRows().findIndex((row) => row.message.id === this.selectedId()),
  );
  readonly selected = computed(() => this.presentedRows()[this.selectedIndex()] ?? null);
  private readonly detailRead = apiResource({
    feature: 'mail.messages',
    params: () => this.selectedId() ?? undefined,
    loader: (id, signal) => this.api.get(id, signal),
    poll: (detail) => (detail ? mailPollDelay([detail.message], null) : null),
  });
  readonly detail = computed(() => {
    const detail = this.detailRead.value();
    if (!detail || detail.message.id !== this.selectedId()) return null;
    return {
      detail,
      attempts: detail.attempts.map((attempt) => ({
        attempt,
        outcome: mailOutcome(attempt.outcome),
        started: formatDate(attempt.startedAt),
        error: mailError(attempt.errorCode),
        stage: mailStage(attempt.stage),
      })),
      priority: mailPriority(detail.message.priority),
      blocked: resendBlocked(detail.resendBlockedBy),
    };
  });
  readonly detailLoading = this.detailRead.loading;
  /** 詳情讀取失敗後重試中。 */
  readonly detailRefreshing = this.detailRead.refreshing;
  /** 骨架列的寬度（%），長短交錯才不像一整塊色塊。 */
  protected readonly skeleton = [40, 88, 72, 92, 64];
  private readonly logsName = computed(() =>
    this.session.has('diagnostics.logs') ? this.session.featureName('diagnostics.logs') : null,
  );
  /** 沒有 `mail.messages` 時頁面只剩標題，不給導覽。 */
  readonly tour = computed(() =>
    this.session.has('mail.messages')
      ? mailMessagesTour({
          resend: this.session.has('mail.messages.resend'),
          logs: this.logsName(),
        })
      : null,
  );
  /** 動作的條件與模板相同：等待中可立即重試，否則看伺服器的 `canResend`；詳情載入前不給導覽。 */
  readonly drawerTour = computed(() => {
    const view = this.detail();
    if (!view) return null;
    const { message, canResend } = view.detail;
    return mailDetailTour({
      action: !this.session.has('mail.messages.resend')
        ? null
        : message.status === 'queued'
          ? 'retry'
          : canResend
            ? 'resend'
            : null,
      attempts: view.attempts.length > 0,
      logs: this.logsName(),
    });
  });
  readonly detailError = this.detailRead.error;
  readonly drawer = viewChild(DetailDrawer);
  private readonly testDialog = viewChild(MailTestDialog);
  private readonly diagnosisDialog = viewChild(MailDiagnosisDialog);
  readonly notice = signal('');
  readonly actionError = signal('');
  readonly resending = signal(false);
  readonly retrying = signal(false);
  readonly savingSettings = signal(false);
  readonly settingsError = signal('');
  /** 同一次開啟的詳情重寄共用一個冪等鍵：連點或逾時重試只建立一封。 */
  private resendKey = '';
  readonly formatDate = formatDate;
  readonly mailError = mailError;

  constructor() {
    // 列表先看到開啟中那封的狀態變了（背景更新），詳情跟著重讀，兩邊不會不一致。
    let watched: string | undefined;
    effect(() => {
      const row = this.selected()?.message;
      const key = row ? `${row.id}:${row.status}:${row.attempts}` : undefined;
      if (watched && key && key !== watched && watched.startsWith(row!.id))
        untracked(() => this.detailRead.reload());
      watched = key;
    });
  }

  /** 表格讀取失敗後重試（表格自己重讀）時，頁首狀態一起更新；平常由自動更新負責。 */
  reloadStatus() {
    this.statusRead.reload();
  }
  reloadDetail() {
    this.detailRead.reload();
  }
  openDetails(id: string) {
    if (this.selectedId() !== id) this.resendKey = crypto.randomUUID();
    this.actionError.set('');
    this.selectedId.set(id);
    this.drawer()?.open();
  }
  adjacent(direction: -1 | 1) {
    const row = this.presentedRows()[this.selectedIndex() + direction];
    if (row) this.openDetails(row.message.id);
  }
  openTest() {
    this.notice.set('');
    this.testDialog()?.open();
  }
  openDiagnosis() {
    this.diagnosisDialog()?.open();
  }
  testSent(id: string) {
    this.notice.set(`測試信已排入佇列（訊息 ID ${id}），寄送結果請看第一筆紀錄。`);
    this.reread();
  }
  /** 寫入後回到第一頁重讀（新信在最前面），並更新頁首狀態。 */
  private reread() {
    this.table.goTo(0);
    this.table.reload();
    this.statusRead.reload();
  }
  async toggleAutoRetry(autoRetry: boolean) {
    if (this.savingSettings()) return;
    this.savingSettings.set(true);
    this.settingsError.set('');
    try {
      const saved = await this.api.updateSettings(autoRetry);
      const current = this.statusRead.value();
      if (current) this.statusRead.value.set({ ...current, autoRetry: saved.autoRetry });
    } catch (error) {
      this.settingsError.set(safeMessage(error));
    } finally {
      this.savingSettings.set(false);
    }
  }
  async retry() {
    const id = this.selectedId();
    if (!id || this.retrying()) return;
    this.retrying.set(true);
    this.actionError.set('');
    try {
      await this.api.retry(id);
      this.notice.set('已排入立即重試，結果會自動更新在這封信的寄送嘗試。');
      this.table.reload();
      this.detailRead.reload();
      this.statusRead.reload();
    } catch (error) {
      this.actionError.set(safeMessage(error));
    } finally {
      this.retrying.set(false);
    }
  }
  async resend() {
    const id = this.selectedId();
    if (!id || this.resending()) return;
    this.resending.set(true);
    this.actionError.set('');
    try {
      const result = await this.api.resend(id, this.resendKey);
      this.notice.set(
        result.duplicate
          ? '這封信已經重寄過，沒有建立新的信。'
          : `已建立重寄的信（訊息 ID ${result.messageId}），寄送結果請看第一筆紀錄。`,
      );
      this.reread();
    } catch (error) {
      this.actionError.set(safeMessage(error));
    } finally {
      this.resending.set(false);
    }
  }
}
