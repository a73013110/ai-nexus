import {
  afterNextRender,
  DestroyRef,
  Directive,
  effect,
  ElementRef,
  inject,
  input,
} from '@angular/core';

/** Uses CSS min/max heights, so typography and design tokens remain authoritative. */
@Directive({ selector: 'textarea[nxAutosize]', host: { '(input)': 'schedule()' } })
export class Autosize {
  readonly nxAutosize = input('');
  private readonly field = inject<ElementRef<HTMLTextAreaElement>>(ElementRef).nativeElement;
  private frame = 0;
  constructor() {
    effect(() => {
      this.nxAutosize();
      this.schedule();
    });
    afterNextRender(() => this.schedule());
    let width = 0;
    const observer = new ResizeObserver((entries) => {
      const next = entries[0]?.contentRect.width ?? 0;
      if (width !== next) {
        width = next;
        this.schedule();
      }
    });
    observer.observe(this.field);
    const resize = () => this.schedule();
    window.addEventListener('resize', resize);
    inject(DestroyRef).onDestroy(() => {
      observer.disconnect();
      window.removeEventListener('resize', resize);
      cancelAnimationFrame(this.frame);
    });
  }
  schedule() {
    cancelAnimationFrame(this.frame);
    this.frame = requestAnimationFrame(() => {
      this.field.style.height = 'auto';
      this.field.style.height = `${this.field.scrollHeight}px`;
    });
  }
}
