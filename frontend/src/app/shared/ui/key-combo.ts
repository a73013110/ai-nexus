import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { keyCaps } from '../browser/hotkeys';

/**
 * 一組快捷鍵，每個鍵一個 `<kbd>`；`keys` 用 `hotkey()` 的格式（`mod+shift+l`），`mod` 依平台顯示
 * ⌘ 或 Ctrl，所以畫面不必另外說明 macOS 的按法。
 */
@Component({
  selector: 'nx-key-combo',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: inline-flex;
      align-items: center;
      gap: var(--p-space-1);
      flex: none;
      white-space: nowrap;
    }
    kbd {
      min-width: 1.75em;
      padding: 0 var(--p-space-1);
      border: 1px solid var(--line);
      border-bottom-width: 2px;
      border-radius: var(--p-radius-sm);
      background: var(--surface-subtle);
      color: var(--secondary);
      font-size: var(--text-caption);
      line-height: 1.6;
      text-align: center;
    }
  `,
  template: `@for (cap of caps(); track $index) {
    <kbd>{{ cap }}</kbd>
  }`,
})
export class KeyCombo {
  readonly keys = input.required<string>();
  protected readonly caps = computed(() => keyCaps(this.keys()));
}
