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
import type { FeatureId } from './catalog';
import { ClientValidationError, isCancellation, safeMessage } from '../errors/safe-errors';

export interface ApiResourceOptions<T, P> {
  /**
   * 已登入帳號必須具備的功能；陣列表示具備其中任一個即可（例：存取目錄供使用者頁與存取控制各頁共用）。
   * 所有帳號都能讀取的資料則省略。
   */
  feature?: FeatureId | readonly FeatureId[];
  /** 重新讀取帳號而不沿用快取，供顯示權限的頁面使用。 */
  freshAccount?: boolean;
  /** 響應式請求；`undefined` 讓資源保持閒置。省略時每次登入只讀一次。 */
  params?: () => P | undefined;
  /** 請求改變或檢視被銷毀時，`signal` 會中止請求。 */
  loader: (params: P, signal: AbortSignal) => Promise<T>;
  /** 距離下次重新讀取的毫秒數；`null` 代表停止。分頁隱藏時會等到可見才讀。 */
  poll?: (value: T | undefined) => number | null;
}

/** 由檢視或 store 持有的讀取：載入、失敗與重試在整個應用中有同一種形狀。 */
export interface ApiResource<T> {
  /**
   * 此次登入讀到的最新值。重新請求或重讀時保留，列表才不會閃爍；
   * 讀取失敗（權限可能已被撤銷）、身分改變或請求閒置時會被清掉。寫入回傳儲存後的值時，
   * 在本地直接設定。
   */
  readonly value: WritableSignal<T | undefined>;
  /** 新請求正在進行：第一次讀取，或請求改變。 */
  readonly loading: Signal<boolean>;
  /** 任何讀取正在進行中，包含 `reload()` 與輪詢。 */
  readonly refreshing: Signal<boolean>;
  /** 上次失敗讀取的審閱過訊息；上次成功時為空字串。 */
  readonly error: Signal<string>;
  /**
   * 以目前的請求再讀一次。新請求仍在載入時呼叫（那次載入可能早於某次寫入），
   * 會等該次載入結束後再讀一次，而不是忽略此呼叫。
   */
  reload(): void;
}

/**
 * 包裝 Angular 的 `resource()`，補上所有需要登入的讀取都需要的行為：等待帳號、檢查功能授權、
 * 身分改變時重來（丟棄遲到的回應），並把失敗轉成審閱過的訊息。寫入則維持明確的方法，
 * 更新 `value` 或呼叫 `reload()`。
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
      const required = options.feature;
      if (
        required &&
        !(typeof required === 'string' ? [required] : required).some((id) => session.has(id))
      )
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
  // Angular 在新請求載入時會忽略 reload()；若期間有寫入，會顯示寫入前開始的那次讀取結果，
  // 所以這裡讓 reload 等該次載入結束。
  let pendingReload = false;
  effect(() => {
    const status = ref.status();
    if (!pendingReload || status === 'loading' || status === 'reloading') return;
    pendingReload = false;
    untracked(() => ref.reload());
  });
  const reload = () => {
    if (!ref.reload() && untracked(ref.status) === 'loading') pendingReload = true;
  };
  return { value, loading, refreshing: ref.isLoading, error, reload };
}
