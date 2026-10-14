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
let sequence = 0;

/**
 * Shared density and accessible naming; native dialog owns focus and dismissal. Every such dialog
 * is a `FrameMotion`: switching tabs (`nxViewMotion` panels) or `size` moves the frame to the new
 * content's size instead of jumping.
 */
@Directive({
  selector: 'dialog[nxCompactDialog]',
  exportAs: 'nxCompactDialog',
  hostDirectives: [FrameMotion],
  host: {
    '[class.ui-density-compact]': 'compact()',
    '[attr.data-size]': 'size()',
    '(beforetoggle)': 'label($event)',
  },
})
export class CompactDialog {
  readonly compact = input(true, { alias: 'nxCompactDialog', transform: booleanAttribute });
  /** Width and density: sm and md are borderless, lg and full add dividers (dialogs.scss). */
  readonly size = input<DialogSize>('md');
  readonly opened = signal(false);
  private readonly element = inject<ElementRef<HTMLDialogElement>>(ElementRef);
  constructor() {
    const frame = inject(FrameMotion);
    let previous: DialogSize | undefined;
    afterRenderEffect(() => {
      const size = this.size();
      if (previous && size !== previous) frame.morph();
      previous = size;
    });
  }
  label(event: Event) {
    this.opened.set((event as ToggleEvent).newState === 'open');
    if (!this.opened()) return;
    const dialog = this.element.nativeElement;
    if (dialog.hasAttribute('aria-label') || dialog.hasAttribute('aria-labelledby')) return;
    const heading = dialog.querySelector('h2');
    if (!heading) return;
    if (!heading.id) heading.id = `nx-dialog-title-${++sequence}`;
    dialog.setAttribute('aria-labelledby', heading.id);
  }
}
