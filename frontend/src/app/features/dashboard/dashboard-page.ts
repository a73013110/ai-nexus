import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink, ActivatedRoute } from '@angular/router';
import type { Dashboard, SpendBucket } from '../../core/api/types';
import { WorkspaceSession } from '../../core/auth/workspace-session';
import { ViewScope } from '../../shared/browser/view-scope';
import { FeaturePage } from '../../shared/ui/feature-page';
import { Icon } from '../../shared/ui/icon';
import { Select } from '../../shared/ui/select';
import { TokenUsageChart } from '../../shared/ui/token-usage-chart';
import { TrendChart } from '../../shared/ui/trend-chart';
import { BillingApi, chargeKind, datePeriod, localDate, money } from '../billing/billing-api';
import { PriceBook } from '../billing/price-book';
import { WorkspaceFlow } from './workspace-flow';
import { downloadBlob } from '../../shared/browser/download';

@Component({
  selector: 'nx-dashboard-page',
  imports: [
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
  readonly data = signal<Dashboard | null>(null);
  readonly loading = signal(true);
  readonly error = signal('');
  readonly exporting = signal(false);
  readonly scope = signal('personal');
  readonly owner = signal('');
  readonly unit = signal('');
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
  readonly appliedFrom = signal(this.from());
  readonly appliedThrough = signal(this.through());
  readonly appliedOwner = signal('');
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
  private sequence = 0;
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
  constructor() {
    void this.initialize();
  }
  private async initialize() {
    const valid = this.view.guard();
    try {
      await this.session.load();
      if (!valid()) return;
      if (!this.session.has('dashboard')) throw new Error('目前沒有總覽功能權限。');
      if (
        this.route.snapshot.queryParamMap.get('scope') === 'platform' &&
        this.session.has('admin')
      )
        this.scope.set('platform');
      await this.load();
    } catch (e) {
      if (valid()) {
        this.error.set(this.view.message(e));
        this.loading.set(false);
      }
    }
  }
  async load() {
    if (!this.from() || !this.through() || this.from() > this.through()) {
      this.error.set('請提供有效的開始與結束日期。');
      return;
    }
    const valid = this.view.guard(),
      sequence = ++this.sequence;
    this.loading.set(true);
    this.error.set('');
    const from = this.from(),
      through = this.through(),
      owner = this.owner();
    try {
      const data = await this.api.dashboard(datePeriod(from, through), this.scope(), owner);
      if (!valid() || sequence !== this.sequence) return;
      this.appliedFrom.set(from);
      this.appliedThrough.set(through);
      this.appliedOwner.set(owner);
      this.data.set(data);
      const options = this.units();
      if (!options.some((x) => x.value === this.unit())) this.unit.set(options[0]?.value ?? '');
    } catch (e) {
      if (valid() && sequence === this.sequence) this.error.set(this.view.message(e));
    } finally {
      if (valid() && sequence === this.sequence) this.loading.set(false);
    }
  }
  setRange(value: string) {
    this.range.set(value);
    if (value === 'custom') return;
    this.through.set(this.today);
    this.from.set(localDate(new Date(Date.now() - (Number(value) - 1) * 86400000)));
    void this.load();
  }
  setScope(value: string) {
    this.scope.set(value);
    this.owner.set('');
    this.data.set(null);
    void this.load();
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
      if (valid()) this.error.set(this.view.message(e));
    } finally {
      if (valid()) this.exporting.set(false);
    }
  }
  private days(rows: SpendBucket[], key: 'amount' | 'requests') {
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
