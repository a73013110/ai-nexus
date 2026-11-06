import { DestroyRef, inject, Injectable, signal } from '@angular/core';
import { TRANSIENT_HOLD_MS } from './motion-tokens';

/** 元件範圍的回饋；計時器與待完成的狀態不會比視圖存活更久。 */
@Injectable()
export class CopyFeedback {
  readonly copied = signal(false);
  readonly error = signal('');
  private readonly timers = new Set<ReturnType<typeof setTimeout>>();
  private destroyed = false;
  constructor() {
    inject(DestroyRef).onDestroy(() => {
      this.destroyed = true;
      this.timers.forEach((timer) => clearTimeout(timer));
    });
  }
  async copy(text: string, button?: Element): Promise<void> {
    try {
      await navigator.clipboard.writeText(text);
      if (this.destroyed) return;
      this.error.set('');
      // innerHTML 裡的按鈕（Markdown 程式碼）不在 Angular 的檢視內，直接換文字並標上 data-copied 讓樣式回饋；
      // 報讀名稱由 aria-label 決定，不受影響。
      const label = button?.textContent ?? '';
      if (button) {
        button.textContent = '已複製';
        button.setAttribute('data-copied', '');
      } else this.copied.set(true);
      const timer = setTimeout(() => {
        this.timers.delete(timer);
        if (button) {
          button.textContent = label;
          button.removeAttribute('data-copied');
        } else this.copied.set(false);
      }, TRANSIENT_HOLD_MS);
      this.timers.add(timer);
    } catch {
      if (!this.destroyed) this.error.set('瀏覽器未允許複製，請選取文字後複製。');
    }
  }
}
