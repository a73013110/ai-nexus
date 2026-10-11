import { Notice } from '../../../shared/ui/notice';
import { EmptyState } from '../../../shared/ui/empty-state';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { FeaturePage } from '../../../core/layout/feature-page';
import { WorkspaceSession } from '../../../core/auth/workspace-session';
import { apiResource } from '../../../core/api/api-resource';
import { ViewScope } from '../../../shared/browser/view-scope';
import { IssueCode } from '../../../shared/ui/issue-code';
import { Icon } from '../../../shared/ui/icon';
import {
  DataTable,
  DataTableColumn,
  DataTableRow,
  TableSortHeader,
  TablePagination,
  type TableColumn,
  type TableSortDirection,
} from '../../../shared/ui/data-table';
import { StatusBadge } from '../../../shared/ui/status-badge';
import { safeMessage } from '../../../core/errors/safe-errors';
import { APP_TIME_ZONE, formatDate } from '../../../shared/browser/format';
import type { DiagnosticSummary } from '../../../core/api/schema';
import { SystemLogsApi, type LogFilter } from './system-logs-api';
import { LogFilterState } from './log-filter-state';
import { LogFilterForm } from './log-filter-form';
import { LogHealth } from './log-health';
import { LogDetailDrawer } from './log-detail-drawer';
import { levelTone, milliseconds } from './log-display';

interface LogPageRequest {
  filter: LogFilter;
  cursor: string | null;
  index: number;
}

@Component({
  selector: 'nx-system-logs-page',
  imports: [
    Notice,
    EmptyState,
    FeaturePage,
    RouterLink,
    IssueCode,
    Icon,
    DataTable,
    DataTableColumn,
    DataTableRow,
    TableSortHeader,
    TablePagination,
    StatusBadge,
    LogFilterForm,
    LogHealth,
    LogDetailDrawer,
  ],
  providers: [ViewScope, LogFilterState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './system-logs-page.html',
  styleUrl: './system-logs-page.scss',
})
export class SystemLogsPage {
  readonly session = inject(WorkspaceSession);
  private readonly api = inject(SystemLogsApi);
  private readonly view = inject(ViewScope);
  private readonly filters = inject(LogFilterState);
  private readonly inspector = viewChild(LogDetailDrawer);
  private readonly table = viewChild(DataTable);
  /** The submitted query and the page of it to show; each submission starts a new read. */
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
  readonly pageIndex = computed(() => this.pageRead.value()?.request.index ?? 0);
  readonly submitted = computed(() => this.request()?.filter ?? null);
  readonly loading = this.pageRead.refreshing;
  readonly exporting = signal(false);
  private readonly actionError = signal('');
  readonly error = computed(() => this.actionError() || this.pageRead.error());
  readonly selectedEntry = signal<DiagnosticSummary | null>(null);
  readonly selectedIndex = computed(
    () =>
      this.page()?.events.findIndex((entry) => entry.logId === this.selectedEntry()?.logId) ?? -1,
  );
  readonly pageSize = signal(50);
  readonly sortDirection = signal<TableSortDirection>('desc');
  readonly columns: TableColumn[] = [
    { id: 'time', label: '時間', hideable: false },
    { id: 'level', label: '等級' },
    { id: 'event', label: '事件／模組', hideable: false },
    { id: 'message', label: '訊息', hideable: false },
    { id: 'status', label: 'HTTP 狀態', defaultHidden: true },
    { id: 'duration', label: '耗時', defaultHidden: true },
    { id: 'correlation', label: '查證／關聯' },
  ];
  readonly timezone = APP_TIME_ZONE;
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
    this.search();
  }
  search() {
    const filter = this.filters.build();
    if (typeof filter === 'string') {
      this.actionError.set(filter);
      return;
    }
    this.read({ ...filter, take: this.pageSize(), sortDirection: this.sortDirection() }, null, 0);
  }
  changePage(direction: -1 | 1) {
    const request = this.request();
    if (this.loading() || !request) return;
    const index = this.pageIndex() + direction;
    const cursor = direction === 1 ? this.page()?.nextCursor : this.cursors[index];
    if (index < 0 || (direction === 1 && !cursor)) return;
    this.read(request.filter, cursor ?? null, index);
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
  private read(filter: LogFilter, cursor: string | null, index: number) {
    if (index === 0) this.cursors = [null];
    this.cursors[index] = cursor;
    this.inspector()?.close();
    this.closeDetail();
    this.actionError.set('');
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
    const entry = this.page()?.events[this.selectedIndex() + direction];
    if (entry) this.inspect(entry);
  }
  async export() {
    const filter = this.submitted();
    if (!filter || this.exporting() || !this.session.has('logs.export')) return;
    const guard = this.view.guard();
    this.exporting.set(true);
    this.actionError.set('');
    try {
      const response = await this.api.export(filter);
      const blob = await response.blob();
      if (!guard()) return;
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = 'ai-nexus-logs.csv';
      link.click();
      URL.revokeObjectURL(url);
    } catch (error) {
      if (guard()) this.actionError.set(safeMessage(error));
    } finally {
      if (guard()) this.exporting.set(false);
    }
  }
}
