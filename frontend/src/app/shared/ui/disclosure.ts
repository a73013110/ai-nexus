import { DestroyRef, Directive, ElementRef, inject } from '@angular/core';

@Directive({
  selector: 'details[nxDisclosure]',
  host: { '(keydown.escape)': 'escape($event)', '(click)': 'activate($event)' },
})
export class Disclosure {
  private readonly element = inject<ElementRef<HTMLDetailsElement>>(ElementRef).nativeElement;
  constructor() {
    const outside = (event: Event) => {
      if (this.element.open && event.target instanceof Node && !this.element.contains(event.target))
        this.element.open = false;
    };
    document.addEventListener('pointerdown', outside);
    inject(DestroyRef).onDestroy(() => document.removeEventListener('pointerdown', outside));
  }
  escape(event: Event) {
    if (!this.element.open) return;
    this.element.open = false;
    this.element.querySelector('summary')?.focus();
    event.stopPropagation();
  }
  activate(event: Event) {
    if (event.target instanceof Element && event.target.closest('button[data-close]'))
      this.element.open = false;
  }
}
