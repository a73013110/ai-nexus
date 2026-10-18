import { computed, signal } from '@angular/core';
import { hasScripts } from './html-scripts';

/** HTML 預覽是否需要腳本提示，以及使用者是否選擇執行腳本。 */
export class HtmlScriptGate {
  /** HTML 文字尚未檢查前為 null。 */
  readonly scripts = signal<boolean | null>(null);
  readonly allowed = signal(false);
  readonly gated = computed(() => this.scripts() === true && !this.allowed());
  readonly running = computed(() => this.scripts() === true && this.allowed());
  inspect(html: string) {
    this.scripts.set(hasScripts(html));
  }
  run() {
    if (this.scripts()) this.allowed.set(true);
  }
  reset() {
    this.scripts.set(null);
    this.allowed.set(false);
  }
}
