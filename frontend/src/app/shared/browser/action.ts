import { computed, inject, signal } from '@angular/core';
import { isStaleWrite, writeFailureMessage } from '@core/errors/safe-errors';
import { ViewScope } from './view-scope';

/**
 * `Action.run` 的結果。失敗的 `error` 是審核過的訊息；`stale` 表示送出的版本已過期（期間有人改過，見 `isStaleWrite`）；
 * `cause` 是原本的錯誤，要依錯誤代碼分流時用（例：`errorCode(outcome.cause)`）。
 * 沒有執行（上一次還在送出中）或執行完時已離開頁面、換了帳號：`ok` 為 false、`error` 為空字串，呼叫端什麼都不用做。
 */
export type ActionOutcome<T> =
  { ok: true; value: T } | { ok: false; error: string; stale: boolean; cause?: unknown };

export const ACTION_SKIPPED: ActionOutcome<never> = { ok: false, error: '', stale: false };

/**
 * 使用者觸發的一件事（寫入、匯出、重試）：送出中、失敗訊息，以及丟掉離開頁面或換帳號後才回來的結果。
 * 取代每處手寫的 `saving`／`error` signal、`ViewScope.guard()` 與 try／catch／finally。
 * 送出中再呼叫 `run` 會直接略過，按鈕綁 `pending()` 就不會重複送出。
 * 在提供 `ViewScope` 的元件（或它提供的 store）裡用 `action()` 建立。
 */
export class Action {
  private readonly running = signal<string | null>(null);
  /** 送出中；綁按鈕的 `nxPending`。 */
  readonly pending = computed(() => this.running() !== null);
  /** 送出中的那一項（`run` 的 `key`，例：`card:<id>`），讓列表只轉正在處理的那一列；沒有指定時為空字串。 */
  readonly key = computed(() => this.running() ?? '');
  /** 最近一次失敗的訊息，下一次 `run` 開始時清掉；放進 `Notice`。 */
  readonly error = signal('');

  constructor(
    private readonly scope: ViewScope,
    private readonly message: (error: unknown) => string = writeFailureMessage,
  ) {}

  async run<T>(work: () => Promise<T>, key = ''): Promise<ActionOutcome<T>> {
    if (this.pending()) return ACTION_SKIPPED;
    const alive = this.scope.guard();
    this.running.set(key);
    this.error.set('');
    try {
      const value = await work();
      return alive() ? { ok: true, value } : ACTION_SKIPPED;
    } catch (error) {
      if (!alive()) return ACTION_SKIPPED;
      const message = this.message(error);
      this.error.set(message);
      return { ok: false, error: message, stale: isStaleWrite(error), cause: error };
    } finally {
      // 換帳號時頁面可能還在：一律結束送出中，不留下轉不停的按鈕。
      this.running.set(null);
    }
  }

  /** 送出前就知道不能做（例：查詢條件不完整）：只顯示原因。 */
  fail(message: string) {
    this.error.set(message);
  }
}

/** 綁定目前視圖的 `Action`；`message` 把錯誤轉成畫面文字，預設是 `writeFailureMessage`。 */
export function action(options: { message?: (error: unknown) => string } = {}): Action {
  return new Action(inject(ViewScope), options.message);
}
