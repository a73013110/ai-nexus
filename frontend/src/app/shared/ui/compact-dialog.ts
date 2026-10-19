import { uniqueId } from '../browser/unique-id';
import {
  Directive,
  ElementRef,
  afterRenderEffect,
  booleanAttribute,
  inject,
  input,
  signal,
} from '@angular/core';
import { FrameMotion } from './view-motion';

export type DialogSize = 'sm' | 'md' | 'lg' | 'full';

/**
 * 共用的密度與無障礙命名；原生 dialog 負責焦點與關閉。每個這樣的對話框都是 `FrameMotion`：
 * 切換分頁（`nxViewMotion` 面板）、`size` 或 `maximized` 時，框架會過渡到新內容的大小，而非直接跳動。
 * `maximized`（邊到邊，`data-maximized`）是
 * toggled by the header's 放大視窗 button and resets on every open.
 */
@Directive({
  selector: 'dialog[nxCompactDialog]',
  exportAs: 'nxCompactDialog',
  hostDirectives: [FrameMotion],
  host: {
    '[class.ui-density-compact]': 'compact()',
    '[attr.data-size]': 'size()',
    '[attr.data-maximized]': 'maximized() ? "" : null',
    '(beforetoggle)': 'label($event)',
  },
})
export class CompactDialog {
  readonly compact = input(true, { alias: 'nxCompactDialog', transform: booleanAttribute });
  /** 寬度與密度：sm 與 md 沒有邊框，lg 與 full 會加上分隔線（dialogs.scss）。 */
  readonly size = input<DialogSize>('md');
  readonly opened = signal(false);
  readonly maximized = signal(false);
  private readonly element = inject<ElementRef<HTMLDialogElement>>(ElementRef);
  constructor() {
    const frame = inject(FrameMotion);
    let previous: string | undefined;
    afterRenderEffect(() => {
      const shape = `${this.size()} ${this.maximized()}`;
      if (previous && shape !== previous) frame.morph();
      previous = shape;
    });
  }
  label(event: Event) {
    this.opened.set((event as ToggleEvent).newState === 'open');
    if (!this.opened()) return;
    this.maximized.set(false);
    const dialog = this.element.nativeElement;
    if (dialog.hasAttribute('aria-label') || dialog.hasAttribute('aria-labelledby')) return;
    const heading = dialog.querySelector('h2');
    if (!heading) return;
    if (!heading.id) heading.id = uniqueId('nx-dialog-title');
    dialog.setAttribute('aria-labelledby', heading.id);
  }
}
