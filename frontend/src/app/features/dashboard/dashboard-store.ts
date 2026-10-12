import { computed, DestroyRef, inject, Injectable, linkedSignal, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { apiResource } from '../../core/api/api-resource';
import type { DashboardDto, DashboardSectionDto } from '../../core/api/schema';
import { WorkspaceSession } from '../../core/auth/workspace-session';
import { downloadBlob } from '../../shared/browser/download';
import { ViewScope } from '../../shared/browser/view-scope';
import { BillingApi, chargeKind, datePeriod, localDate } from '../billing/billing-api';
import { DashboardApi } from './dashboard-api';
import { dayRange, fillDays } from './dashboard-format';

/** The personal overview refreshes itself; the audited platform read waits for the viewer. */
const REFRESH_MS = 60_000;
/** Typed dates apply once the viewer pauses, so a half-typed day is not read (or audited). */
const DATE_SETTLE_MS = 600;

/**
 * The overview's one read and the choices around it: scope, period, selected user and the
 * currency shown. Provided by the page, so every widget reads the same snapshot.
 */
@Injectable()
export class DashboardStore {
  readonly session = inject(WorkspaceSession);
  private readonly api = inject(DashboardApi);
  private readonly billing = inject(BillingApi);
  private readonly view = inject(ViewScope);
  private readonly requestedScope = signal(
    inject(ActivatedRoute).snapshot.queryParamMap.get('scope') === 'platform'
      ? 'platform'
      : 'personal',
  );
  /** Only administrators can see the whole platform; others always see their own work. */
  readonly scope = computed<'personal' | 'platform'>(() =>
    this.requestedScope() === 'platform' && this.session.has('admin') ? 'platform' : 'personal',
  );
  readonly owner = signal('');
  readonly today = localDate(new Date());
  readonly from = signal(localDate(new Date(Date.now() - 29 * 86400000)));
  readonly through = signal(this.today);
  readonly rangeOptions = [
    { value: '7', label: '7 天' },
    { value: '30', label: '30 天' },
    { value: '90', label: '90 天' },
    { value: '365', label: '1 年' },
    { value: 'custom', label: '自訂' },
  ];
  readonly range = signal('30');
  /** The period and owner last submitted; the read follows it and the scope. */
  private readonly query = signal({ from: this.from(), through: this.through(), owner: '' });
  private readonly resource = apiResource({
    feature: 'dashboard',
    params: () => ({ ...this.query(), scope: this.scope() }),
    loader: async (query) => ({
      query,
      data: await this.api.read(datePeriod(query.from, query.through), query.scope, query.owner),
    }),
    poll: (value) => (value?.query.scope === 'personal' ? REFRESH_MS : null),
  });
  /** Figures from another scope are never shown under this scope's label. */
  readonly data = computed<DashboardDto | null>(() => {
    const value = this.resource.value();
    return value?.query.scope === this.scope() ? value.data : null;
  });
  readonly loading = this.resource.loading;
  readonly refreshing = this.resource.refreshing;
  readonly formError = signal('');
  readonly exportError = signal('');
  readonly error = computed(() => this.formError() || this.resource.error() || this.exportError());
  readonly exporting = signal(false);
  readonly appliedFrom = computed(() => this.resource.value()?.query.from ?? this.from());
  readonly appliedThrough = computed(() => this.resource.value()?.query.through ?? this.through());
  readonly appliedOwner = computed(() => this.resource.value()?.query.owner ?? '');
  readonly days = computed(() => dayRange(this.appliedFrom(), this.appliedThrough()));
  readonly scopeLabel = computed(() =>
    this.scope() === 'personal'
      ? '我的工作'
      : this.appliedOwner()
        ? '使用者：' +
          (this.data()?.spend.users.find((x) => x.ownerId === this.appliedOwner())?.displayName ??
            '指定使用者')
        : '整個平台',
  );
  readonly sections = computed(() => this.data()?.sections ?? []);
  private readonly byId = computed(
    () => new Map<string, DashboardSectionDto>(this.sections().map((x) => [x.id, x])),
  );
  section(id: string) {
    return this.byId().get(id) ?? null;
  }
  /** A current or period figure of a section, or `null` when the section or metric is absent. */
  metric(section: string, key: string) {
    return this.section(section)?.metrics.find((x) => x.key === key)?.value ?? null;
  }

  readonly units = computed(() =>
    (this.data()?.spend.totals ?? []).map((x) => ({
      value: x.currency + ':' + x.kind,
      label: x.currency ? x.currency + ' · ' + chargeKind(x.kind) : '未設定價格',
    })),
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
  readonly unknownCalls = computed(
    () =>
      (this.data()?.spend.totals.reduce((a, x) => a + x.unknownCalls, 0) ?? 0) +
      (this.data()?.spend.legacyCalls ?? 0),
  );
  /** Known spend per day in the chosen currency and kind. */
  readonly spendDaily = computed(() =>
    fillDays(
      this.days(),
      (this.data()?.spend.daily ?? [])
        .filter((x) => x.currency + ':' + x.kind === this.unit())
        .map((x) => ({ date: x.label, value: x.amount })),
    ),
  );
  /** Model and tool calls per day, across currencies and kinds. */
  readonly callsDaily = computed(() =>
    fillDays(
      this.days(),
      (this.data()?.spend.daily ?? []).map((x) => ({ date: x.label, value: x.requests })),
    ),
  );
  readonly tokensDaily = computed(() =>
    fillDays(
      this.days(),
      (this.data()?.tokens.daily ?? []).map((x) => ({
        date: x.date,
        value: x.inputTokens + x.outputTokens,
      })),
    ),
  );
  readonly tokenTotals = computed(() =>
    (this.data()?.tokens.daily ?? []).reduce(
      (sum, row) => ({
        input: sum.input + row.inputTokens,
        output: sum.output + row.outputTokens,
      }),
      { input: 0, output: 0 },
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
  /** AI replies being generated plus queued or running background jobs. */
  readonly activeWork = computed(
    () => (this.metric('chat', 'generating') ?? 0) + (this.metric('tasks', 'active') ?? 0),
  );
  readonly attention = computed(() => this.sections().filter((x) => x.state === 'attention'));
  /** Every section's recent items, newest first: the viewer's own work across features. */
  readonly recent = computed(() =>
    this.sections()
      .filter((x) => x.id !== 'notifications')
      .flatMap((section) => section.items.map((item) => ({ ...item, section })))
      .sort((a, b) => (b.at ?? '').localeCompare(a.at ?? '')),
  );

  private settle = 0;
  constructor() {
    inject(DestroyRef).onDestroy(() => clearTimeout(this.settle));
  }

  /** Reads the chosen period; `force` reads again even when nothing changed (refresh, retry). */
  load(force = true) {
    clearTimeout(this.settle);
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
    ) {
      if (force) this.resource.reload();
    } else this.query.set(query);
  }
  /** A typed or picked date; an empty value is an unfinished or out-of-range entry and waits. */
  setDate(which: 'from' | 'through', value: string) {
    (which === 'from' ? this.from : this.through).set(value);
    this.range.set('custom');
    clearTimeout(this.settle);
    if (value) this.settle = setTimeout(() => this.load(false), DATE_SETTLE_MS);
  }
  setRange(value: string) {
    this.range.set(value);
    if (value === 'custom') return;
    this.through.set(this.today);
    this.from.set(localDate(new Date(Date.now() - (Number(value) - 1) * 86400000)));
    this.load(false);
  }
  setScope(value: string) {
    this.requestedScope.set(value);
    this.owner.set('');
    this.load();
  }
  viewOwner(id: string) {
    this.owner.set(id);
    this.load();
  }
  async export() {
    if (this.exporting() || !this.data()) return;
    const valid = this.view.guard();
    this.exporting.set(true);
    try {
      const response = await this.billing.export(
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
}
