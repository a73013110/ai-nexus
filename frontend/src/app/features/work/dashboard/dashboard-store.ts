import { computed, DestroyRef, inject, Injectable, linkedSignal, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { apiResource } from '@core/api/api-resource';
import type { DashboardDto, DashboardSectionDto } from '@core/api/schema';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { localDateKey } from '@shared/browser/calendar-day';
import { downloadBlob } from '@shared/browser/download';
import { ViewScope } from '@shared/browser/view-scope';
import { BillingApi, chargeKind, datePeriod } from '@features/admin/billing/billing-api';
import { DashboardApi } from './dashboard-api';
import { dayRange, fillDays } from './dashboard-format';

/** 個人總覽會自行重新整理；需稽核的平台讀取則等檢視者操作。 */
const REFRESH_MS = 60_000;
/** 輸入的日期要等檢視者停止輸入後才套用，避免讀到輸到一半的日期（或被稽核）。 */
const DATE_SETTLE_MS = 600;

/** 含今天在內 `count` 天期間的第一天。 */
const firstDayOf = (count: number) => localDateKey(new Date(Date.now() - (count - 1) * 86400000));
/** 費用合計、每日與模型列共用的幣別＋類型鍵；不同幣別與類型不相加。 */
const unitOf = (item: { currency: string; kind: string }) => item.currency + ':' + item.kind;

/**
 * 總覽的一次讀取，以及其周圍的選擇：範圍、期間、所選使用者與顯示的幣別。
 * 由頁面提供，讓每個小工具讀取同一份快照。
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
  /** 只有平台操作者能看到整個平台；其他人永遠只看到自己的工作。 */
  readonly scope = computed<'personal' | 'platform'>(() =>
    this.requestedScope() === 'platform' && this.session.has('operations.usage')
      ? 'platform'
      : 'personal',
  );
  readonly owner = signal('');
  readonly today = localDateKey(new Date());
  readonly from = signal(firstDayOf(30));
  readonly through = signal(this.today);
  readonly rangeOptions = [
    { value: '7', label: '7 天' },
    { value: '30', label: '30 天' },
    { value: '90', label: '90 天' },
    { value: '365', label: '1 年' },
    { value: 'custom', label: '自訂' },
  ];
  readonly range = signal('30');
  /** 最後送出的期間與擁有者；讀取會跟著它與範圍。 */
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
  /** 其他範圍的數字絕不會顯示在此範圍的標籤下。 */
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
          (this.data()?.spend.users.find((item) => item.ownerId === this.appliedOwner())
            ?.displayName ?? '指定使用者')
        : '整個平台',
  );
  readonly sections = computed(() => this.data()?.sections ?? []);
  private readonly byId = computed(
    () => new Map<string, DashboardSectionDto>(this.sections().map((item) => [item.id, item])),
  );
  section(id: string) {
    return this.byId().get(id) ?? null;
  }
  /** 區段或指標的當期或期間數字；區段或指標不存在時為 `null`。 */
  metric(section: string, key: string) {
    return this.section(section)?.metrics.find((item) => item.key === key)?.value ?? null;
  }

  readonly units = computed(() =>
    (this.data()?.spend.totals ?? []).map((item) => ({
      value: unitOf(item),
      label: item.currency ? item.currency + ' · ' + chargeKind(item.kind) : '未設定價格',
    })),
  );
  /** 所選幣別仍有花費時保留；否則顯示第一個。 */
  readonly unit = linkedSignal<{ value: string }[], string>({
    source: this.units,
    computation: (options, previous) =>
      previous && options.some((item) => item.value === previous.value)
        ? previous.value
        : (options[0]?.value ?? ''),
  });
  readonly total = computed(
    () => this.data()?.spend.totals.find((item) => unitOf(item) === this.unit()) ?? null,
  );
  readonly unknownCalls = computed(
    () =>
      (this.data()?.spend.totals.reduce((sum, item) => sum + item.unknownCalls, 0) ?? 0) +
      (this.data()?.spend.legacyCalls ?? 0),
  );
  /** 所選幣別與類型下，每天的已知花費。 */
  readonly spendDaily = computed(() =>
    fillDays(
      this.days(),
      (this.data()?.spend.daily ?? [])
        .filter((item) => unitOf(item) === this.unit())
        .map((item) => ({ date: item.label, value: item.amount })),
    ),
  );
  /** 每天的模型與工具呼叫次數，跨所有幣別與類型。 */
  readonly callsDaily = computed(() =>
    fillDays(
      this.days(),
      (this.data()?.spend.daily ?? []).map((item) => ({ date: item.label, value: item.requests })),
    ),
  );
  readonly tokensDaily = computed(() =>
    fillDays(
      this.days(),
      (this.data()?.tokens.daily ?? []).map((item) => ({
        date: item.date,
        value: item.inputTokens + item.outputTokens,
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
      .filter((item) => unitOf(item) === this.unit())
      .sort((first, second) => second.amount - first.amount),
  );
  readonly users = computed(() =>
    (this.data()?.spend.users ?? []).filter((item) => unitOf(item) === this.unit()),
  );
  /** 正在產生的 AI 回覆，加上排隊或執行中的背景工作。 */
  readonly activeWork = computed(
    () => (this.metric('chat', 'generating') ?? 0) + (this.metric('tasks', 'active') ?? 0),
  );
  readonly attention = computed(() => this.sections().filter((item) => item.state === 'attention'));
  /** 每個區段的最近項目，最新的在前：檢視者在各功能的工作。 */
  readonly recent = computed(() =>
    this.sections()
      .filter((item) => item.id !== 'notifications')
      .flatMap((section) => section.items.map((item) => ({ ...item, section })))
      .sort((first, second) => (second.at ?? '').localeCompare(first.at ?? '')),
  );

  private settle = 0;
  constructor() {
    inject(DestroyRef).onDestroy(() => clearTimeout(this.settle));
  }

  /** 讀取所選期間；`force` 即使沒有變更也會再讀一次（重新整理、重試）。 */
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
  /** 輸入或選取的日期；空值代表未完成或超出範圍的輸入，會等待。 */
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
    this.from.set(firstDayOf(Number(value)));
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
    } catch (error) {
      if (valid()) this.exportError.set(this.view.message(error));
    } finally {
      if (valid()) this.exporting.set(false);
    }
  }
}
