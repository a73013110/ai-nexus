import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { apiResource } from '@core/api/api-resource';
import type { FeatureDto } from '@core/api/schema';
import { catalogFeature, routeOf } from '@core/api/catalog';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { safeMessage } from '@core/errors/safe-errors';
import { FeaturePage } from '@core/layout/feature-page';
import { FeatureSummary } from '@features/admin/access-control/roles/feature-summary';
import { downloadBlob } from '@shared/browser/download';
import { formatDate, formatModelName } from '@shared/browser/format';
import { ViewScope } from '@shared/browser/view-scope';
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
import { Select, type SelectOption } from '@shared/ui/select';
import { StatusBadge } from '@shared/ui/status-badge';
import { TableHeader } from '@shared/ui/table-header';
import { cursorTable } from '@shared/ui/table-model';
import { TimeRangePicker } from '@shared/ui/time-range-picker';
import { ViewSwitch } from '@shared/ui/view-switch';
import { AuditApi } from './audit-api';
import { auditLogQuery } from './audit-navigation';
import { activityAuditTour, auditDetailTour } from './audit-tour';
import {
  auditChanges,
  auditContext,
  auditDetails,
  auditOutcome,
  auditResource,
} from './audit-presentation';
import {
  AUDIT_RANGES,
  AUDIT_RESULTS,
  actionLabel,
  actionsFor,
  auditFilters,
  auditQueryOptions,
} from './audit-query';

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
    PendingButton,
    QueryBar,
    RouterLink,
    Select,
    StatusBadge,
    TableHeader,
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
  private readonly modules = computed(() => this.catalog()?.modules ?? []);
  readonly categories = computed<SelectOption[]>(() => [
    { value: '', label: '全部活動' },
    ...(this.catalog()?.categories ?? []),
  ]);
  readonly query = queryState(auditQueryOptions(this.actions, this.modules));
  readonly draft = this.query.draft;
  /** 每個模組先給「模組（全部）」（前綴篩選），再列該模組在目前分類下的動作。 */
  readonly actionOptions = computed<SelectOption[]>(() => {
    const actions = actionsFor(this.actions(), this.draft().category);
    return [
      { value: '', label: '所有動作' },
      ...this.modules().flatMap((module) => {
        const own = actions.filter((action) => action.module === module.value);
        return own.length
          ? [
              { value: module.value + '.', label: module.label + '（全部）' },
              ...own.map(({ value, label }) => ({ value, label })),
            ]
          : [];
      }),
    ];
  });
  readonly resultOptions: SelectOption[] = AUDIT_RESULTS;
  readonly ranges = AUDIT_RANGES;
  /** 已套用的條件；相對時間（最近 24 小時）以套用當下計算，條件不合法時不讀。 */
  private readonly filters = computed(() => {
    const filters = auditFilters(this.query.applied());
    return typeof filters === 'string' ? undefined : filters;
  });
  /** 游標是上一頁最後一筆的 ID，只讀比它舊的紀錄；不算總數。 */
  readonly table = cursorTable({
    feature: 'audit',
    params: this.filters,
    load: async (filters, table, signal) => {
      const before = typeof table.cursor === 'number' ? table.cursor : undefined;
      const items = await this.api.query(filters, table.limit, before, signal);
      return { items, next: items.length === table.limit ? items.at(-1)?.id : null };
    },
    columns: [
      { id: 'time', label: '時間', hideable: false },
      { id: 'actor', label: '操作者' },
      { id: 'action', label: '動作', hideable: false },
      { id: 'feature', label: '功能' },
      { id: 'subject', label: '資料主體' },
      { id: 'resource', label: '資源', defaultHidden: true },
      { id: 'result', label: '結果' },
    ],
    navigated: () => {
      this.drawer()?.close();
      this.selectedId.set(null);
      this.exportError.set('');
    },
  });
  readonly rows = this.table.rows;
  readonly loading = computed(() => this.catalogRead.loading() || this.table.refreshing());
  readonly catalogError = this.catalogRead.error;
  /** 動作選項來自目錄：讀到前選單顯示「正在載入」，不先只給「所有動作」。 */
  readonly catalogLoading = this.catalogRead.loading;
  readonly catalogRefreshing = this.catalogRead.refreshing;
  readonly exporting = signal(false);
  readonly exportError = signal('');
  readonly selectedId = signal<number | null>(null);
  readonly drawer = viewChild(DetailDrawer);
  readonly presentedRows = computed(() =>
    this.rows().map((entry) => ({
      entry,
      ...auditOutcome(entry.result),
      date: formatDate(entry.at),
      action: actionLabel(entry.action, this.actions(), this.modules()),
      // 目錄還沒讀到時先留白，不先顯示原始代碼。
      category: this.catalog()
        ? this.categories().find((item) => item.value === entry.category)?.label || entry.category
        : '',
      feature: entry.feature ? (catalogFeature(entry.feature)?.name ?? entry.feature) : '',
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

  /** 分類切換立即套用；已選的動作（或模組）在新分類下沒有動作時一併清除，避免查出空結果。 */
  changeCategory(category: string) {
    const action = this.draft().action;
    const keep =
      !action ||
      actionsFor(this.actions(), category).some((item) =>
        action.endsWith('.') ? item.value.startsWith(action) : item.value === action,
      );
    this.query.commit({ category, ...(keep ? {} : { action: '' }) });
  }
  /** 表格讀取失敗後重試（表格自己重讀）時，動作目錄也失敗就一起重讀。 */
  retryCatalog() {
    if (this.catalogError()) this.catalogRead.reload();
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
    if (!this.table.data() || this.exporting()) return;
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
