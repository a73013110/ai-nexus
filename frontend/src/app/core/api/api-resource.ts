import {
  computed,
  effect,
  inject,
  linkedSignal,
  resource,
  untracked,
  type Signal,
  type WritableSignal,
} from '@angular/core';
import { WorkspaceSession } from '../auth/workspace-session';
import { ClientValidationError, isCancellation, safeMessage } from '../errors/safe-errors';

export interface ApiResourceOptions<T, P> {
  /** Feature the signed-in account must have; omit for reads every account may make. */
  feature?: string;
  /** Read the account again instead of reusing it, for pages that show its permissions. */
  freshAccount?: boolean;
  /** Reactive request; `undefined` keeps the resource idle. Omit for one read per sign-in. */
  params?: () => P | undefined;
  /** `signal` aborts when the request changes or the view is destroyed. */
  loader: (params: P, signal: AbortSignal) => Promise<T>;
  /** Milliseconds until the next re-read, or `null` to stop; a hidden tab waits until shown. */
  poll?: (value: T | undefined) => number | null;
}

/** A read owned by a view or store: one shape for loading, failure and retry across the app. */
export interface ApiResource<T> {
  /**
   * The latest value read for this sign-in. It stays while a new request or re-read loads, so
   * lists do not flicker; it is dropped when a read fails (access may have been revoked), the
   * identity changes or the request becomes idle. Set it locally when a write returns the saved
   * value.
   */
  readonly value: WritableSignal<T | undefined>;
  /** A new request is in flight: the first read, or the request changed. */
  readonly loading: Signal<boolean>;
  /** Any read is in flight, including `reload()` and polling. */
  readonly refreshing: Signal<boolean>;
  /** Reviewed message for the last failed read; empty while the last read succeeded. */
  readonly error: Signal<string>;
  /** Read again with the current request. */
  reload(): void;
}

/**
 * Wraps Angular's `resource()` with what every authenticated read needs: wait for the account,
 * check the feature grant, start over (discarding late responses) when the identity changes, and
 * turn failures into reviewed messages. Writes stay explicit methods that update `value` or call
 * `reload()`.
 */
export function apiResource<T, P = true>(options: ApiResourceOptions<T, P>): ApiResource<T> {
  const session = inject(WorkspaceSession);
  const request = computed(() => {
    const generation = session.auth.generation();
    const params = options.params ? options.params() : (true as P);
    return params === undefined ? undefined : { generation, params };
  });
  const ref = resource({
    params: request,
    loader: async ({ params, abortSignal }) => {
      if (!(await session.load(options.freshAccount)))
        throw new DOMException('Signed out', 'AbortError');
      if (options.feature && !session.has(options.feature))
        throw new ClientValidationError('featureAccess');
      return options.loader(params.params, abortSignal);
    },
  });
  const loaded = computed(() =>
    ref.hasValue() ? { value: ref.value() as T } : ref.status() === 'error' ? null : undefined,
  );
  const value = linkedSignal<{ generation?: number; loaded?: { value: T } | null }, T | undefined>({
    source: () => ({ generation: request()?.generation, loaded: loaded() }),
    computation: (source, previous) =>
      source.loaded
        ? source.loaded.value
        : source.loaded === undefined &&
            source.generation !== undefined &&
            previous?.source.generation === source.generation
          ? previous.value
          : undefined,
  });
  const error = computed(() => {
    const failure = ref.error();
    return !failure || isCancellation(failure) ? '' : safeMessage(failure);
  });
  const loading = computed(() => ref.status() === 'loading');
  if (options.poll) {
    const poll = options.poll;
    effect((onCleanup) => {
      const status = ref.status();
      if (status !== 'resolved' && status !== 'error' && status !== 'local') return;
      const delay = poll(untracked(value));
      if (delay === null) return;
      let timer = setTimeout(function tick() {
        if (document.hidden) timer = setTimeout(tick, delay);
        else ref.reload();
      }, delay);
      onCleanup(() => clearTimeout(timer));
    });
  }
  return { value, loading, refreshing: ref.isLoading, error, reload: () => void ref.reload() };
}
