import { DestroyRef, inject, Injectable, signal } from '@angular/core';

/** Component-scoped feedback; timers and pending completion never outlive the view. */
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
      if (button) button.textContent = '已複製';
      else this.copied.set(true);
      const timer = setTimeout(() => {
        this.timers.delete(timer);
        if (button) button.textContent = '複製程式碼';
        else this.copied.set(false);
      }, 1800);
      this.timers.add(timer);
    } catch {
      if (!this.destroyed) this.error.set('瀏覽器未允許複製，請選取文字後複製。');
    }
  }
}
