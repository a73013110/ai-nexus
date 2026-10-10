import { Notice } from '../../shared/ui/notice';
import { Card } from '../../shared/ui/card';
import { StatusBadge } from '../../shared/ui/status-badge';
import { DataTable } from '../../shared/ui/data-table';
import { DateTimePicker } from '../../shared/ui/date-time-picker';
import { FilterPanel } from '../../shared/ui/filter-panel';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  linkedSignal,
  signal,
} from '@angular/core';
import { apiResource } from '../../core/api/api-resource';
import { RouterLink, ActivatedRoute } from '@angular/router';
import type { DashboardDto, SpendBucketDto } from '../../core/api/schema';
import { WorkspaceSession } from '../../core/auth/workspace-session';
import { ViewScope } from '../../shared/browser/view-scope';
import { FeaturePage } from '../../core/layout/feature-page';
import { Icon } from '../../shared/ui/icon';
import { Select } from '../../shared/ui/select';
import { TokenUsageChart } from '../billing/token-usage-chart';
import { TrendChart } from '../../shared/ui/trend-chart';
import { BillingApi, chargeKind, datePeriod, localDate, money } from '../billing/billing-api';
import { PriceBook } from '../billing/price-book';
import { WorkspaceFlow } from './workspace-flow';
import { downloadBlob } from '../../shared/browser/download';

@Component({
  selector: 'nx-dashboard-page',
  imports: [
    Notice,
    Card,
    StatusBadge,
    DataTable,
    DateTimePicker,
    FilterPanel,
    FeaturePage,
    Icon,
    Select,
    TrendChart,
    TokenUsageChart,
    RouterLink,
    PriceBook,
    WorkspaceFlow,
  ],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './dashboard-page.html',
  styleUrl: './dashboard-page.scss',
})
export class DashboardPage {
  readonly session = inject(WorkspaceSession);
  private readonly api = inject(BillingApi);
  private readonly view = inject(ViewScope);
  private readonly route = inject(ActivatedRoute);
  private readonly requestedScope = signal(
    this.route.snapshot.queryParamMap.get('scope') === 'platform' ? 'platform' : 'personal',
  );
  /** Only administrators can see the whole platform; others always see their own work. */
  readonly scope = computed(() =>
    this.requestedScope() === 'platform' && this.session.has('admin') ? 'platform' : 'personal',
  );
  readonly owner = signal('');
  readonly today = localDate(new Date());
  readonly from = signal(localDate(new Date(Date.now() - 29 * 86400000)));
  readonly through = signal(this.today);
  readonly rangeOptions = [
    { value: '7', label: '最近 7 天' },
    { value: '30', label: '最近 30 天' },
    { value: '90', label: '最近 90 天' },
    { value: 'custom', label: '自訂期間' },
  ];
  readonly range = signal('30');
  /** The period and owner the form last submitted; the read follows it and the scope. */
  private readonly query = signal({ from: this.from(), through: this.through(), owner: '' });
  private readonly dashboard = apiResource({
    feature: 'dashboard',
    params: () => ({ ...this.query(), scope: this.scope() }),
    loader: async (query) => ({
      query,
      data: await this.api.dashboard(
        datePeriod(query.from, query.through),
        query.scope,
        query.owner,
      ),
    }),
  });
  /** Figures from another scope are never shown under this scope's label. */
  readonly data = computed<DashboardDto | null>(() => {
    const value = this.dashboard.value();
    return value?.query.scope === this.scope() ? value.data : null;
  });
  readonly loading = this.dashboard.refreshing;
  readonly formError = signal('');
  readonly exportError = signal('');
  readonly error = computed(() => this.formError() || this.dashboard.error() || this.exportError());
  readonly exporting = signal(false);
  readonly appliedFrom = computed(() => this.dashboard.value()?.query.from ?? this.from());
  readonly appliedThrough = computed(() => this.dashboard.value()?.query.through ?? this.through());
  readonly appliedOwner = computed(() => this.dashboard.value()?.query.owner ?? '');
  readonly scopeLabel = computed(() =>
    this.scope() === 'personal'
      ? '我的工作'
      : this.appliedOwner()
        ? '使用者：' +
          (this.data()?.spend.users.find((x) => x.ownerId === this.appliedOwner())?.displayName ??
            '指定使用者')
        : '整個平台',
  );
  readonly money = money;
  readonly kind = chargeKind;
  readonly units = computed(() =>
    (this.data()?.spend.totals ?? []).map((x) => ({
      value: x.currency + ':' + x.kind,
      label: x.currency ? x.currency + ' · ' + chargeKind(x.kind) : '未設定價格',
    })),
  );
  readonly tokenTotals = computed(
    () =>
      this.data()?.tokens?.daily.reduce(
        (sum, row) => ({
          input: sum.input + row.inputTokens,
          output: sum.output + row.outputTokens,
        }),
        { input: 0, output: 0 },
      ) ?? {
        input: this.data()?.spend.inputTokens || 0,
        output: this.data()?.spend.outputTokens || 0,
      },
  );
  /** Keep the chosen currency while it still has spending; otherwise show the first one. */
  readonly unit = linkedSignal<{ value: string }[], string>({
    source: this.units,
    computation: (options, previous) =>
      previous && options.some((x) => x.value === previous.value)
        ? previous.value
        : (options[0]?.value ?? ''),
  });
  readonly total = computed(
    () => this.data()?.spend.totals.find((x) => x.currency + ':' + x.kind === this.unit()) ?? null,
  );
  readonly unknown = computed(
    () =>
      (this.data()?.spend.totals.reduce((a, x) => a + x.unknownCalls, 0) ?? 0) +
      (this.data()?.spend.legacyCalls ?? 0),
  );
  readonly trend = computed(() =>
    this.days(
      this.data()?.spend.daily.filter((x) => x.currency + ':' + x.kind === this.unit()) ?? [],
      'amount',
    ),
  );
  readonly modelRows = computed(() =>
    (this.data()?.spend.models ?? [])
      .filter((x) => x.currency + ':' + x.kind === this.unit())
      .sort((a, b) => b.amount - a.amount),
  );
  readonly users = computed(() =>
    (this.data()?.spend.users ?? []).filter((x) => x.currency + ':' + x.kind === this.unit()),
  );
  load() {
    this.exportError.set('');
    if (!this.from() || !this.through() || this.from() > this.through()) {
      this.formError.set('請提供有效的開始與結束日期。');
      return;
    }
    this.formError.set('');
    const query = { from: this.from(), through: this.through(), owner: this.owner() };
    const current = this.query();
    if (
      query.from === current.from &&
      query.through === current.through &&
      query.owner === current.owner
    )
      this.dashboard.reload();
    else this.query.set(query);
  }
  setRange(value: string) {
    this.range.set(value);
    if (value === 'custom') return;
    this.through.set(this.today);
    this.from.set(localDate(new Date(Date.now() - (Number(value) - 1) * 86400000)));
    this.load();
  }
  setScope(value: string) {
    this.requestedScope.set(value);
    this.owner.set('');
    this.load();
  }
  async export() {
    if (this.exporting() || !this.data()) return;
    const valid = this.view.guard();
    this.exporting.set(true);
    try {
      const response = await this.api.export(
        datePeriod(this.appliedFrom(), this.appliedThrough()),
        this.appliedOwner(),
      );
      const blob = await response.blob();
      if (valid()) downloadBlob(blob, 'ai-nexus-spend', 'csv');
    } catch (e) {
      if (valid()) this.exportError.set(this.view.message(e));
    } finally {
      if (valid()) this.exporting.set(false);
    }
  }
  private days(rows: SpendBucketDto[], key: 'amount' | 'requests') {
    const values = new Map<string, number>();
    rows.forEach((x) => values.set(x.label, (values.get(x.label) ?? 0) + x[key]));
    const result = [];
    const day = new Date(this.appliedFrom() + 'T00:00:00'),
      end = new Date(this.appliedThrough() + 'T00:00:00');
    for (let i = 0; day <= end && i < 366; i++, day.setDate(day.getDate() + 1)) {
      const label = localDate(day);
      result.push({ label, value: values.get(label) ?? 0 });
    }
    return result;
  }
}
