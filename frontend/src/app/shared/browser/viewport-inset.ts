import { Directive, ElementRef, afterRenderEffect, inject, input } from '@angular/core';

/** Keep workspace height and overlay offsets aligned with a responsive global banner. */
@Directive({ selector: '[nxViewportInset]' })
export class ViewportInset {
  readonly property = input.required<'--global-issue-height' | '--identity-banner-height'>({
    alias: 'nxViewportInset',
  });
  private readonly element = inject<ElementRef<HTMLElement>>(ElementRef);
  constructor() {
    afterRenderEffect((onCleanup) => {
      const property = this.property();
      const style = document.documentElement.style;
      let frame = 0;
      const measure = () => {
        const height = `${this.element.nativeElement.getBoundingClientRect().height}px`;
        if (style.getPropertyValue(property) !== height) style.setProperty(property, height);
      };
      measure();
      const observer = new ResizeObserver(() => {
        cancelAnimationFrame(frame);
        frame = requestAnimationFrame(measure);
      });
      observer.observe(this.element.nativeElement);
      onCleanup(() => {
        observer.disconnect();
        cancelAnimationFrame(frame);
        style.removeProperty(property);
      });
    });
  }
}
