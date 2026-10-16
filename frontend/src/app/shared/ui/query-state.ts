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

/** One applied condition shown as a removable chip; removing it applies `clear`. */
export interface QueryChip<T> {
  id: string;
  label: string;
  clear: Partial<T>;
}

export interface QueryStateOptions<T> {
  /** A fresh query; called again on reset, so relative times start from that moment. */
  defaults: () => T;
  /** Applied values as URL parameters; leave out values at their defaults. */
  toParams?: (value: T) => Params;
  /** The query a link presets; parameters that are absent or malformed keep `defaults`. */
  fromParams?: (params: ParamMap, defaults: T) => T;
  /** The applied conditions to show as chips, usually those away from their defaults. */
  chips?: (value: T) => QueryChip<T>[];
  /** Why the values cannot be queried, or '' when they can. */
  validate?: (value: T) => string;
}

/**
 * The conditions a query form holds (`draft`) apart from those its results were read with
 * (`applied`). Every `apply` sets a new `applied` value, so the same conditions read again; the
 * applied conditions replace the URL query, and a navigation to other parameters on the same page
 * (a link, the sidebar) applies them. Paging, sorting and page size belong to the page, not here.
 * Create it with `queryState()` in an injection context.
 */
export class QueryState<T extends object> {
  readonly draft: WritableSignal<T>;
  private readonly current: WritableSignal<T>;
  readonly applied: Signal<T>;
  readonly error = signal('');
  readonly dirty: Signal<boolean>;
  readonly chips: Signal<QueryChip<T>[]>;
  /** The URL query last written or read, so the page's own writes are not applied twice. */
  private seen = '';

  constructor(
    private readonly options: QueryStateOptions<T>,
    private readonly router?: Router,
    private readonly route?: ActivatedRoute,
  ) {
    const initial = this.fromRoute(route?.snapshot.queryParamMap);
    this.draft = signal(initial);
    this.current = signal(initial);
    this.applied = this.current.asReadonly();
    this.dirty = computed(() => stableJson(this.draft()) !== stableJson(this.current()));
    this.chips = computed(() => this.options.chips?.(this.current()) ?? []);
    if (route) this.seen = paramsKey(route.snapshot.queryParamMap);
  }

  set<K extends keyof T>(key: K, value: T[K]) {
    this.draft.update((draft) => ({ ...draft, [key]: value }));
  }
  patch(values: Partial<T>) {
    this.draft.update((draft) => ({ ...draft, ...values }));
  }
  /** Reads with the draft; returns false (and sets `error`) when it cannot be queried. */
  apply(): boolean {
    const value = this.draft();
    const error = this.options.validate?.(value) ?? '';
    this.error.set(error);
    if (error) return false;
    this.write({ ...value });
    return true;
  }
  /** Applies these values at once (a view switch, a removed chip); other draft edits stay pending. */
  commit(values: Partial<T>) {
    const value = { ...this.current(), ...values };
    this.draft.update((draft) => ({ ...draft, ...values }));
    this.error.set('');
    this.write(value);
  }
  remove(chip: QueryChip<T>) {
    this.commit(chip.clear);
  }
  reset() {
    const value = this.options.defaults();
    this.draft.set(value);
    this.error.set('');
    this.write({ ...value });
  }
  /** Follows the URL when another navigation changes it; call once, after construction. */
  listen(destroy: DestroyRef) {
    this.route?.queryParamMap.pipe(takeUntilDestroyed(destroy)).subscribe((params) => {
      const key = paramsKey(params);
      if (key === this.seen) return;
      this.seen = key;
      const value = this.fromRoute(params);
      this.draft.set(value);
      this.error.set('');
      this.current.set({ ...value });
    });
  }
  private fromRoute(params: ParamMap | undefined): T {
    const defaults = this.options.defaults();
    if (!params || !this.options.fromParams) return defaults;
    const value = this.options.fromParams(params, defaults);
    return this.options.validate?.(value) ? defaults : value;
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

/** A `QueryState` bound to the current route's URL query. */
export function queryState<T extends object>(options: QueryStateOptions<T>): QueryState<T> {
  const state = new QueryState(options, inject(Router), inject(ActivatedRoute));
  state.listen(inject(DestroyRef));
  return state;
}

function stableJson(value: unknown): string {
  return JSON.stringify(value, (_, item: unknown) =>
    item && typeof item === 'object' && !Array.isArray(item)
      ? Object.fromEntries(Object.entries(item).sort(([a], [b]) => a.localeCompare(b)))
      : item,
  );
}

/** One spelling for the same parameters, whether read from the URL or about to be written. */
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
    entries.filter(([, values]) => values.length).sort(([a], [b]) => a.localeCompare(b)),
  );
}
