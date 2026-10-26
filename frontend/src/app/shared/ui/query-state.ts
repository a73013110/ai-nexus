import {
  DestroyRef,
  computed,
  inject,
  signal,
  type Signal,
  type WritableSignal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, type ParamMap, type Params } from '@angular/router';

/** 一個已套用的條件，顯示成可移除的 chip；移除時以 `clear` 覆蓋並立即套用。 */
export interface QueryChip<T> {
  id: string;
  label: string;
  clear: Partial<T>;
}

export interface QueryStateOptions<T> {
  /** 全新的條件；重設時會再呼叫一次，「最近 24 小時」這類相對時間才會從重設當下起算。 */
  defaults: () => T;
  /** 已套用條件轉成 URL 參數；等於預設值的不要寫入。 */
  toParams?: (value: T) => Params;
  /** 從連結帶入條件；缺少或格式不對的參數沿用 `defaults`。 */
  fromParams?: (params: ParamMap, defaults: T) => T;
  /** 要顯示成 chip 的已套用條件，通常是不等於預設值的那些。 */
  chips?: (value: T) => QueryChip<T>[];
  /** 無法查詢的原因；可以查詢時回傳空字串。 */
  validate?: (value: T) => string;
  /**
   * 連動的條件：上層改成另一個值時，下層回到預設值（`{ unitId: ['person'] }`：換單位就清掉選的人，人員選項另依單位重讀）。
   * 上層清回預設值只是範圍變大，原本選的下層照樣成立，保留。同一次一起指定的下層（連結、`commit`）不清。
   */
  dependents?: { readonly [K in keyof T]?: readonly (keyof T)[] };
}

/**
 * 查詢表單的兩份條件：`draft` 是正在編輯、尚未送出的，`applied` 是目前結果所用的。
 * - `apply` 每次都給 `applied` 新物件，條件沒變也會重新查詢（等於重新整理）。
 * - 已套用條件以 replaceUrl 寫回 URL；同一頁的其他導覽（連結、側邊欄）改了參數時，反過來套用
 * URL 的條件。
 * - 分頁、排序、每頁筆數不屬於查詢條件，由頁面自己管理。
 * 在 injection context 用 `queryState()` 建立。
 */
export class QueryState<T extends object> {
  readonly draft: WritableSignal<T>;
  private readonly current: WritableSignal<T>;
  readonly applied: Signal<T>;
  readonly error = signal('');
  readonly dirty: Signal<boolean>;
  readonly chips: Signal<QueryChip<T>[]>;
  /**
   * 最近寫入或讀到的 URL 參數。自己寫 URL 也會觸發 queryParamMap，
   * 靠它比對才不會把同一組條件再套用一次。
   */
  private seen = '';

  constructor(
    private readonly options: QueryStateOptions<T>,
    private readonly router?: Router,
    private readonly route?: ActivatedRoute,
  ) {
    const initial = this.fromRoute(route?.snapshot.queryParamMap);
    this.draft = signal(initial.draft);
    this.current = signal(initial.applied);
    this.error.set(initial.error);
    this.applied = this.current.asReadonly();
    this.dirty = computed(() => stableJson(this.draft()) !== stableJson(this.current()));
    this.chips = computed(() => this.options.chips?.(this.current()) ?? []);
    if (route) this.seen = paramsKey(route.snapshot.queryParamMap);
  }

  set<K extends keyof T>(key: K, value: T[K]) {
    this.draft.update((draft) => this.cascade(draft, { [key]: value } as unknown as Partial<T>));
  }
  /** 以草稿查詢；驗證不過時設定 `error`、回傳 false，結果維持不變。 */
  apply(): boolean {
    const value = this.draft();
    const error = this.options.validate?.(value) ?? '';
    this.error.set(error);
    if (error) return false;
    this.write({ ...value });
    return true;
  }
  /** 只把這幾個值立即套用（分類切換、移除 chip）；草稿裡其他未送出的修改仍留在草稿。 */
  commit(values: Partial<T>) {
    const value = this.cascade(this.current(), values);
    this.draft.update((draft) => this.cascade(draft, values));
    this.error.set('');
    this.write(value);
  }
  reset() {
    const value = this.options.defaults();
    this.draft.set(value);
    this.error.set('');
    this.write({ ...value });
  }
  /** 跟隨其他導覽造成的 URL 變更；建構後呼叫一次。 */
  listen(destroy: DestroyRef) {
    this.route?.queryParamMap.pipe(takeUntilDestroyed(destroy)).subscribe((params) => {
      const key = paramsKey(params);
      if (key === this.seen) return;
      this.seen = key;
      const next = this.fromRoute(params);
      this.draft.set(next.draft);
      this.error.set(next.error);
      this.current.set(next.applied);
    });
  }
  private fromRoute(params: ParamMap | undefined): { draft: T; applied: T; error: string } {
    const defaults = this.options.defaults();
    if (!params || !this.options.fromParams)
      return { draft: defaults, applied: { ...defaults }, error: '' };
    const value = this.options.fromParams(params, defaults);
    const error = this.options.validate?.(value) ?? '';
    if (!error) return { draft: value, applied: { ...value }, error: '' };
    // 連結帶入的條件驗證不過時不查詢、結果顯示預設條件；連結的條件留在草稿並說明原因，
    // 使用者可以修正後再查，不會誤以為連結的條件已生效。
    return {
      draft: value,
      applied: this.options.defaults(),
      error: '連結的條件無法查詢，目前顯示預設條件：' + error,
    };
  }
  /** 套上 `values`，再清掉上層換了值的下層（`dependents`）。 */
  private cascade(base: T, values: Partial<T>): T {
    const next = { ...base, ...values };
    const dependents = this.options.dependents;
    if (!dependents) return next;
    const defaults = this.options.defaults();
    for (const key of Object.keys(values) as (keyof T)[]) {
      const changed = stableJson(base[key]) !== stableJson(next[key]);
      if (!changed || stableJson(next[key]) === stableJson(defaults[key])) continue;
      for (const dependent of dependents[key] ?? [])
        if (!Object.hasOwn(values, dependent)) next[dependent] = defaults[dependent];
    }
    return next;
  }
  private write(value: T) {
    this.current.set(value);
    if (!this.router || !this.route || !this.options.toParams) return;
    const queryParams = this.options.toParams(value);
    const key = paramsKey(queryParams);
    if (key === this.seen) return;
    this.seen = key;
    void this.router.navigate([], { relativeTo: this.route, queryParams, replaceUrl: true });
  }
}

/** 綁定目前路由 URL 參數的 `QueryState`。 */
export function queryState<T extends object>(options: QueryStateOptions<T>): QueryState<T> {
  const state = new QueryState(options, inject(Router), inject(ActivatedRoute));
  state.listen(inject(DestroyRef));
  return state;
}

/** 鍵排序後再序列化，鍵順序不同的同一組條件才不會被判定為「已變更」。 */
function stableJson(value: unknown): string {
  return JSON.stringify(value, (_, item: unknown) =>
    item && typeof item === 'object' && !Array.isArray(item)
      ? Object.fromEntries(
          Object.entries(item).sort(([text], [text2]) => text.localeCompare(text2)),
        )
      : item,
  );
}

/** 把從 URL 讀到的 ParamMap 與準備寫入的 Params 轉成同一種字串，才能比對兩者是否相同。 */
function paramsKey(params: ParamMap | Params): string {
  const entries: [string, string[]][] =
    'keys' in params && typeof params.getAll === 'function'
      ? (params as ParamMap).keys.map((key) => [key, (params as ParamMap).getAll(key)])
      : Object.entries(params as Params).map(([key, value]) => [
          key,
          value === null || value === undefined || value === ''
            ? []
            : (Array.isArray(value) ? value : [value]).map(String),
        ]);
  return JSON.stringify(
    entries
      .filter(([, values]) => values.length)
      .sort(([text3], [text4]) => text3.localeCompare(text4)),
  );
}
