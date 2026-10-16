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
import type { AuditDto, FeatureDto, ModelDto, NavigationGroupDto } from '@core/api/schema';
import { FEATURE_NAMES } from '@core/auth/feature-names';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { safeMessage } from '@core/errors/safe-errors';
import { FeatureAccessNotice } from '@core/layout/feature-access-notice';
import { FeaturePage } from '@core/layout/feature-page';
import { FeatureSummary } from '@features/administration/roles/feature-summary';
import { downloadBlob } from '@shared/browser/download';
import { formatDate, formatModelName } from '@shared/browser/format';
import { ViewScope } from '@shared/browser/view-scope';
import { CopyValue } from '@shared/ui/copy-value';
import {
  DataTable,
  DataTableColumn,
  DataTableRow,
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
import {
  auditAction,
  auditChanges,
  auditContext,
  auditDetails,
  auditRejected,
  auditResource,
  auditResult,
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
  /** The last ID of the previous page; none for the first. */
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
    FeatureAccessNotice,
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
  readonly session = inject(WorkspaceSession);
  private readonly api = inject(AuditApi);
  private readonly view = inject(ViewScope);
  private readonly catalogRead = apiResource({
    feature: 'audit',
    loader: () => this.api.catalog(),
  });
  private readonly catalog = computed(() => this.catalogRead.value());
  readonly features = computed<readonly FeatureDto[]>(() => this.catalog()?.features ?? []);
  /** Every menu group, so recorded feature changes read in menu order under their group. */
  readonly navigationGroups = computed<readonly NavigationGroupDto[]>(
    () => this.catalog()?.navigationGroups ?? [],
  );
  readonly models = computed<readonly ModelDto[]>(() => this.catalog()?.models ?? []);
  readonly modelNames = computed(() =>
    Object.fromEntries(this.models().map((model) => [model.id, formatModelName(model)])),
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
  /** A full page means older entries may remain. */
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
      result: auditResult(
        entry.action === 'billing.price.created' ? 'created' : entry.result,
        entry.action,
      ),
      rejected: entry.action !== 'billing.price.created' && auditRejected(entry.result),
    })),
  );
  readonly selectedIndex = computed(() =>
    this.presentedRows().findIndex((row) => row.entry.id === this.selectedId()),
  );
  readonly selected = computed(() => this.presentedRows()[this.selectedIndex()] ?? null);
  /** The `before` ID that reads each page already visited, for going back. */
  private cursors: (number | undefined)[] = [undefined];

  constructor() {
    // Each applied query reads its newest page, with the time bounds of that moment.
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
  /** The category switch applies at once and drops an action outside the category. */
  changeCategory(category: string) {
    const action = this.draft().action;
    const keep = !action || actionsFor(this.actions(), category).some((x) => x.value === action);
    this.query.commit({ category, ...(keep ? {} : { action: '' }) });
  }
  changePage(direction: -1 | 1) {
    const index = this.pageIndex() + direction;
    if (index < 0 || (direction === 1 && !this.hasNext())) return;
    this.goTo(index, direction === 1 ? this.rows().at(-1)?.id : this.cursors[index]);
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
    if (this.loading() || !request || ![25, 50, 100].includes(size)) return;
    this.pageSize.set(size);
    this.read(request.filters, undefined, 0);
  }
  private read(filters: AuditFilters, before: number | undefined, index: number) {
    if (index === 0) this.cursors = [undefined];
    this.cursors[index] = before;
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
    const filters = this.request()?.filters;
    if (!filters || this.exporting()) return;
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
    const catalog = new Map(this.features().map((feature) => [feature.id, feature]));
    return ids.map(
      (id) =>
        catalog.get(id) ?? {
          id,
          name: FEATURE_NAMES[id] || id,
          route: '',
          groupId: null,
          icon: '',
          sortOrder: 0,
        },
    );
  }
}
