import { computed, signal } from '@angular/core';
import { hasScripts } from './html-scripts';

/** Whether an HTML preview needs the script notice, and whether the user chose to run scripts. */
export class HtmlScriptGate {
  /** null until the HTML text has been inspected. */
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
