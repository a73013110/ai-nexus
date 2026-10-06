import { afterRenderEffect, Directive, ElementRef, inject, input } from '@angular/core';

/** Reveal an explicitly linked resource once; polling must preserve the reader's position. */
@Directive({
  selector: '[nxResourceTarget]',
  host: { tabindex: '-1' },
})
export class ResourceTarget {
  readonly active = input(false, { alias: 'nxResourceTarget' });
  private readonly element = inject<ElementRef<HTMLElement>>(ElementRef);

  constructor() {
    let activated = false;
    afterRenderEffect(() => {
      const active = this.active();
      if (active && !activated) {
        this.element.nativeElement.focus({ preventScroll: true });
        this.element.nativeElement.scrollIntoView({ block: 'start', behavior: 'instant' });
      }
      activated = active;
    });
  }
}
