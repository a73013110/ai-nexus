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

/**
 * 一個查詢條件：預設值、URL 參數與 chip 在同一處宣告。`write` 與 `chip` 只在值不等於預設值時呼叫，
 * 所以預設值不寫進網址、也不成為 chip；常見的欄位用 `query-fields.ts` 的 `queryField` 建立。
 * 第二個參數是整組條件，給要看其他欄位才寫得出來的欄位用（例：日誌的 GUID 比對欄位）。
 */
export interface QueryFieldSpec<V, T = unknown> {
  /** 全新的值；重設時會再呼叫一次，「本月」這類相對值才會從重設當下起算。 */
  initial: () => V;
  /** 從連結讀；缺少或格式不對時回傳 `undefined`，沿用預設值。不寫表示這個條件不放進網址。 */
  read?: (params: ParamMap) => V | undefined;
  /** 寫進網址的參數；值為空字串或 `undefined` 的參數不寫。 */
  write?: (value: V, query: T) => Params;
  /** chip 文字；空字串表示不顯示（例：上方切換本身就看得到目前的值）。 */
  chip?: (value: V, query: T) => string;
}
export type QueryFieldSpecs<T> = { [K in keyof T]-?: QueryFieldSpec<T[K], T> };

export interface QueryStateOptions<T> {
  /** 每個條件一個欄位；宣告順序就是 chip 的順序。 */
  fields: QueryFieldSpecs<T>;
  /** 無法查詢的原因；可以查詢時回傳空字串。 */
  validate?: (value: T) => string;
  /**
   * 連動的條件：上層改成另一個值時，下層回到預設值（`{ unitId: ['person'] }`：換單位就清掉選的人，人員選項另依單位重讀）。
   * 上層清回預設值只是範圍變大，原本選的下層照樣成立，保留。同一次一起指定的下層（連結、`commit`）不清。
   */
  dependents?: { readonly [K in keyof T]?: readonly (keyof T)[] };
}

const keysOf = <T>(options: QueryStateOptions<T>) => Object.keys(options.fields) as (keyof T)[];

/** 全部條件的預設值。 */
export function queryDefaults<T>(options: QueryStateOptions<T>): T {
  return Object.fromEntries(
    keysOf(options).map((key) => [key, options.fields[key].initial()]),
  ) as T;
}

/** 連結帶入的條件；缺少或格式不對的參數沿用預設值。 */
export function readQuery<T>(options: QueryStateOptions<T>, params: ParamMap): T {
  const value = queryDefaults(options);
  for (const key of keysOf(options)) {
    const read = options.fields[key].read?.(params);
    if (read !== undefined) value[key] = read;
  }
  return value;
}

/** 已套用條件轉成 URL 參數；等於預設值的條件不寫。 */
export function writeQuery<T>(options: QueryStateOptions<T>, value: T): Params {
  const defaults = queryDefaults(options);
  const params: Params = {};
  for (const key of keysOf(options))
    if (!same(value[key], defaults[key]))
      for (const [name, item] of Object.entries(
        options.fields[key].write?.(value[key], value) ?? {},
      ))
        if (item !== undefined && item !== '') params[name] = item;
  return params;
}

/** 不等於預設值、且有 chip 文字的條件，依宣告順序；移除時回到該欄位的預設值。 */
export function queryChips<T>(options: QueryStateOptions<T>, value: T): QueryChip<T>[] {
  const defaults = queryDefaults(options);
  return keysOf(options).flatMap((key) => {
    if (same(value[key], defaults[key])) return [];
    const label = options.fields[key].chip?.(value[key], value) ?? '';
    return label
      ? [{ id: String(key), label, clear: { [key]: defaults[key] } as unknown as Partial<T> }]
      : [];
  });
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
    this.dirty = computed(() => !same(this.draft(), this.current()));
    this.chips = computed(() => queryChips(this.options, this.current()));
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
  /** 全部條件的預設值；「本月」這類相對值以呼叫當下計算。 */
  defaults(): T {
    return queryDefaults(this.options);
  }
  reset() {
    const value = this.defaults();
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
    if (!params) {
      const defaults = this.defaults();
      return { draft: defaults, applied: { ...defaults }, error: '' };
    }
    const value = readQuery(this.options, params);
    const error = this.options.validate?.(value) ?? '';
    if (!error) return { draft: value, applied: { ...value }, error: '' };
    // 連結帶入的條件驗證不過時不查詢、結果顯示預設條件；連結的條件留在草稿並說明原因，
    // 使用者可以修正後再查，不會誤以為連結的條件已生效。
    return {
      draft: value,
      applied: this.defaults(),
      error: '連結的條件無法查詢，目前顯示預設條件：' + error,
    };
  }
  /** 套上 `values`，再清掉上層換了值的下層（`dependents`）。 */
  private cascade(base: T, values: Partial<T>): T {
    const next = { ...base, ...values };
    const dependents = this.options.dependents;
    if (!dependents) return next;
    const defaults = this.defaults();
    for (const key of Object.keys(values) as (keyof T)[]) {
      if (same(base[key], next[key]) || same(next[key], defaults[key])) continue;
      for (const dependent of dependents[key] ?? [])
        if (!Object.hasOwn(values, dependent)) next[dependent] = defaults[dependent];
    }
    return next;
  }
  private write(value: T) {
    this.current.set(value);
    if (!this.router || !this.route) return;
    const queryParams = writeQuery(this.options, value);
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

const same = (a: unknown, b: unknown) => stableJson(a) === stableJson(b);

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
