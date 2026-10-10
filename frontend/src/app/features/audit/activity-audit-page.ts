import { Notice } from '../../shared/ui/notice';
import { DateTimePicker } from '../../shared/ui/date-time-picker';
import { EmptyState } from '../../shared/ui/empty-state';
import { FilterPanel } from '../../shared/ui/filter-panel';
import { safeMessage } from '../../core/api/safe-errors';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { WorkspaceSession } from '../../core/auth/workspace-session';
import { FEATURE_NAMES } from '../../core/feature-names';
import { FeaturePage } from '../../shared/ui/feature-page';
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
} from '@angular/core';
import { ActivityAuditApi } from './activity-audit-api';
import type { AuditDto, FeatureDto, ModelDto } from '../../core/api/schema';
import { FeatureSummary } from '../../shared/ui/feature-summary';
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
  readonly features = signal<readonly FeatureDto[]>([]);
  readonly models = signal<readonly ModelDto[]>([]);
  readonly modelNames = computed(() =>
    Object.fromEntries(this.models().map((model) => [model.id, formatModelName(model)])),
  );
  readonly rows = signal<AuditDto[]>([]);
  readonly pageIndex = signal(0);
  readonly loadedCount = signal(0);
  private readonly pages = signal<AuditDto[][]>([]);
  readonly hasNext = computed(() => this.pageIndex() < this.pages().length - 1 || this.more());
  readonly selectedId = signal<number | null>(null);
  readonly drawer = viewChild.required(DetailDrawer);
  private readonly table = viewChild(DataTable);
  readonly columns: TableColumn[] = [
    { id: 'time', label: '時間', hideable: false },
    { id: 'actor', label: '操作者' },
    { id: 'action', label: '動作', hideable: false },
    { id: 'resource', label: '資源' },
    { id: 'result', label: '結果' },
  ];
  readonly loading = signal(true);
  readonly more = signal(false);
  readonly error = signal('');
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
    this.drawer().open();
  }
  adjacent(direction: -1 | 1) {
    const row = this.presentedRows()[this.selectedIndex() + direction];
    if (row) this.openDetails(row.entry.id);
  }
  private resetDetails() {
    this.drawer().close();
    this.selectedId.set(null);
    this.table()?.resetScroll();
  }
  async changePage(direction: -1 | 1) {
    if (this.loading() || !this.validDates()) return;
    const index = this.pageIndex() + direction;
    if (index < 0) return;
    const page = this.pages()[index];
    if (page) {
      this.resetDetails();
      this.pageIndex.set(index);
      this.rows.set(page);
    } else if (direction === 1 && this.more()) await this.load(true);
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
  private catalogLoaded = false;
  private version = 0;
  private timer?: ReturnType<typeof setTimeout>;
  constructor() {
    afterNextRender(() => void this.initialize());
    this.destroy.onDestroy(() => {
      ++this.version;
      clearTimeout(this.timer);
    });
  }
  private async initialize() {
    try {
      await this.session.load();
      if (this.destroy.destroyed) return;
      if (!this.session.has('audit')) {
        this.loading.set(false);
        return;
      }
      const catalog = await this.api.catalog();
      if (this.destroy.destroyed) return;
      this.features.set(catalog.features);
      this.models.set(catalog.models);
      this.catalogLoaded = true;
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
        void this.load();
      });
    } catch (error) {
      if (!this.destroy.destroyed) {
        this.error.set(safeMessage(error));
        this.loading.set(false);
      }
    }
  }
  reload() {
    if (this.catalogLoaded) void this.load();
    else void this.initialize();
  }
  searchChanged(value: string) {
    this.search.set(value);
    ++this.version;
    this.loading.set(true);
    clearTimeout(this.timer);
    this.timer = setTimeout(() => void this.load(), 250);
  }
  filter(key: 'category' | 'action' | 'result' | 'from' | 'until', value: string) {
    this[key].set(value);
    clearTimeout(this.timer);
    void this.load();
  }
  async load(append = false) {
    if (!this.session.has('audit') || this.destroy.destroyed) return;
    if (append && this.loading()) return;
    const version = ++this.version;
    if (!this.validDates()) {
      this.loading.set(false);
      return;
    }
    this.loading.set(true);
    this.error.set('');
    if (!append) {
      this.resetDetails();
      this.pages.set([]);
      this.pageIndex.set(0);
      this.loadedCount.set(0);
      this.rows.set([]);
      this.more.set(false);
    }
    try {
      const filters = {
        search: this.search() || undefined,
        action: this.action() || undefined,
        result: this.result() || undefined,
        category: this.category() || undefined,
        traceId: this.traceId() || undefined,
        from: this.from() ? this.from() + 'T00:00:00+08:00' : undefined,
        until: this.until() ? this.nextDay(this.until()) + 'T00:00:00+08:00' : undefined,
      };
      const rows = await this.api.query(append ? this.rows().at(-1)?.id : undefined, filters);
      if (version !== this.version) return;
      this.more.set(rows.length === 100);
      if (append && !rows.length) return;
      if (append) {
        this.resetDetails();
        this.pages.update((pages) => [...pages, rows]);
        this.pageIndex.set(this.pages().length - 1);
        this.loadedCount.update((count) => count + rows.length);
      } else {
        this.pages.set([rows]);
        this.loadedCount.set(rows.length);
      }
      this.rows.set(rows);
    } catch (error) {
      if (version === this.version) this.error.set(safeMessage(error));
    } finally {
      if (version === this.version) this.loading.set(false);
    }
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
