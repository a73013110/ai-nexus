import { EmptyState } from '../../../shared/ui/empty-state';
import { ViewMotion } from '../../../shared/ui/view-motion';
import { FilterPanel } from '../../../shared/ui/filter-panel';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FeaturePage } from '../../../shared/ui/feature-page';
import { WorkspaceSession } from '../../../core/auth/workspace-session';
import { ViewScope } from '../../../shared/browser/view-scope';
import { IssueCode } from '../../../shared/ui/issue-code';
import { Icon } from '../../../shared/ui/icon';
import { Select, type SelectOption } from '../../../shared/ui/select';
import { Field } from '../../../shared/ui/field';
import {
  DataTable,
  DataTableColumn,
  DataTableRow,
  TableSortHeader,
  TablePagination,
  type TableColumn,
  type TableSortDirection,
} from '../../../shared/ui/data-table';
import { DateTimePicker } from '../../../shared/ui/date-time-picker';
import { StatusBadge } from '../../../shared/ui/status-badge';
import type { BadgeTone } from '../../../shared/ui/count-badge';
import { DetailDrawer } from '../../../shared/ui/detail-drawer';
import { Tabs, type TabItem } from '../../../shared/ui/tabs';
import { CodeBlock } from '../../../shared/ui/code-block';
import { Disclosure } from '../../../shared/ui/disclosure';
import { safeMessage } from '../../../core/api/safe-errors';
import {
  APP_TIME_ZONE,
  formatBytes,
  formatDate,
  formatDateTimeInput,
  parseDateTimeInput,
} from '../../../shared/browser/format';
import {
  SystemLogsApi,
  type LogEntry,
  type LogDetail,
  type LogFilter,
  type LogPage,
  type LogHealth,
} from './system-logs-api';

type Fields = Omit<LogFilter, 'from' | 'to' | 'take' | 'sortDirection'>;
const emptyFields = (): Fields => ({
  level: '',
  category: '',
  eventName: '',
  issueCode: '',
  traceId: '',
  jobId: '',
  runId: '',
  errorCode: '',
  instance: '',
  text: '',
});
const levels = ['Trace', 'Debug', 'Information', 'Warning', 'Error', 'Critical'];
const tones: Record<string, BadgeTone> = {
  Information: 'info',
  Warning: 'warning',
  Error: 'danger',
  Critical: 'danger',
};

@Component({
  selector: 'nx-system-logs-page',
  imports: [
    EmptyState,
    ViewMotion,
    FilterPanel,
    FeaturePage,
    FormsModule,
    RouterLink,
    IssueCode,
    Icon,
    Select,
    Field,
    DataTable,
    DataTableColumn,
    DataTableRow,
    TableSortHeader,
    TablePagination,
    DateTimePicker,
    StatusBadge,
    DetailDrawer,
    Tabs,
    CodeBlock,
    Disclosure,
  ],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './system-logs-page.html',
  styleUrl: './system-logs-page.scss',
})
export class SystemLogsPage {
  readonly session = inject(WorkspaceSession);
  private readonly api = inject(SystemLogsApi);
  private readonly view = inject(ViewScope);
  private readonly drawer = viewChild.required(DetailDrawer);
  private readonly table = viewChild(DataTable);
  readonly page = signal<LogPage | null>(null);
  readonly selectedEntry = signal<LogEntry | null>(null);
  readonly detail = signal<LogDetail | null>(null);
  readonly related = signal<LogEntry[]>([]);
  readonly health = signal<LogHealth | null>(null);
  readonly loading = signal(false);
  readonly detailLoading = signal(false);
  readonly relatedLoading = signal(false);
  readonly healthLoading = signal(false);
  readonly exporting = signal(false);
  readonly error = signal('');
  readonly detailError = signal('');
  readonly relatedError = signal('');
  readonly healthError = signal('');
  readonly detailTab = signal('overview');
  readonly pageIndex = signal(0);
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
  readonly range = signal('24');
  readonly timezone = APP_TIME_ZONE;
  readonly date = formatDate;
  readonly bytes = formatBytes;
  readonly milliseconds = (value: number | null) =>
    value === null ? '—' : value.toLocaleString('zh-TW', { maximumFractionDigits: 3 }) + ' ms';
  readonly tone = (level: string) => tones[level] ?? 'neutral';
  readonly categoryName = (value: string) => value.split('.').at(-1) || value;
  readonly levelOptions: SelectOption[] = [
    { value: '', label: '全部等級' },
    ...levels.map((value) => ({ value, label: value })),
  ];
  readonly ranges: SelectOption[] = [
    { value: '.25', label: '最近 15 分鐘' },
    { value: '1', label: '最近 1 小時' },
    { value: '24', label: '最近 24 小時' },
    { value: '168', label: '最近 7 天' },
    { value: 'custom', label: '自訂時間' },
  ];
  readonly tabs: TabItem[] = [
    { value: 'overview', label: '概覽' },
    { value: 'properties', label: '受控屬性' },
    { value: 'exception', label: '例外堆疊' },
    { value: 'timeline', label: '關聯流程' },
  ];
  readonly advancedFields: { key: keyof Fields; label: string; maxLength: number }[] = [
    { key: 'category', label: '模組 Category', maxLength: 180 },
    { key: 'eventName', label: '事件名稱', maxLength: 100 },
    { key: 'traceId', label: 'TraceId', maxLength: 32 },
    { key: 'jobId', label: 'JobId', maxLength: 36 },
    { key: 'runId', label: 'RunId', maxLength: 36 },
    { key: 'errorCode', label: '錯誤碼', maxLength: 80 },
    { key: 'instance', label: '執行個體', maxLength: 100 },
    { key: 'text', label: '訊息模板文字（限一天）', maxLength: 72 },
  ];
  private readonly initialEnd = Date.now() + 60_000;
  readonly fromLocal = signal(formatDateTimeInput(new Date(this.initialEnd - 24 * 60 * 60_000)));
  readonly toLocal = signal(formatDateTimeInput(new Date(this.initialEnd)));
  readonly filter = signal<Fields>(emptyFields());
  readonly advancedCount = computed(
    () =>
      Object.entries(this.filter()).filter(
        ([key, value]) => !['level', 'issueCode'].includes(key) && value.trim(),
      ).length,
  );
  readonly selectedIndex = computed(
    () =>
      this.page()?.events.findIndex((entry) => entry.logId === this.selectedEntry()?.logId) ?? -1,
  );
  readonly properties = computed(() => {
    const raw = this.detail()?.propertiesJson ?? '{}';
    try {
      return JSON.stringify(JSON.parse(raw), null, 2);
    } catch {
      return raw;
    }
  });
  readonly requestContext = computed(() => {
    try {
      const properties: Record<string, unknown> = JSON.parse(this.detail()?.propertiesJson ?? '{}');
      const fields = [
        ['ClientAddress', '連線 IP'],
        ['UserAgent', 'User-Agent（用戶端提供）'],
        ['RequestProtocol', 'HTTP 協定'],
        ['RequestScheme', '傳輸方式'],
        ['RequestOutcome', '請求結果'],
        ['RequestAborted', '用戶端中止'],
        ['ResponseStarted', '回應已開始'],
      ];
      const outcomes: Record<string, string> = {
        completed: '回應已完成',
        failed: '執行失敗',
        cancelled: '用戶端取消',
      };
      return fields
        .filter(
          ([key]) =>
            properties[key] !== null && properties[key] !== undefined && properties[key] !== '',
        )
        .map(([key, label]) => ({
          key,
          label,
          value:
            key === 'RequestOutcome'
              ? (outcomes[String(properties[key])] ?? String(properties[key]))
              : typeof properties[key] === 'boolean'
                ? properties[key]
                  ? '是'
                  : '否'
                : String(properties[key]),
        }));
    } catch {
      return [];
    }
  });
  private submitted: LogFilter | null = null;
  private cursors: (string | null)[] = [null];
  private controller?: AbortController;
  private detailController?: AbortController;
  private version = 0;
  private detailVersion = 0;

  constructor() {
    const params = inject(ActivatedRoute).snapshot.queryParamMap;
    const traceId = params.get('traceId'),
      issueCode = params.get('issueCode');
    if (traceId && /^[a-f\d]{32}$/i.test(traceId)) this.setFilter('traceId', traceId.toLowerCase());
    if (issueCode && /^NX-[a-f\d]{32}$/i.test(issueCode)) this.setFilter('issueCode', issueCode);
    const from = Date.parse(params.get('from') || ''),
      to = Date.parse(params.get('to') || '');
    if (
      Number.isFinite(from) &&
      Number.isFinite(to) &&
      from < to &&
      to - from <= 24 * 60 * 60_000
    ) {
      this.fromLocal.set(formatDateTimeInput(new Date(from)));
      this.toLocal.set(formatDateTimeInput(new Date(to)));
      this.range.set('custom');
    }
    inject(DestroyRef).onDestroy(() => {
      this.controller?.abort();
      this.detailController?.abort();
    });
    void this.initialize();
  }
  private async initialize() {
    const guard = this.view.guard();
    try {
      await this.session.load();
      if (guard() && this.session.has('logs.query')) await this.search();
    } catch (error) {
      if (guard()) this.error.set(safeMessage(error));
    }
  }
  setFilter(key: keyof Fields, value: string) {
    this.filter.update((fields) => ({ ...fields, [key]: value }));
  }
  setDate(which: 'from' | 'to', value: string) {
    (which === 'from' ? this.fromLocal : this.toLocal).set(value);
    this.range.set('custom');
  }
  setRange(value: string) {
    this.range.set(value);
    if (value === 'custom') return;
    const now = Date.now();
    this.fromLocal.set(formatDateTimeInput(new Date(now + 60_000 - Number(value) * 60 * 60_000)));
    this.toLocal.set(formatDateTimeInput(new Date(now + 60_000)));
  }
  resetFilters() {
    this.filter.set(emptyFields());
    this.setRange('24');
    void this.search();
  }
  async search() {
    if (!this.session.has('logs.query')) return;
    const from = parseDateTimeInput(this.fromLocal()),
      to = parseDateTimeInput(this.toLocal());
    if (!Number.isFinite(from.getTime()) || !Number.isFinite(to.getTime()) || from >= to) {
      this.error.set('請選擇有效的時間範圍，結束時間須晚於起始時間。');
      return;
    }
    const fields = Object.fromEntries(
      Object.entries(this.filter()).map(([key, value]) => [key, value.trim()]),
    ) as Fields;
    if (fields.text && to.getTime() - from.getTime() > 24 * 60 * 60_000) {
      this.error.set('訊息模板文字查詢限一天，請縮小時間範圍。');
      return;
    }
    this.submitted = {
      ...fields,
      from: from.toISOString(),
      to: to.toISOString(),
      take: this.pageSize(),
      sortDirection: this.sortDirection(),
    };
    this.cursors = [null];
    await this.loadPage(null, 0);
  }
  async changePage(direction: -1 | 1) {
    if (this.loading() || !this.submitted) return;
    const index = this.pageIndex() + direction;
    const cursor = direction === 1 ? this.page()?.nextCursor : this.cursors[index];
    if (index < 0 || (direction === 1 && !cursor)) return;
    await this.loadPage(cursor ?? null, index);
  }
  async changeSort(direction: TableSortDirection) {
    if (this.loading() || !this.submitted) return;
    this.sortDirection.set(direction);
    this.submitted = { ...this.submitted, sortDirection: direction };
    this.cursors = [null];
    await this.loadPage(null, 0);
  }
  async changePageSize(size: number) {
    if (this.loading() || !this.submitted || ![25, 50, 100].includes(size)) return;
    this.pageSize.set(size);
    this.submitted = { ...this.submitted, take: size };
    this.cursors = [null];
    await this.loadPage(null, 0);
  }
  private async loadPage(cursor: string | null, index: number) {
    this.controller?.abort();
    const controller = (this.controller = new AbortController());
    const version = ++this.version;
    const guard = this.view.guard();
    this.closeDetail();
    this.drawer().close();
    this.loading.set(true);
    this.error.set('');
    try {
      const page = await this.api.list(this.submitted!, cursor, controller.signal);
      if (guard() && version === this.version) {
        this.page.set(page);
        this.pageIndex.set(index);
        this.cursors[index] = cursor;
        this.health.set(page.health ?? null);
        this.table()?.resetScroll();
      }
    } catch (error) {
      if (!controller.signal.aborted && guard() && version === this.version) {
        this.error.set(safeMessage(error));
        this.page.set(null);
      }
    } finally {
      if (guard() && version === this.version) this.loading.set(false);
    }
  }
  closeDetail() {
    this.detailController?.abort();
    ++this.detailVersion;
    this.selectedEntry.set(null);
    this.detail.set(null);
    this.related.set([]);
    this.detailLoading.set(false);
    this.relatedLoading.set(false);
    this.detailError.set('');
    this.relatedError.set('');
  }
  async inspect(entry: LogEntry) {
    if (!entry.logId || !this.session.has('logs.detail')) return;
    this.detailController?.abort();
    const controller = (this.detailController = new AbortController());
    const version = ++this.detailVersion;
    const guard = this.view.guard();
    const current = () => !controller.signal.aborted && guard() && version === this.detailVersion;
    if (!this.selectedEntry()) this.detailTab.set('overview');
    this.selectedEntry.set(entry);
    this.detailLoading.set(true);
    this.detailError.set('');
    this.relatedError.set('');
    this.detail.set(null);
    this.related.set([]);
    this.drawer().open();
    const tasks = [
      (async () => {
        try {
          const detail = await this.api.detail(entry.logId, controller.signal);
          if (current()) this.detail.set(detail);
        } catch (error) {
          if (current()) this.detailError.set(safeMessage(error));
        } finally {
          if (current()) this.detailLoading.set(false);
        }
      })(),
    ];
    if (entry.traceId || entry.jobId || entry.runId) {
      this.relatedLoading.set(true);
      tasks.push(
        (async () => {
          try {
            const filter: LogFilter = {
              ...emptyFields(),
              from: this.submitted!.from,
              to: this.submitted!.to,
              traceId: entry.traceId ?? '',
              jobId: entry.jobId ?? '',
              runId: entry.runId ?? '',
            };
            const page = await this.api.list(filter, undefined, controller.signal);
            if (current()) this.related.set([...page.events].reverse());
          } catch (error) {
            if (current()) this.relatedError.set(safeMessage(error));
          } finally {
            if (current()) this.relatedLoading.set(false);
          }
        })(),
      );
    } else {
      this.relatedLoading.set(false);
      this.related.set([entry]);
    }
    await Promise.all(tasks);
  }
  inspectAdjacent(direction: -1 | 1) {
    const entry = this.page()?.events[this.selectedIndex() + direction];
    if (entry) void this.inspect(entry);
  }
  selectTab(value: string) {
    this.detailTab.set(value);
    this.drawer().resetScroll();
  }
  async refreshHealth() {
    if (this.healthLoading()) return;
    const guard = this.view.guard();
    this.healthLoading.set(true);
    this.healthError.set('');
    try {
      const health = await this.api.health();
      if (guard()) this.health.set(health);
    } catch (error) {
      if (guard()) this.healthError.set(safeMessage(error));
    } finally {
      if (guard()) this.healthLoading.set(false);
    }
  }
  async export() {
    if (!this.submitted || this.exporting() || !this.session.has('logs.export')) return;
    const guard = this.view.guard();
    this.exporting.set(true);
    this.error.set('');
    try {
      const response = await this.api.export(this.submitted);
      const blob = await response.blob();
      if (!guard()) return;
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = 'ai-nexus-logs.csv';
      link.click();
      URL.revokeObjectURL(url);
    } catch (error) {
      if (guard()) this.error.set(safeMessage(error));
    } finally {
      if (guard()) this.exporting.set(false);
    }
  }
}
