import { Directive, ElementRef, booleanAttribute, inject, input, signal } from '@angular/core';

let sequence = 0;

/** Shared density and accessible naming; native dialog owns focus and dismissal. */
@Directive({
  selector: 'dialog[nxCompactDialog]',
  exportAs: 'nxCompactDialog',
  host: { '[class.ui-density-compact]': 'compact()', '(beforetoggle)': 'label($event)' },
})
export class CompactDialog {
  readonly compact = input(true, { alias: 'nxCompactDialog', transform: booleanAttribute });
  readonly opened = signal(false);
  private readonly element = inject<ElementRef<HTMLDialogElement>>(ElementRef);
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
