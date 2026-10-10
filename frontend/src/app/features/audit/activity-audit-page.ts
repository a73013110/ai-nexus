import { apiResource } from '../../core/api/api-resource';
import { Notice } from '../../shared/ui/notice';
import { DateTimePicker } from '../../shared/ui/date-time-picker';
import { EmptyState } from '../../shared/ui/empty-state';
import { FilterPanel } from '../../shared/ui/filter-panel';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { WorkspaceSession } from '../../core/auth/workspace-session';
import { FEATURE_NAMES } from '../../core/auth/feature-names';
import { FeaturePage } from '../../core/layout/feature-page';
import { ViewSwitch } from '../../shared/ui/view-switch';
import { Disclosure } from '../../shared/ui/disclosure';
import { AUDIT_CATEGORIES, auditLogQuery } from './audit-navigation';
import {
  ChangeDetectionStrategy,
  afterNextRender,
  Component,
  DestroyRef,
  computed,
  inject,
  signal,
  viewChild,
  viewChildren,
  linkedSignal,
  untracked,
  effect,
} from '@angular/core';
import { ActivityAuditApi, type AuditFilters } from './activity-audit-api';
import type { AuditDto, FeatureDto, ModelDto } from '../../core/api/schema';
import { FeatureSummary } from '../admin/feature-summary';
import { SearchField } from '../../shared/ui/search-field';
import { Select } from '../../shared/ui/select';
import { Icon } from '../../shared/ui/icon';
import {
  DataTable,
  DataTableColumn,
  DataTableRow,
  TablePagination,
  type TableColumn,
} from '../../shared/ui/data-table';
import { DetailDrawer } from '../../shared/ui/detail-drawer';
import { StatusBadge } from '../../shared/ui/status-badge';
import { downloadBlob } from '../../shared/browser/download';
import { toCsv } from '../../shared/browser/csv';
import { formatDate, formatModelName } from '../../shared/browser/format';
import {
  auditAction,
  auditChanges,
  auditDetails,
  auditResource,
  auditResult,
  auditRejected,
  auditContext,
} from './audit-presentation';

interface AuditPage {
  filters: AuditFilters;
  after: number | undefined;
  rows: AuditDto[];
}

@Component({
  selector: 'nx-activity-audit-page',
  imports: [
    FeaturePage,
    Notice,
    EmptyState,
    DateTimePicker,
    FilterPanel,
    SearchField,
    Select,
    Icon,
    FeatureSummary,
    StatusBadge,
    DataTable,
    DataTableColumn,
    DataTableRow,
    TablePagination,
    DetailDrawer,
    RouterLink,
    ViewSwitch,
    Disclosure,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: ':host { display: block; min-width: 0; }',
  styleUrl: './activity-audit-page.scss',
  templateUrl: './activity-audit-page.html',
})
export class ActivityAuditPage {
  readonly session = inject(WorkspaceSession);
  readonly categories = AUDIT_CATEGORIES;
  readonly category = signal('');
  readonly traceId = signal('');
  private readonly catalogRead = apiResource({
    feature: 'audit',
    loader: () => this.api.catalog(),
  });
  readonly features = computed<readonly FeatureDto[]>(
    () => this.catalogRead.value()?.features ?? [],
  );
  readonly models = computed<readonly ModelDto[]>(() => this.catalogRead.value()?.models ?? []);
  readonly modelNames = computed(() =>
    Object.fromEntries(this.models().map((model) => [model.id, formatModelName(model)])),
  );
  /** The filters last applied; every change starts again from the newest entries. */
  private readonly applied = signal<AuditFilters | null>(null);
  /** The oldest entry already read, when reading the next 100 for the same filters. */
  private readonly after = linkedSignal<AuditFilters | null, number | undefined>({
    source: this.applied,
    computation: () => undefined,
  });
  private readonly pageRead = apiResource({
    feature: 'audit',
    params: () => {
      const filters = this.applied();
      return filters ? { filters, after: this.after() } : undefined;
    },
    loader: async (request) => ({
      ...request,
      rows: await this.api.query(request.after, request.filters),
    }),
  });
  /** Pages read so far for the applied filters; each read past the last page adds one. */
  private readonly pages = linkedSignal<
    { page: AuditPage | undefined; filters: AuditFilters | null },
    AuditDto[][]
  >({
    source: () => ({ page: this.pageRead.value(), filters: this.applied() }),
    computation: ({ page, filters }, previous) => {
      if (!page || page.filters !== filters) return [];
      if (page.after === undefined) return [page.rows];
      if (!previous || previous.source.page === page) return previous?.value ?? [page.rows];
      return page.rows.length ? [...previous.value, page.rows] : previous.value;
    },
  });
  readonly pageIndex = linkedSignal(() => Math.max(0, this.pages().length - 1));
  readonly rows = computed<AuditDto[]>(() => this.pages()[this.pageIndex()] ?? []);
  readonly loadedCount = computed(() =>
    this.pages().reduce((count, page) => count + page.length, 0),
  );
  readonly hasNext = computed(() => this.pageIndex() < this.pages().length - 1 || this.more());
  readonly selectedId = signal<number | null>(null);
  readonly drawer = viewChild(DetailDrawer);
  private readonly table = viewChild(DataTable);
  readonly columns: TableColumn[] = [
    { id: 'time', label: '時間', hideable: false },
    { id: 'actor', label: '操作者' },
    { id: 'action', label: '動作', hideable: false },
    { id: 'resource', label: '資源' },
    { id: 'result', label: '結果' },
  ];
  private readonly typing = signal(false);
  readonly loading = computed(
    () => this.typing() || this.catalogRead.loading() || this.pageRead.refreshing(),
  );
  /** A full page means older entries may remain. */
  readonly more = computed(() => {
    const page = this.pageRead.value();
    return !!page && page.filters === this.applied() && page.rows.length === 100;
  });
  readonly error = computed(() => this.catalogRead.error() || this.pageRead.error());
  readonly search = signal('');
  readonly action = signal('');
  readonly result = signal('');
  readonly from = signal('');
  readonly until = signal('');
  private readonly dateFilters = viewChildren(DateTimePicker);
  readonly validDates = computed(() => this.dateFilters().every((picker) => picker.valid()));
  readonly date = formatDate;
  readonly presentedRows = computed(() =>
    this.rows().map((entry) => ({
      entry,
      date: formatDate(entry.at),
      action: auditAction(entry.action),
      category: AUDIT_CATEGORIES.find((item) => item.value === entry.category)?.label || '功能操作',
      context: auditContext(entry.detailsJson),
      logs: auditLogQuery(entry),
      resource: auditResource(entry.detailsJson),
      details: auditDetails(entry.detailsJson, this.modelNames()),
      changes: auditChanges(entry.detailsJson, this.modelNames()).map((change) => ({
        ...change,
        featureBefore: change.featureIds ? this.resolveFeatures(change.featureIds.before) : null,
        featureAfter: change.featureIds ? this.resolveFeatures(change.featureIds.after) : null,
      })),
      result: auditResult(
        entry.action === 'billing.price.created' ? 'created' : entry.result,
        entry.action,
      ),
      rejected: entry.action !== 'billing.price.created' && auditRejected(entry.result),
    })),
  );
  readonly actions = [
    { value: '', label: '所有動作' },
    { value: 'admin.', label: '平台管理' },
    { value: 'identity.login', label: '登入' },
    { value: 'identity.logout', label: '登出' },
    { value: 'logs.', label: '日誌查閱／匯出' },
    { value: 'admin.user', label: '使用者設定' },
    { value: 'identity.test_', label: '測試身分' },
    { value: 'admin.user_roles', label: '使用者角色' },
    { value: 'admin.user_model_policy', label: '個人模型政策' },
    { value: 'admin.user_storage', label: '使用者容量上限' },
    { value: 'admin.role', label: '角色授權' },
    { value: 'admin.group', label: '群組與模型政策' },
    { value: 'admin.feature', label: '功能異動' },
    { value: 'admin.conversation_read', label: '對話內容檢視' },
    { value: 'admin.user_usage_read', label: '使用者用量檢視' },
    { value: 'project.', label: '專案與範本' },
    { value: 'conversation.', label: '對話管理' },
    { value: 'artifact.', label: '成果文件' },
    { value: 'file.', label: '檔案庫' },
    { value: 'share.', label: '分享' },
    { value: 'repository.', label: '程式庫' },
    { value: 'web.search', label: '網路搜尋' },
    { value: 'knowledge.', label: '知識庫' },
    { value: 'document.', label: '文件與索引' },
    { value: 'evaluation.', label: '評測資源' },
    { value: 'quality.', label: '評測與回饋' },
    { value: 'run.', label: 'AI 生成' },
    { value: 'integration.', label: '外部資料查閱' },
    { value: 'resource.acl', label: '資源授權' },
  ];
  readonly selectedIndex = computed(() =>
    this.presentedRows().findIndex((row) => row.entry.id === this.selectedId()),
  );
  readonly selected = computed(() => this.presentedRows()[this.selectedIndex()] ?? null);
  openDetails(id: number) {
    this.selectedId.set(id);
    this.drawer()?.open();
  }
  adjacent(direction: -1 | 1) {
    const row = this.presentedRows()[this.selectedIndex() + direction];
    if (row) this.openDetails(row.entry.id);
  }
  private resetDetails() {
    this.drawer()?.close();
    this.selectedId.set(null);
    this.table()?.resetScroll();
  }
  changePage(direction: -1 | 1) {
    if (this.loading() || !this.validDates()) return;
    const index = this.pageIndex() + direction;
    if (index < 0) return;
    if (this.pages()[index]) {
      this.resetDetails();
      this.pageIndex.set(index);
    } else if (direction === 1 && this.more()) this.after.set(this.rows().at(-1)?.id);
  }
  private resolveFeatures(ids: string[]): FeatureDto[] {
    const catalog = new Map(this.features().map((feature) => [feature.id, feature]));
    return ids.map((id) => catalog.get(id) ?? { id, name: FEATURE_NAMES[id] || id, route: '' });
  }
  readonly results = [
    { value: '', label: '所有結果' },
    { value: 'success', label: '已受理／完成' },
    { value: 'saved', label: '已儲存' },
    { value: 'read', label: '已檢視' },
    { value: 'failed', label: '未完成／拒絕' },
  ];
  private readonly api = inject(ActivityAuditApi);
  private readonly route = inject(ActivatedRoute);
  private readonly destroy = inject(DestroyRef);
  private timer?: ReturnType<typeof setTimeout>;
  constructor() {
    // Date pickers validate the filters, so the link's filters are applied after the first render.
    afterNextRender(() =>
      this.route.queryParamMap.pipe(takeUntilDestroyed(this.destroy)).subscribe((params) => {
        clearTimeout(this.timer);
        const category = params.get('category') || '';
        this.category.set(AUDIT_CATEGORIES.some((item) => item.value === category) ? category : '');
        this.search.set((params.get('search') || '').slice(0, 120));
        const traceId = params.get('traceId') || '';
        this.traceId.set(/^[a-f\d]{32}$/i.test(traceId) ? traceId.toLowerCase() : '');
        this.action.set(
          this.actions.some((item) => item.value === params.get('action'))
            ? params.get('action')!
            : '',
        );
        this.result.set(
          this.results.some((item) => item.value === params.get('result'))
            ? params.get('result')!
            : '',
        );
        this.from.set(params.get('from') || '');
        this.until.set(params.get('until') || '');
        this.load();
      }),
    );
    // A new set of pages starts with the drawer closed and the table at the top.
    effect(() => {
      this.pages();
      untracked(() => this.resetDetails());
    });
    this.destroy.onDestroy(() => clearTimeout(this.timer));
  }
  reload() {
    this.catalogRead.reload();
    this.pageRead.reload();
  }
  searchChanged(value: string) {
    this.search.set(value);
    this.typing.set(true);
    clearTimeout(this.timer);
    this.timer = setTimeout(() => this.load(), 250);
  }
  filter(key: 'category' | 'action' | 'result' | 'from' | 'until', value: string) {
    this[key].set(value);
    clearTimeout(this.timer);
    this.load();
  }
  /** Apply the current filters and read the newest entries again. */
  load() {
    this.typing.set(false);
    if (!this.validDates()) return;
    this.applied.set({
      search: this.search() || undefined,
      action: this.action() || undefined,
      result: this.result() || undefined,
      category: this.category() || undefined,
      traceId: this.traceId() || undefined,
      from: this.from() ? this.from() + 'T00:00:00+08:00' : undefined,
      until: this.until() ? this.nextDay(this.until()) + 'T00:00:00+08:00' : undefined,
    });
  }
  export() {
    const rows = this.pages().flat();
    downloadBlob(
      new Blob(
        [
          toCsv([
            [
              '紀錄 ID',
              '時間（台北）',
              '操作者',
              '測試身分',
              '動作',
              '資源 ID',
              '資源識別碼',
              '結果',
              '異動資訊',
              '分類',
              'Trace ID',
              'Operation ID',
              '查證代碼',
            ],
            ...rows.map((row) => [
              row.id,
              this.date(row.at),
              row.actor,
              row.actingAs || '',
              row.action,
              row.resourceId,
              auditResource(row.detailsJson),
              auditResult(
                row.action === 'billing.price.created' ? 'created' : row.result,
                row.action,
              ),
              auditDetails(row.detailsJson, this.modelNames()),
              AUDIT_CATEGORIES.find((item) => item.value === row.category)?.label || '功能操作',
              row.traceId || '',
              row.operationId || '',
              row.issueCode || '',
            ]),
          ]),
        ],
        { type: 'text/csv;charset=utf-8' },
      ),
      `AI-Nexus-稽核-${new Date().toISOString().slice(0, 10)}-${rows.length}筆`,
      'csv',
    );
  }
  private nextDay(day: string) {
    const date = new Date(day + 'T00:00:00Z');
    date.setUTCDate(date.getUTCDate() + 1);
    return date.toISOString().slice(0, 10);
  }
}
