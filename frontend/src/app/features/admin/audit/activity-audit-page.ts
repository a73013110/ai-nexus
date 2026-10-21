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
import { apiResource } from '@core/api/api-resource';
import type { AuditDto, FeatureDto } from '@core/api/schema';
import { catalogFeature, routeOf } from '@core/api/catalog';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { safeMessage } from '@core/errors/safe-errors';
import { FeaturePage } from '@core/layout/feature-page';
import { FeatureSummary } from '@features/admin/access-control/roles/feature-summary';
import { downloadBlob } from '@shared/browser/download';
import { formatDate, formatModelName } from '@shared/browser/format';
import { ViewScope } from '@shared/browser/view-scope';
import { CopyValue } from '@shared/ui/copy-value';
import {
  CursorTrail,
  DataTable,
  DataTableColumn,
  DataTableRow,
  PAGE_SIZES,
  TablePagination,
  TableState,
  type TableColumn,
} from '@shared/ui/data-table';
import { DetailDrawer } from '@shared/ui/detail-drawer';
import { DrawerPager } from '@shared/ui/drawer-pager';
import { Field } from '@shared/ui/field';
import { Icon } from '@shared/ui/icon';
import { Notice } from '@shared/ui/notice';
import { QueryBar } from '@shared/ui/query-bar';
import { queryState } from '@shared/ui/query-state';
import { Select, type SelectOption } from '@shared/ui/select';
import { StatusBadge } from '@shared/ui/status-badge';
import { TimeRangePicker } from '@shared/ui/time-range-picker';
import { ViewSwitch } from '@shared/ui/view-switch';
import { AuditApi, type AuditFilters } from './audit-api';
import { auditLogQuery } from './audit-navigation';
import { activityAuditTour, auditDetailTour } from './audit-tour';
import {
  auditAction,
  auditChanges,
  auditContext,
  auditDetails,
  auditOutcome,
  auditResource,
} from './audit-presentation';
import {
  AUDIT_RANGES,
  AUDIT_RESULTS,
  actionsFor,
  auditFilters,
  auditQueryOptions,
} from './audit-query';

interface AuditPageRequest {
  filters: AuditFilters;
  take: number;
  /** 游標：上一頁最後一筆的 ID，只讀比它舊的紀錄；第一頁為 undefined。 */
  before: number | undefined;
  index: number;
}

@Component({
  selector: 'nx-activity-audit-page',
  imports: [
    CopyValue,
    DataTable,
    DataTableColumn,
    DataTableRow,
    DetailDrawer,
    DrawerPager,
    FeaturePage,
    FeatureSummary,
    Field,
    Icon,
    Notice,
    QueryBar,
    RouterLink,
    Select,
    StatusBadge,
    TablePagination,
    TableState,
    TimeRangePicker,
    ViewSwitch,
  ],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: ':host { display: block; min-width: 0; }',
  styleUrl: './activity-audit-page.scss',
  templateUrl: './activity-audit-page.html',
})
export class ActivityAuditPage {
  /** 範本裡的連結由功能目錄取得網址。 */
  readonly routeOf = routeOf;
  readonly session = inject(WorkspaceSession);
  private readonly api = inject(AuditApi);
  private readonly view = inject(ViewScope);
  private readonly catalogRead = apiResource({
    feature: 'audit',
    loader: () => this.api.catalog(),
  });
  private readonly catalog = this.catalogRead.value;
  private readonly features = computed(
    () => new Map((this.catalog()?.features ?? []).map((feature) => [feature.id, feature])),
  );
  readonly navigation = computed(() => this.catalog()?.navigationSections ?? []);
  private readonly modelNames = computed(() =>
    Object.fromEntries(
      (this.catalog()?.models ?? []).map((model) => [model.id, formatModelName(model)]),
    ),
  );
  private readonly actions = computed(() => this.catalog()?.actions ?? []);
  readonly categories = computed<SelectOption[]>(() => [
    { value: '', label: '全部活動' },
    ...(this.catalog()?.categories ?? []),
  ]);
  readonly query = queryState(auditQueryOptions(this.actions));
  readonly draft = this.query.draft;
  readonly actionOptions = computed<SelectOption[]>(() => [
    { value: '', label: '所有動作' },
    ...actionsFor(this.actions(), this.draft().category).map(({ value, label }) => ({
      value,
      label,
    })),
  ]);
  readonly resultOptions: SelectOption[] = AUDIT_RESULTS;
  readonly ranges = AUDIT_RANGES;
  readonly pageSize = signal(50);
  private readonly request = signal<AuditPageRequest | undefined>(undefined);
  private readonly pageRead = apiResource({
    feature: 'audit',
    params: this.request,
    loader: async (request, signal) => ({
      request,
      rows: await this.api.query(request.filters, request.take, request.before, signal),
    }),
  });
  readonly rows = computed<AuditDto[]>(() => this.pageRead.value()?.rows ?? []);
  readonly pageIndex = computed(() => this.pageRead.value()?.request.index ?? 0);
  /** 游標分頁不算總數：本頁筆數等於每頁筆數，才表示可能還有更舊的紀錄。 */
  readonly hasNext = computed(() => {
    const page = this.pageRead.value();
    return !!page && page.rows.length === page.request.take;
  });
  readonly loading = computed(() => this.catalogRead.loading() || this.pageRead.refreshing());
  readonly readError = computed(() => this.pageRead.error());
  readonly catalogError = this.catalogRead.error;
  readonly exporting = signal(false);
  readonly exportError = signal('');
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
  readonly presentedRows = computed(() =>
    this.rows().map((entry) => ({
      entry,
      ...auditOutcome(entry),
      date: formatDate(entry.at),
      action: auditAction(entry.action),
      category:
        this.categories().find((item) => item.value === entry.category)?.label || '功能操作',
      context: auditContext(entry.detailsJson),
      logs: auditLogQuery(entry),
      resource: auditResource(entry.detailsJson),
      details: auditDetails(entry.detailsJson, this.modelNames()),
      changes: auditChanges(entry.detailsJson, this.modelNames()).map((change) => ({
        ...change,
        featureBefore: change.featureIds ? this.resolveFeatures(change.featureIds.before) : null,
        featureAfter: change.featureIds ? this.resolveFeatures(change.featureIds.after) : null,
      })),
    })),
  );
  readonly selectedIndex = computed(() =>
    this.presentedRows().findIndex((row) => row.entry.id === this.selectedId()),
  );
  readonly selected = computed(() => this.presentedRows()[this.selectedIndex()] ?? null);
  private readonly cursors = new CursorTrail<number | undefined>(undefined);
  private readonly logsName = computed(() =>
    this.session.has('diagnostics.logs') ? this.session.featureName('diagnostics.logs') : null,
  );
  /** 沒有 `audit` 時頁面只剩標題，不給導覽。 */
  readonly tour = computed(() =>
    this.session.has('audit') ? activityAuditTour({ logs: this.logsName() }) : null,
  );
  /** 日誌連結的條件與範本相同：有授權且這筆紀錄能帶入 Trace 或查證代碼。 */
  readonly drawerTour = computed(() => {
    const row = this.selected();
    if (!row) return null;
    const { traceId, operationId, issueCode } = row.entry;
    return auditDetailTour({
      changes: row.changes.length > 0,
      flow: !!(traceId || operationId || issueCode),
      logs: row.logs ? this.logsName() : null,
    });
  });

  constructor() {
    // 每次套用條件都從最新一頁重讀；相對時間（最近 24 小時）以套用當下計算。
    effect(() => {
      const query = this.query.applied();
      untracked(() => {
        const filters = auditFilters(query);
        if (typeof filters !== 'string') this.read(filters, undefined, 0);
      });
    });
    effect(() => {
      if (this.pageRead.value()) this.table()?.resetScroll();
    });
  }
  /** 分類切換立即套用；已選的動作不屬於新分類時一併清除，避免查出空結果。 */
  changeCategory(category: string) {
    const action = this.draft().action;
    const keep =
      !action || actionsFor(this.actions(), category).some((item) => item.value === action);
    this.query.commit({ category, ...(keep ? {} : { action: '' }) });
  }
  changePage(direction: -1 | 1) {
    const index = this.pageIndex() + direction;
    if (index < 0 || (direction === 1 && !this.hasNext())) return;
    this.goTo(index, direction === 1 ? this.rows().at(-1)?.id : this.cursors.at(index));
  }
  firstPage() {
    this.goTo(0, undefined);
  }
  private goTo(index: number, before: number | undefined) {
    const request = this.request();
    if (this.loading() || !request) return;
    this.read(request.filters, before, index);
  }
  changePageSize(size: number) {
    const request = this.request();
    if (this.loading() || !request || !PAGE_SIZES.includes(size)) return;
    this.pageSize.set(size);
    this.read(request.filters, undefined, 0);
  }
  private read(filters: AuditFilters, before: number | undefined, index: number) {
    this.cursors.visit(index, before);
    this.drawer()?.close();
    this.selectedId.set(null);
    this.exportError.set('');
    this.request.set({ filters, take: this.pageSize(), before, index });
  }
  retry() {
    if (this.catalogError()) this.catalogRead.reload();
    this.pageRead.reload();
  }
  openDetails(id: number) {
    this.selectedId.set(id);
    this.drawer()?.open();
  }
  adjacent(direction: -1 | 1) {
    const row = this.presentedRows()[this.selectedIndex() + direction];
    if (row) this.openDetails(row.entry.id);
  }
  async export() {
    if (!this.request() || this.exporting()) return;
    // 預設區間（最近 N 天）以按下匯出的當下重算，才與標籤相符；畫面上的結果可能是較早套用的。
    const filters = auditFilters(this.query.applied());
    if (typeof filters === 'string') {
      this.exportError.set(filters);
      return;
    }
    const guard = this.view.guard();
    this.exporting.set(true);
    this.exportError.set('');
    try {
      const response = await this.api.export(filters);
      const blob = await response.blob();
      if (guard()) downloadBlob(blob, 'ai-nexus-audit', 'csv');
    } catch (error) {
      if (guard()) this.exportError.set(safeMessage(error));
    } finally {
      if (guard()) this.exporting.set(false);
    }
  }
  private resolveFeatures(ids: string[]): FeatureDto[] {
    const features = this.features();
    return ids.map(
      (id) =>
        features.get(id) ?? {
          id,
          // 此帳號沒有授權的功能不在 `/me`，名稱與圖示取自目錄；不認得的 ID 原樣顯示。
          name: catalogFeature(id)?.name || id,
          route: catalogFeature(id)?.route ?? '',
          icon: catalogFeature(id)?.icon ?? '',
        },
    );
  }
}
