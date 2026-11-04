import { computed, inject, signal } from '@angular/core';
import { isCancellation, isStaleWrite, writeFailureMessage } from '@core/errors/safe-errors';
import { ViewScope } from './view-scope';

/**
 * `Action.run` 的結果。失敗的 `error` 是審核過的訊息；`stale` 表示送出的版本已過期（期間有人改過，見 `isStaleWrite`）；
 * `cause` 是原本的錯誤，要依錯誤代碼分流時用（例：`errorCode(outcome.cause)`）。
 * 沒有執行（同一項還在送出中）、被取消，或回來時已離開頁面、換了帳號、換了對象（`current`）：
 * `ok` 為 false、`error` 為空字串，呼叫端什麼都不用做。
 */
export type ActionOutcome<T> =
  { ok: true; value: T } | { ok: false; error: string; stale: boolean; cause?: unknown };

export const ACTION_SKIPPED: ActionOutcome<never> = { ok: false, error: '', stale: false };

export interface ActionRun {
  /** 這次處理的對象（例：`card:<id>`），讓列表只轉那一列（`pendingFor`）。 */
  key?: string;
  /**
   * 送出時的對象是否仍是畫面上的那一個（例：對話框換開了另一筆、訊息換了一則）。
   * 回來時不成立就當作沒發生：不回傳結果、不顯示失敗。
   */
  current?: () => boolean;
}

/** 交給 `work` 的執行狀態。 */
export interface ActionContext {
  /** 仍在同一個頁面、帳號與對象上，且沒有被 `cancel`；執行中途決定要不要接著做（例：成功後才導覽）時用。 */
  alive: () => boolean;
  /** 傳給 `fetch`／API 呼叫，`cancel` 時中斷請求。 */
  signal: AbortSignal;
}

export interface ActionOptions {
  /** 錯誤轉成畫面文字，預設 `writeFailureMessage`。 */
  message?: (error: unknown) => string;
  /** 不同 `key` 可以同時送出（逐列的重試、刪除）；同一個 `key` 仍不重複送出。預設一次只做一件。 */
  concurrent?: boolean;
  /**
   * 送出當下取得、回來時判斷結果還算不算數的守衛。預設是目前視圖的 `ViewScope`（離開頁面或換帳號就作廢）；
   * 不屬於任何視圖的全域服務（例：通知中心）改用自己的帳號守衛。
   */
  guard?: () => () => boolean;
}

type Guard = Pick<ViewScope, 'guard'>;

/**
 * 使用者觸發的一件事（寫入、匯出、重試）：送出中、失敗訊息，以及丟掉離開頁面、換帳號或換對象後才回來的結果。
 * 取代每處手寫的 `saving`／`error` signal、`ViewScope.guard()` 與 try／catch／finally。
 * 送出中再呼叫 `run` 會直接略過，按鈕綁 `pending()` 就不會重複送出。
 * 在提供 `ViewScope` 的元件（或它提供的 store）裡用 `action()` 建立。
 */
export class Action {
  private readonly running = signal<ReadonlySet<string>>(new Set());
  /** 每個 `key` 目前那一次執行；`cancel` 後同一個 `key` 可以馬上再送，舊的那次結束時不會清掉新的。 */
  private readonly runs = new Map<string, AbortController>();
  /** 送出中；綁按鈕的 `nxPending`。 */
  readonly pending = computed(() => this.running().size > 0);
  /** 最近一次失敗的訊息，下一次 `run` 開始時清掉；放進 `Notice`。 */
  readonly error = signal('');

  constructor(
    private readonly scope: Guard,
    private readonly options: ActionOptions = {},
  ) {}

  /** 這一項正在送出（`run` 的 `key`）。 */
  pendingFor(key: string) {
    return this.running().has(key);
  }

  /** 執行 `work`；已離開頁面或換了對象時不執行。被取消的請求不算失敗。 */
  async run<T>(
    work: (context: ActionContext) => Promise<T>,
    { key = '', current = () => true }: ActionRun = {},
  ): Promise<ActionOutcome<T>> {
    if (this.options.concurrent ? this.pendingFor(key) : this.pending()) return ACTION_SKIPPED;
    const controller = new AbortController();
    const guard = this.scope.guard();
    const alive = () => guard() && current() && !controller.signal.aborted;
    // 確認對話框等待期間離開了頁面：不送出。
    if (!alive()) return ACTION_SKIPPED;
    this.runs.set(key, controller);
    this.running.update((keys) => new Set(keys).add(key));
    this.error.set('');
    try {
      const value = await work({ alive, signal: controller.signal });
      return alive() ? { ok: true, value } : ACTION_SKIPPED;
    } catch (error) {
      if (!alive() || isCancellation(error)) return ACTION_SKIPPED;
      const message = (this.options.message ?? writeFailureMessage)(error);
      this.error.set(message);
      return { ok: false, error: message, stale: isStaleWrite(error), cause: error };
    } finally {
      // 換帳號時頁面可能還在：一律結束送出中，不留下轉不停的按鈕。
      if (this.runs.get(key) === controller) this.finish(key);
    }
  }

  /**
   * 中斷送出中的那一次（使用者按停止、換了要產生的內容）：立即結束送出中，晚到的結果與失敗都丟掉。
   * 已送到伺服器的寫入不會因此撤回，所以只用在讀取或產生這類可放棄的工作。
   */
  cancel(key = '') {
    const controller = this.runs.get(key);
    if (!controller) return;
    controller.abort();
    this.finish(key);
  }

  /**
   * 畫面換了對象（同一個元件換網址、換一筆資料）：放棄所有送出中的結果、立即結束送出中並清掉失敗訊息。
   * 已送出的寫入照樣在伺服器完成，只是結果不再套到畫面上。
   */
  reset() {
    for (const key of [...this.runs.keys()]) this.cancel(key);
    this.error.set('');
  }

  private finish(key: string) {
    this.runs.delete(key);
    this.running.update((keys) => {
      const next = new Set(keys);
      next.delete(key);
      return next;
    });
  }

  /** 送出前就知道不能做（例：查詢條件不完整），或結果要另外說明：只顯示原因。 */
  fail(message: string) {
    this.error.set(message);
  }
}

/** 綁定目前視圖（或 `guard`）的 `Action`。 */
export function action(options: ActionOptions = {}): Action {
  const guard = options.guard;
  return new Action(guard ? { guard } : inject(ViewScope), options);
}
