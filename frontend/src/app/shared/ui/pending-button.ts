import { ChangeDetectionStrategy, Component, ElementRef, inject, input } from '@angular/core';
import { Icon } from './icon';

/**
 * 送出中的按鈕：`<button nxPending [pending]="saving()" class="primary-button">`，`type` 由呼叫端決定
 * （表單的送出鈕仍是 submit）。送出中前導圖示換成轉圈（沒有圖示的按鈕在文字前加轉圈），並設
 * `aria-busy` 與 `aria-disabled`；不用 `disabled`，焦點才留在剛按下的按鈕。文字由呼叫端換成「動詞＋中…」。
 * 送出中攔下 click，包含表單裡按 Enter 隱式送出時瀏覽器對送出鈕觸發的 click，所以不會重複送出。
 */
@Component({
  selector: 'button[nxPending]',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    '[class.is-pending]': 'pending()',
    '[attr.aria-busy]': 'pending() || null',
    '[attr.aria-disabled]': 'pending() || null',
  },
  // 投影進來的前導圖示由 controls.scss 的 `.pending-icon + nx-icon` 隱藏（它屬於呼叫端的檢視，元件樣式選不到）。
  template: `@if (pending()) {
      <nx-icon name="loading" class="pending-icon" />
    }
    <ng-content />`,
})
export class PendingButton {
  /** 動作進行中；按下之後到完成或失敗都算在內。 */
  readonly pending = input(false);

  constructor() {
    // 呼叫端在同一元素上的 `(click)` 比元件的 host 監聽先註冊，所以改在捕獲階段攔：
    // 目標元素上的捕獲監聽先於一般監聽執行，`stopImmediatePropagation` 才擋得住它；
    // `preventDefault` 擋下送出鈕的表單送出。
    inject<ElementRef<HTMLButtonElement>>(ElementRef).nativeElement.addEventListener(
      'click',
      (event) => {
        if (!this.pending()) return;
        event.preventDefault();
        event.stopImmediatePropagation();
      },
      { capture: true },
    );
  }
}
