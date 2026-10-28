import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { FeaturePage } from '@core/layout/feature-page';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { safeMessage } from '@core/errors/safe-errors';
import type { DiagnosticSummary, LogLevel } from '@core/api/schema';
import { ViewScope } from '@shared/browser/view-scope';
import { downloadBlob } from '@shared/browser/download';
import { formatDate } from '@shared/browser/format';
import { CopyValue } from '@shared/ui/copy-value';
import { DataTable, DataTableColumn, DataTableRow, TableState } from '@shared/ui/data-table';
import { Field } from '@shared/ui/field';
import { Icon } from '@shared/ui/icon';
import { Notice } from '@shared/ui/notice';
import { PendingButton } from '@shared/ui/pending-button';
import { QueryBar } from '@shared/ui/query-bar';
import { queryState } from '@shared/ui/query-state';
import { Select, type SelectOption } from '@shared/ui/select';
import { StatusBadge } from '@shared/ui/status-badge';
import { TableHeader } from '@shared/ui/table-header';
import { cursorTable } from '@shared/ui/table-model';
import { TimeRangePicker } from '@shared/ui/time-range-picker';
import { DiagnosticsApi } from './diagnostics-api';
import { systemLogsTour } from './diagnostics-tour';
import { LogDetailDrawer } from './log-detail-drawer';
import { levelTone, milliseconds } from './log-display';
import {
  LOG_GUID_FIELDS,
  LOG_LEVELS,
  LOG_MORE_FIELDS,
  LOG_RANGES,
  logFilter,
  logMoreCount,
  logQueryOptions,
  searchKind,
  type LogGuidField,
  type LogMoreField,
} from './log-query';
import { routeOf } from '@core/api/catalog';

@Component({
  selector: 'nx-system-logs-page',
  imports: [
    CopyValue,
    DataTable,
    DataTableColumn,
    DataTableRow,
    FeaturePage,
    Field,
    Icon,
    LogDetailDrawer,
    Notice,
    PendingButton,
    QueryBar,
    RouterLink,
    Select,
    StatusBadge,
    TableHeader,
    TableState,
    TimeRangePicker,
  ],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './system-logs-page.html',
  styleUrl: './system-logs-page.scss',
})
export class SystemLogsPage {
  /** 範本裡的連結由功能目錄取得網址。 */
  readonly routeOf = routeOf;
  readonly session = inject(WorkspaceSession);
  private readonly api = inject(DiagnosticsApi);
  private readonly view = inject(ViewScope);
  private readonly inspector = viewChild(LogDetailDrawer);
  readonly query = queryState(logQueryOptions);
  readonly draft = this.query.draft;
  readonly searchKind = computed(() => searchKind(this.draft().search));
  readonly moreCount = computed(() => logMoreCount(this.draft()));
  /** 已套用的條件；預設區間以套用當下計算起訖，條件不合法時不讀。 */
  private readonly filter = computed(() => {
    const filter = logFilter(this.query.applied());
    return typeof filter === 'string' ? undefined : filter;
  });
  /** 游標分頁、不計總數；只能依時間排序（後端以時間與事件 ID 為游標）。 */
  readonly table = cursorTable({
    feature: 'diagnostics.logs',
    params: this.filter,
    load: async (filter, table, signal) => {
      const sortDirection = table.order?.direction ?? 'desc';
      const cursor = typeof table.cursor === 'string' ? table.cursor : undefined;
      const page = await this.api.list(
        { ...filter, take: table.limit, sortDirection },
        cursor,
        signal,
      );
      return { ...page, items: page.events, next: page.nextCursor };
    },
    columns: [
      { id: 'time', label: '時間', hideable: false, sort: 'desc', time: true },
      { id: 'level', label: '等級' },
      { id: 'event', label: '事件／模組', hideable: false },
      { id: 'message', label: '訊息', hideable: false },
      { id: 'status', label: 'HTTP 狀態', header: 'HTTP', numeric: true },
      { id: 'duration', label: '耗時', numeric: true },
      { id: 'correlation', label: '查證／關聯' },
    ],
    sort: { column: 'time', direction: 'desc' },
    navigated: () => {
      this.inspector()?.close();
      this.closeDetail();
      this.exportError.set('');
    },
  });
  readonly events = this.table.rows;
  readonly loading = this.table.refreshing;
  readonly submitted = this.filter;
  readonly degraded = computed(() => this.table.data()?.degraded === true);
  readonly exporting = signal(false);
  readonly exportError = signal('');
  readonly selectedEntry = signal<DiagnosticSummary | null>(null);
  readonly selectedIndex = computed(() =>
    this.events().findIndex((entry) => entry.logId === this.selectedEntry()?.logId),
  );
  readonly ranges = LOG_RANGES;
  readonly levelOptions: SelectOption[] = LOG_LEVELS.map((value) => ({ value, label: value }));
  readonly guidOptions: SelectOption[] = LOG_GUID_FIELDS;
  readonly moreFields = LOG_MORE_FIELDS;
  readonly date = formatDate;
  readonly milliseconds = milliseconds;
  readonly tone = levelTone;
  readonly categoryName = (value: string) => value.split('.').at(-1) || value;
  /** 沒有 `diagnostics.logs` 時頁面只剩標題，不給導覽。 */
  readonly tour = computed(() => {
    const session = this.session;
    if (!session.has('diagnostics.logs')) return null;
    const name = (feature: 'monitoring' | 'audit') =>
      session.has(feature) ? session.featureName(feature) : null;
    return systemLogsTour({
      detail: session.has('diagnostics.logs.detail'),
      export: session.has('diagnostics.logs.export'),
      degraded: this.degraded(),
      monitoring: name('monitoring'),
      audit: name('audit'),
    });
  });

  setLevels(levels: string[]) {
    this.query.set('levels', levels as LogLevel[]);
  }
  setGuid(value: string) {
    this.query.set('guid', value as LogGuidField);
  }
  setMore(key: LogMoreField, value: string) {
    this.query.set(key, value);
  }
  closeDetail() {
    this.selectedEntry.set(null);
  }
  inspect(entry: DiagnosticSummary) {
    if (!entry.logId || !this.session.has('diagnostics.logs.detail')) return;
    this.selectedEntry.set(entry);
    this.inspector()?.open();
  }
  inspectAdjacent(direction: -1 | 1) {
    const entry = this.events()[this.selectedIndex() + direction];
    if (entry) this.inspect(entry);
  }
  async export() {
    if (!this.table.data() || this.exporting() || !this.session.has('diagnostics.logs.export'))
      return;
    // 預設區間（最近 N 小時）以按下匯出的當下重算，才與標籤相符；畫面上的結果可能是較早套用的。
    const filter = logFilter(this.query.applied());
    if (typeof filter === 'string') {
      this.exportError.set(filter);
      return;
    }
    const guard = this.view.guard();
    this.exporting.set(true);
    this.exportError.set('');
    try {
      const response = await this.api.export({
        ...filter,
        sortDirection: this.table.sort()?.direction ?? 'desc',
      });
      const blob = await response.blob();
      if (guard()) downloadBlob(blob, 'ai-nexus-logs', 'csv');
    } catch (error) {
      if (guard()) this.exportError.set(safeMessage(error));
    } finally {
      if (guard()) this.exporting.set(false);
    }
  }
}
