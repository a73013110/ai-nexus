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
import { RouterLink } from '@angular/router';
import { FeatureAccessNotice } from '@core/layout/feature-access-notice';
import { FeaturePage } from '@core/layout/feature-page';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { apiResource } from '@core/api/api-resource';
import { safeMessage } from '@core/errors/safe-errors';
import type { DiagnosticSummary, LogLevel } from '@core/api/schema';
import { ViewScope } from '@shared/browser/view-scope';
import { downloadBlob } from '@shared/browser/download';
import { formatDate } from '@shared/browser/format';
import { CopyValue } from '@shared/ui/copy-value';
import {
  DataTable,
  DataTableColumn,
  DataTableRow,
  TablePagination,
  TableSortHeader,
  TableState,
  type TableColumn,
  type TableSortDirection,
} from '@shared/ui/data-table';
import { Field } from '@shared/ui/field';
import { Icon } from '@shared/ui/icon';
import { Notice } from '@shared/ui/notice';
import { QueryBar } from '@shared/ui/query-bar';
import { queryState } from '@shared/ui/query-state';
import { Select, type SelectOption } from '@shared/ui/select';
import { StatusBadge } from '@shared/ui/status-badge';
import { TimeRangePicker } from '@shared/ui/time-range-picker';
import { DiagnosticsApi, type LogFilter } from './diagnostics-api';
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

interface LogPageRequest {
  filter: LogFilter;
  cursor: string | null;
  index: number;
}

@Component({
  selector: 'nx-system-logs-page',
  imports: [
    CopyValue,
    DataTable,
    DataTableColumn,
    DataTableRow,
    FeatureAccessNotice,
    FeaturePage,
    Field,
    Icon,
    LogDetailDrawer,
    Notice,
    QueryBar,
    RouterLink,
    Select,
    StatusBadge,
    TablePagination,
    TableSortHeader,
    TableState,
    TimeRangePicker,
  ],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './system-logs-page.html',
  styleUrl: './system-logs-page.scss',
})
export class SystemLogsPage {
  readonly session = inject(WorkspaceSession);
  private readonly api = inject(DiagnosticsApi);
  private readonly view = inject(ViewScope);
  private readonly inspector = viewChild(LogDetailDrawer);
  private readonly table = viewChild(DataTable);
  readonly query = queryState(logQueryOptions);
  readonly draft = this.query.draft;
  readonly searchKind = computed(() => searchKind(this.draft().search));
  readonly moreCount = computed(() => logMoreCount(this.draft()));
  /** The applied query, the page of it to show, and the cursor that reads that page. */
  private readonly request = signal<LogPageRequest | undefined>(undefined);
  private readonly pageRead = apiResource({
    feature: 'logs.query',
    params: this.request,
    loader: async (request, signal) => ({
      request,
      data: await this.api.list(request.filter, request.cursor, signal),
    }),
  });
  readonly page = computed(() => this.pageRead.value()?.data ?? null);
  readonly events = computed(() => this.page()?.events ?? []);
  readonly pageIndex = computed(() => this.pageRead.value()?.request.index ?? 0);
  readonly submitted = computed(() => this.request()?.filter ?? null);
  readonly loading = this.pageRead.refreshing;
  readonly readError = this.pageRead.error;
  readonly degraded = computed(() => this.page()?.degraded === true);
  readonly exporting = signal(false);
  readonly exportError = signal('');
  readonly selectedEntry = signal<DiagnosticSummary | null>(null);
  readonly selectedIndex = computed(() =>
    this.events().findIndex((entry) => entry.logId === this.selectedEntry()?.logId),
  );
  readonly pageSize = signal(50);
  readonly sortDirection = signal<TableSortDirection>('desc');
  readonly columns: TableColumn[] = [
    { id: 'time', label: '時間', hideable: false },
    { id: 'level', label: '等級' },
    { id: 'event', label: '事件／模組', hideable: false },
    { id: 'message', label: '訊息', hideable: false },
    { id: 'status', label: 'HTTP 狀態' },
    { id: 'duration', label: '耗時' },
    { id: 'correlation', label: '查證／關聯' },
  ];
  readonly ranges = LOG_RANGES;
  readonly levelOptions: SelectOption[] = LOG_LEVELS.map((value) => ({ value, label: value }));
  readonly guidOptions: SelectOption[] = LOG_GUID_FIELDS;
  readonly moreFields = LOG_MORE_FIELDS;
  readonly date = formatDate;
  readonly milliseconds = milliseconds;
  readonly tone = levelTone;
  readonly categoryName = (value: string) => value.split('.').at(-1) || value;
  /** Cursor that reads each page already visited, for going back. */
  private cursors: (string | null)[] = [null];

  constructor() {
    effect(() => {
      if (this.page()) this.table()?.resetScroll();
    });
    // Each applied query reads its first page, with the time bounds of that moment.
    effect(() => {
      const query = this.query.applied();
      untracked(() => {
        const filter = logFilter(query);
        if (typeof filter === 'string') return;
        this.read(
          { ...filter, take: this.pageSize(), sortDirection: this.sortDirection() },
          null,
          0,
        );
      });
    });
  }
  setLevels(levels: string[]) {
    this.query.set('levels', levels as LogLevel[]);
  }
  setGuid(value: string) {
    this.query.set('guid', value as LogGuidField);
  }
  setMore(key: LogMoreField, value: string) {
    this.query.set(key, value);
  }
  changePage(direction: -1 | 1) {
    const index = this.pageIndex() + direction;
    const cursor = direction === 1 ? this.page()?.nextCursor : this.cursors[index];
    if (index < 0 || (direction === 1 && !cursor)) return;
    this.goTo(index, cursor ?? null);
  }
  firstPage() {
    this.goTo(0, null);
  }
  private goTo(index: number, cursor: string | null) {
    const request = this.request();
    if (this.loading() || !request) return;
    this.read(request.filter, cursor, index);
  }
  changeSort(direction: TableSortDirection) {
    const request = this.request();
    if (this.loading() || !request) return;
    this.sortDirection.set(direction);
    this.read({ ...request.filter, sortDirection: direction }, null, 0);
  }
  changePageSize(size: number) {
    const request = this.request();
    if (this.loading() || !request || ![25, 50, 100].includes(size)) return;
    this.pageSize.set(size);
    this.read({ ...request.filter, take: size }, null, 0);
  }
  retry() {
    this.pageRead.reload();
  }
  private read(filter: LogFilter, cursor: string | null, index: number) {
    if (index === 0) this.cursors = [null];
    this.cursors[index] = cursor;
    this.inspector()?.close();
    this.closeDetail();
    this.exportError.set('');
    this.request.set({ filter, cursor, index });
  }
  closeDetail() {
    this.selectedEntry.set(null);
  }
  inspect(entry: DiagnosticSummary) {
    if (!entry.logId || !this.session.has('logs.detail')) return;
    this.selectedEntry.set(entry);
    this.inspector()?.open();
  }
  inspectAdjacent(direction: -1 | 1) {
    const entry = this.events()[this.selectedIndex() + direction];
    if (entry) this.inspect(entry);
  }
  async export() {
    const filter = this.submitted();
    if (!filter || this.exporting() || !this.session.has('logs.export')) return;
    const guard = this.view.guard();
    this.exporting.set(true);
    this.exportError.set('');
    try {
      const response = await this.api.export(filter);
      const blob = await response.blob();
      if (guard()) downloadBlob(blob, 'ai-nexus-logs', 'csv');
    } catch (error) {
      if (guard()) this.exportError.set(safeMessage(error));
    } finally {
      if (guard()) this.exporting.set(false);
    }
  }
}
