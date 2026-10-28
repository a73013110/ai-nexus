import {
  computed,
  effect,
  linkedSignal,
  signal,
  untracked,
  type Signal,
  type WritableSignal,
} from '@angular/core';
import { apiResource, type ApiResourceOptions } from '@core/api/api-resource';
import type { FeatureId } from '@core/api/catalog';
import { formatCalendarValue } from '../browser/date-time';
import type { SelectOption } from './select';

export type TableSortDirection = 'asc' | 'desc';
export interface TableSort {
  column: string;
  direction: TableSortDirection;
}

/** 欄位顯示偏好；`TableColumnDef` 再加上排序與篩選。 */
export interface TableColumn {
  id: string;
  label: string;
  hideable?: boolean;
  defaultHidden?: boolean;
}

/**
 * 欄位篩選的型別：`text` 包含、`options` 多選、`number` 範圍、`date` 依本地日期篩時間點（迄日含當天）、
 * `day` 篩日曆日（`yyyy-mm-dd`，後端 `DateOnly`）。
 */
export type TableFilterType =
  | 'text'
  | 'number'
  | 'date'
  | 'day'
  /** 選項可以是函式（signal），例如從已讀到的資料算出；資料只在後端逐頁時這種篩選不顯示（見 `local`）。 */
  | { options: readonly SelectOption[] | (() => readonly SelectOption[]) };

/** 欄位的篩選選項；不是選項篩選時為空。 */
export function filterOptions(column: TableColumnDef<never>): readonly SelectOption[] {
  const filter = column.filter;
  if (typeof filter !== 'object') return [];
  return typeof filter.options === 'function' ? filter.options() : filter.options;
}

export interface TableColumnDef<Row = unknown> extends TableColumn {
  /** 表頭顯示的短標題（例：「HTTP」）；欄位選擇、篩選與無障礙名稱仍用 `label`。 */
  header?: string;
  /** 可排序；`'desc'` 表示第一次按下時由大到小（時間、數量常用）。 */
  sort?: boolean | TableSortDirection;
  filter?: TableFilterType;
  /** 數字欄：表頭與儲存格靠右、等寬數字。 */
  numeric?: boolean;
  /** 時間欄：排序說明寫「由新到舊」；`date`、`day` 篩選的欄位不必另外標。 */
  time?: boolean;
  /** 在本機排序與篩選時取的值；省略時取列上同名的屬性。 */
  value?: (row: Row) => unknown;
}

export type ColumnFilter =
  | { kind: 'text'; text: string }
  | { kind: 'options'; values: readonly string[] }
  | { kind: 'range'; from?: string; to?: string };

/** 送給後端的表格參數，與 `TableRequest` 同名（ADR 0026）。 */
export interface TableRequestQuery {
  offset: number;
  limit: number;
  sort?: string;
  filter?: string[];
}

/** 後端 `TablePage<T>` 的形狀。 */
export interface TablePageLike<Row> {
  items: readonly Row[];
  total: number;
  offset: number;
  complete: boolean;
}

export interface CursorPage<Row> {
  items: readonly Row[];
  /** 下一頁的游標（常是本頁最後一筆的 ID）；沒有下一頁時為空。 */
  next?: string | number | null;
}

/** 讀取狀態的來源，`apiResource` 或其他 store 都符合。 */
export interface TableReadState {
  loading: Signal<boolean>;
  refreshing: Signal<boolean>;
  error: Signal<string>;
  reload(): void;
}

interface CommonOptions<Row> {
  columns: readonly TableColumnDef<Row>[];
  /** 初始（也是後端預設）的排序。 */
  sort?: TableSort | null;
  pageSize?: number;
  pageSizes?: readonly number[];
  /** 分頁列的計數單位，預設「筆」；依人分頁寫「人」。 */
  unit?: string;
  /** 換頁、換排序或篩選、換查詢條件時呼叫（關閉詳情、清掉選取）。 */
  navigated?: () => void;
}

export interface LocalTableOptions<Row> extends CommonOptions<Row> {
  /** 全部的列；`undefined` 表示還沒讀到。 */
  rows: () => readonly Row[] | undefined;
  read?: TableReadState;
}

export interface EndpointTableOptions<
  Row,
  P,
  Page extends TablePageLike<unknown>,
> extends CommonOptions<Row> {
  feature?: FeatureId | readonly FeatureId[];
  /** 查詢條件（通常是 QueryBar 已套用的值）；新參考就重讀第一頁，`undefined` 不讀。 */
  params: () => P | undefined;
  /** 回傳後端的 `TablePage<T>`；頁面需要的其他欄位可以一起放在回應裡，從 `data()` 取。 */
  load: (params: P, table: TableRequestQuery, signal: AbortSignal) => Promise<Page>;
  poll?: ApiResourceOptions<unknown, unknown>['poll'];
}

export interface CursorTableOptions<
  Row,
  P,
  Page extends CursorPage<unknown>,
> extends CommonOptions<Row> {
  feature?: FeatureId | readonly FeatureId[];
  params: () => P | undefined;
  /**
   * `table.cursor` 為空時讀第一頁；`table.order` 是目前的排序（沿用自己參數的後端用它）。
   * 後端不支援的排序或篩選不要在欄位上宣告。
   */
  load: (
    params: P,
    table: TableRequestQuery & { cursor?: string | number; order: TableSort | null },
    signal: AbortSignal,
  ) => Promise<Page>;
  /** 背景更新的間隔；收到目前頁的回應。 */
  poll?: (page: Page | undefined, pageIndex: number) => number | null;
}

/** 回應裡列的型別；列的型別由 `load` 的回應推得，欄位定義不參與推論。 */
type RowOf<Page> = Page extends { items: readonly (infer Row)[] } ? Row : never;

/** 表格模型再加上最後一次讀到的完整回應（列以外的欄位，例如降級旗標、統計）。 */
export type ReadTableModel<Row, Page> = TableModel<Row> & {
  readonly data: Signal<Page | undefined>;
};

/** `TablePagination` 預設可選的每頁筆數；頁面收到 `pageSizeChange` 時以它擋下其他值。 */
export const PAGE_SIZES: readonly number[] = [25, 50, 100];

export interface TableChip {
  id: string;
  label: string;
  remove(): void;
}

/**
 * 表格的資料、排序、欄位篩選與分頁（ADR 0026）。由 `localTable`、`endpointTable`、`cursorTable` 建立，
 * 交給 `<nx-data-table [table]>`：表頭（`th[nxHeader]`）、載入／空狀態與分頁列都從這裡取值，頁面只寫儲存格。
 */
export interface TableModel<Row = unknown> {
  readonly columns: readonly TableColumnDef<Row>[];
  /** 目前這一頁的列。 */
  readonly rows: Signal<readonly Row[]>;
  readonly mode: Signal<'offset' | 'cursor'>;
  readonly total: Signal<number>;
  readonly hasNext: Signal<boolean>;
  readonly pageIndex: Signal<number>;
  readonly pageSize: Signal<number>;
  readonly pageSizes: readonly number[];
  readonly unit: string;
  readonly sort: Signal<TableSort | null>;
  readonly filters: Signal<Readonly<Record<string, ColumnFilter>>>;
  /** 讀取中且沒有列（首次載入，或上一次查無資料後再查）：表格畫骨架，不讓空狀態留在畫面上。 */
  readonly loading: Signal<boolean>;
  /** 使用者感覺得到的讀取：表格淡化、操作擋住。 */
  readonly refreshing: Signal<boolean>;
  readonly error: Signal<string>;
  /** 每次換頁、排序、篩選或查詢條件就遞增；表格據此回到頂端。 */
  readonly revision: Signal<number>;
  readonly chips: Signal<TableChip[]>;
  /** 欄位篩選的 chip 已由同頁的 `QueryBar`（`[table]`）顯示；表格就不重複顯示。 */
  readonly chipsClaimed: WritableSignal<boolean>;
  /** 欄位的選項篩選是否可用：資料只在後端逐頁時，從資料算出的選項不完整。 */
  readonly local: Signal<boolean>;
  column(id: string): TableColumnDef<Row> | undefined;
  toggleSort(id: string): void;
  setFilter(id: string, filter: ColumnFilter | null): void;
  clearFilters(): void;
  turn(direction: -1 | 1): void;
  goTo(index: number): void;
  resize(size: number): void;
  reload(): void;
}

/** 資料已在前端（另一個讀取、輸入、統計）：排序、篩選與分頁都在本機。 */
export function localTable<Row>(options: LocalTableOptions<Row>): TableModel<Row> {
  const state = tableState(options);
  const all = computed(() => options.rows() ?? []);
  const shown = computed(() => applyLocal(all(), options.columns, state.sort(), state.filters()));
  const lastPage = computed(() => Math.max(0, Math.ceil(shown().length / state.pageSize()) - 1));
  // 資料變少（篩選、背景更新）時停在最後一頁，不顯示空白頁。
  const index = computed(() => Math.min(state.pageIndex(), lastPage()));
  return state.build({
    rows: computed(() => slicePage(shown(), index(), state.pageSize())),
    total: computed(() => shown().length),
    hasNext: computed(() => index() < lastPage()),
    mode: computed(() => 'offset' as const),
    local: computed(() => true),
    loading: computed(
      () => (options.read?.loading() ?? options.rows() === undefined) && !all().length,
    ),
    refreshing: options.read?.refreshing ?? computed(() => false),
    error: options.read?.error ?? computed(() => ''),
    reload: () => options.read?.reload(),
    lastPage,
  });
}

/**
 * 後端以 `TableRequest` 分頁的列表。後端回傳 `complete` 時（資料在門檻內），之後的排序、篩選與換頁都在本機完成，
 * 不再呼叫 API；查詢條件換成新參考才重讀。
 */
export function endpointTable<P, Page extends TablePageLike<unknown>>(
  options: EndpointTableOptions<RowOf<Page>, P, Page>,
): ReadTableModel<RowOf<Page>, Page> {
  type Row = RowOf<Page>;
  const state = tableState<Row>(options, options.params);
  /** 已一次讀到全部資料的查詢條件；條件相同時排序、篩選與換頁不重讀。 */
  const completeFor = signal<P | undefined>(undefined);
  let held: { params: P; query: TableRequestQuery } | undefined;
  const request = computed(() => {
    const params = options.params();
    if (params === undefined) return (held = undefined);
    if (held && held.params === params && completeFor() === params) return held;
    const query = serverQuery(options.columns, state, state.pageIndex());
    return (held = { params, query });
  });
  const read = apiResource({
    feature: options.feature,
    params: request,
    loader: async (current, signal) => ({
      current,
      page: await options.load(current.params, current.query, signal),
    }),
    poll: options.poll,
  });
  effect(() => {
    const value = read.value();
    untracked(() => completeFor.set(value?.page.complete ? value.current.params : undefined));
  });
  const complete = computed(() => !!read.value()?.page.complete);
  const shown = computed(() => {
    const page = read.value()?.page;
    if (!page) return [];
    const items = page.items as readonly Row[];
    return page.complete
      ? applyLocal(items, options.columns, state.sort(), state.filters())
      : items;
  });
  const total = computed(() => (complete() ? shown().length : (read.value()?.page.total ?? 0)));
  const lastPage = computed(() => Math.max(0, Math.ceil(total() / state.pageSize()) - 1));
  const index = computed(() => Math.min(state.pageIndex(), lastPage()));
  return withData(
    read,
    state.build({
      rows: computed(() => (complete() ? slicePage(shown(), index(), state.pageSize()) : shown())),
      total,
      hasNext: computed(() => index() < lastPage()),
      mode: computed(() => 'offset' as const),
      local: complete,
      loading: computed(() => read.loading() && !shown().length),
      refreshing: read.refreshing,
      error: read.error,
      reload: read.reload,
      lastPage,
    }),
  );
}

/** 大表（日誌、稽核）：游標分頁、不計總數；上一頁用看過的游標重讀。 */
export function cursorTable<P, Page extends CursorPage<unknown>>(
  options: CursorTableOptions<RowOf<Page>, P, Page>,
): ReadTableModel<RowOf<Page>, Page> {
  type Row = RowOf<Page>;
  const state = tableState<Row>(options, options.params);
  /** 看過的各頁游標：游標只能往後讀，上一頁要用記下的游標重讀。 */
  let cursors: (string | number | undefined)[] = [undefined];
  const request = computed(() => {
    const params = options.params();
    if (params === undefined) return undefined;
    const index = state.pageIndex();
    if (index === 0) cursors = [undefined];
    return {
      params,
      index,
      query: {
        ...serverQuery(options.columns, state, 0),
        cursor: cursors[index],
        order: state.sort(),
      },
    };
  });
  const read = apiResource({
    feature: options.feature,
    params: request,
    loader: async (current, signal) => ({
      current,
      page: await options.load(current.params, current.query, signal),
    }),
    poll: (value) => (options.poll ? options.poll(value?.page, value?.current.index ?? 0) : null),
  });
  const page = computed(() => read.value()?.page);
  const hasNext = computed(() => !!page()?.next);
  return withData(
    read,
    state.build({
      rows: computed(() => (page()?.items ?? []) as readonly Row[]),
      total: computed(() => page()?.items.length ?? 0),
      hasNext,
      mode: computed(() => 'cursor' as const),
      local: computed(() => false),
      loading: computed(() => read.loading() && !page()?.items.length),
      refreshing: read.refreshing,
      error: read.error,
      reload: read.reload,
      lastPage: computed(() => state.pageIndex() + (hasNext() ? 1 : 0)),
      beforeTurn: (index) => {
        // 往後一頁前記下它的游標；回上一頁用記下的。
        if (index === state.pageIndex() + 1) cursors[index] = page()?.next ?? undefined;
        return cursors[index] !== undefined || index === 0;
      },
      // 讀取中的頁碼以已顯示的頁為準，背景更新不跳頁。
      shownIndex: computed(() => read.value()?.current.index ?? 0),
    }),
  );
}

function withData<Row, Page>(
  read: { value: Signal<{ page: Page } | undefined> },
  model: TableModel<Row>,
): ReadTableModel<Row, Page> {
  return { ...model, data: computed(() => read.value()?.page) };
}

/**
 * 本機排序與篩選，語意與後端 `TableQuery` 相同（文字包含不分大小寫、null 在由小到大時排最前）。
 * 篩選條件先換算一次（小寫字、日期界線、選項集合），排序鍵每列只取一次，幾百列在每次按排序時也不必重算。
 */
export function applyLocal<Row>(
  rows: readonly Row[],
  columns: readonly TableColumnDef<Row>[],
  sort: TableSort | null,
  filters: Readonly<Record<string, ColumnFilter>>,
): Row[] {
  const byId = new Map(columns.map((column) => [column.id, column]));
  const tests = Object.entries(filters).flatMap(([id, filter]) => {
    const column = byId.get(id);
    if (!column) return [];
    const test = predicate(filter, column.filter);
    return [(row: Row) => test(read(row, column))];
  });
  const kept = tests.length ? rows.filter((row) => tests.every((test) => test(row))) : [...rows];
  const column = sort && byId.get(sort.column);
  if (!sort || !column) return kept;
  const sign = sort.direction === 'asc' ? 1 : -1;
  // Array.prototype.sort 是穩定的：同值維持原順序（後端預設排序＋唯一鍵）。
  return kept
    .map((row) => ({ row, key: read(row, column) }))
    .sort((a, b) => sign * compareValues(a.key, b.key))
    .map((item) => item.row);
}

const collator = new Intl.Collator('zh-Hant', { numeric: true, sensitivity: 'base' });

function compareValues(a: unknown, b: unknown): number {
  if (a == null || b == null) return a == null ? (b == null ? 0 : -1) : 1;
  if (typeof a === 'number' && typeof b === 'number') return a - b;
  if (typeof a === 'boolean' && typeof b === 'boolean') return Number(a) - Number(b);
  return collator.compare(String(a), String(b));
}

function read<Row>(row: Row, column: TableColumnDef<Row>): unknown {
  return column.value ? column.value(row) : (row as Record<string, unknown>)[column.id];
}

/** 一個欄位篩選的判斷式；條件的換算只做一次。 */
function predicate(
  filter: ColumnFilter,
  type: TableFilterType | undefined,
): (value: unknown) => boolean {
  switch (filter.kind) {
    case 'text': {
      const text = filter.text.trim().toLocaleLowerCase();
      return (value) => value != null && String(value).toLocaleLowerCase().includes(text);
    }
    case 'options': {
      const values = new Set(filter.values);
      return (value) => value != null && values.has(String(value));
    }
    case 'range': {
      const { from, to } = filter;
      if (type === 'number') {
        const low = from ? Number(from) : -Infinity;
        const high = to ? Number(to) : Infinity;
        return (value) => value != null && Number(value) >= low && Number(value) <= high;
      }
      if (type === 'day')
        return (value) => {
          if (value == null) return false;
          const day = String(value).slice(0, 10);
          return (!from || day >= from) && (!to || day <= to);
        };
      const start = from ? localDayStart(from) : -Infinity;
      const end = to ? localDayStart(to, 1) : Infinity;
      return (value) => {
        if (value == null) return false;
        const at = Date.parse(String(value));
        return at >= start && at < end;
      };
    }
  }
}

/** 本地日期的 0 時（毫秒）；`days` 往後推幾天。 */
function localDayStart(day: string, days = 0): number {
  const [year, month, date] = day.split('-').map(Number);
  return new Date(year, month - 1, date + days).getTime();
}

/** 欄位篩選轉成後端的 `filter` 參數（`欄位:運算:值`）。 */
export function filterParams(
  columns: readonly TableColumnDef<unknown>[],
  filters: Readonly<Record<string, ColumnFilter>>,
): string[] {
  return Object.entries(filters).flatMap(([id, filter]) => {
    const type = columns.find((column) => column.id === id)?.filter;
    switch (filter.kind) {
      case 'text':
        return [`${id}:has:${filter.text.trim()}`];
      case 'options':
        return [`${id}:in:${filter.values.join(',')}`];
      case 'range': {
        const bounds: string[] = [];
        if (type === 'date') {
          if (filter.from)
            bounds.push(`${id}:ge:${new Date(localDayStart(filter.from)).toISOString()}`);
          if (filter.to)
            bounds.push(`${id}:lt:${new Date(localDayStart(filter.to, 1)).toISOString()}`);
        } else {
          if (filter.from) bounds.push(`${id}:ge:${filter.from}`);
          if (filter.to) bounds.push(`${id}:le:${filter.to}`);
        }
        return bounds;
      }
    }
  });
}

/** 已套用篩選的說明，用在 chip。 */
export function describeFilter(column: TableColumnDef<never>, filter: ColumnFilter): string {
  switch (filter.kind) {
    case 'text':
      return `${column.label}：${filter.text.trim()}`;
    case 'options': {
      const options = filterOptions(column);
      const labels = filter.values.map(
        (value) => options.find((option) => option.value === value)?.label ?? value,
      );
      return `${column.label}：${labels.join('、')}`;
    }
    case 'range': {
      // 日期寫成日曆欄位顯示的 yyyy/MM/dd，跟篩選面板看到的一致。
      const show = (value?: string) =>
        !value
          ? ''
          : column.filter === 'number'
            ? value
            : formatCalendarValue(value, 'gregory', false);
      return `${column.label}：${show(filter.from)}～${show(filter.to)}`.replace('：～', '：～ ');
    }
  }
}

/** 篩選是否有值；沒有值等於清除。 */
export function hasValue(filter: ColumnFilter | null): filter is ColumnFilter {
  if (!filter) return false;
  switch (filter.kind) {
    case 'text':
      return !!filter.text.trim();
    case 'options':
      return filter.values.length > 0;
    case 'range':
      return !!filter.from || !!filter.to;
  }
}

function slicePage<Row>(rows: readonly Row[], index: number, size: number): Row[] {
  return rows.slice(index * size, (index + 1) * size);
}

function serverQuery(
  columns: readonly TableColumnDef<never>[],
  state: {
    sort: Signal<TableSort | null>;
    filters: Signal<Record<string, ColumnFilter>>;
    pageSize: Signal<number>;
  },
  index: number,
): TableRequestQuery {
  const sort = state.sort();
  const filter = filterParams(columns as readonly TableColumnDef<unknown>[], state.filters());
  return {
    offset: index * state.pageSize(),
    limit: state.pageSize(),
    ...(sort ? { sort: sort.direction === 'desc' ? `${sort.column}:desc` : sort.column } : {}),
    ...(filter.length ? { filter } : {}),
  };
}

interface BuildParts<Row> {
  rows: Signal<readonly Row[]>;
  total: Signal<number>;
  hasNext: Signal<boolean>;
  mode: Signal<'offset' | 'cursor'>;
  local: Signal<boolean>;
  loading: Signal<boolean>;
  refreshing: Signal<boolean>;
  error: Signal<string>;
  reload: () => void;
  lastPage: Signal<number>;
  /** 換到 `index` 前的檢查（游標分頁記下游標）；回傳 false 表示不能去。 */
  beforeTurn?: (index: number) => boolean;
  shownIndex?: Signal<number>;
}

/**
 * 三種來源共用的排序、篩選、分頁狀態與動作。`params` 換成新參考時頁碼同步回到 0（linkedSignal），
 * 請求才不會先用舊頁碼讀一次。
 */
function tableState<Row>(options: CommonOptions<Row>, params?: () => unknown) {
  const pageSizes = options.pageSizes ?? PAGE_SIZES;
  const sort = signal<TableSort | null>(options.sort ?? null);
  const filters = signal<Record<string, ColumnFilter>>({});
  const pageSize = signal(options.pageSize ?? pageSizes[1] ?? pageSizes[0]);
  const page = linkedSignal({ source: params ?? (() => null), computation: () => 0 });
  const revision = signal(0);
  const columns = new Map(options.columns.map((column) => [column.id, column]));
  const navigated = () => {
    revision.update((value) => value + 1);
    options.navigated?.();
  };
  if (params) {
    let first = true;
    effect(() => {
      params();
      if (first) first = false;
      else untracked(navigated);
    });
  }
  return {
    sort: sort.asReadonly(),
    filters: filters.asReadonly(),
    pageSize: pageSize.asReadonly(),
    pageIndex: page.asReadonly(),
    build(parts: BuildParts<Row>): TableModel<Row> {
      const blocked = () => untracked(parts.refreshing);
      const go = (index: number) => {
        if (blocked() || index < 0 || index > untracked(parts.lastPage)) return;
        if (parts.beforeTurn && !parts.beforeTurn(index)) return;
        page.set(index);
        navigated();
      };
      const restart = () => {
        page.set(0);
        navigated();
      };
      const pageIndex = parts.shownIndex ?? page.asReadonly();
      const model: TableModel<Row> = {
        columns: options.columns,
        rows: parts.rows,
        mode: parts.mode,
        total: parts.total,
        hasNext: parts.hasNext,
        pageIndex: computed(() => Math.min(pageIndex(), parts.lastPage())),
        pageSize: pageSize.asReadonly(),
        pageSizes,
        unit: options.unit ?? '筆',
        sort: sort.asReadonly(),
        filters: filters.asReadonly(),
        loading: parts.loading,
        refreshing: parts.refreshing,
        error: parts.error,
        revision: revision.asReadonly(),
        local: parts.local,
        chips: computed(() =>
          Object.entries(filters()).flatMap(([id, filter]) => {
            const column = columns.get(id);
            return column
              ? [
                  {
                    id,
                    label: describeFilter(column as TableColumnDef<never>, filter),
                    remove: () => model.setFilter(id, null),
                  },
                ]
              : [];
          }),
        ),
        chipsClaimed: signal(false),
        column: (id) => columns.get(id),
        toggleSort(id) {
          const column = columns.get(id);
          if (!column?.sort || blocked()) return;
          const current = untracked(sort);
          const first = column.sort === 'desc' ? 'desc' : 'asc';
          sort.set({
            column: id,
            direction:
              current?.column === id ? (current.direction === 'asc' ? 'desc' : 'asc') : first,
          });
          restart();
        },
        setFilter(id, filter) {
          if (blocked() || !columns.get(id)?.filter) return;
          // 清除本來就沒有的篩選不算換條件：不重讀、不回第一頁。
          if (!hasValue(filter) && !(id in untracked(filters))) return;
          filters.update((current) => {
            const { [id]: _removed, ...rest } = current;
            return hasValue(filter) ? { ...rest, [id]: filter } : rest;
          });
          restart();
        },
        clearFilters() {
          if (blocked() || !Object.keys(untracked(filters)).length) return;
          filters.set({});
          restart();
        },
        turn: (direction) => go(untracked(pageIndex) + direction),
        goTo: go,
        resize(size) {
          if (blocked() || !pageSizes.includes(size) || size === untracked(pageSize)) return;
          pageSize.set(size);
          restart();
        },
        reload: parts.reload,
      };
      return model;
    },
  };
}
