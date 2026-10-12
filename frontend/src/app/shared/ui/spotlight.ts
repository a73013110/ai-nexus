import { DestroyRef, Directive, ElementRef, inject } from '@angular/core';

/**
 * Follows the pointer over a surface: writes `--spot-x`/`--spot-y` (pixels) and `data-spot` so
 * the surface can paint a light under the cursor. The styles belong to the host; this only
 * measures, once per frame. Touch leaves the surface unlit.
 */
@Directive({ selector: '[nxSpotlight]' })
export class Spotlight {
  private readonly element = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  private frame = 0;
  private point: PointerEvent | null = null;

  constructor() {
    const move = (event: PointerEvent) => {
      if (event.pointerType === 'touch') return;
      this.point = event;
      this.frame ||= requestAnimationFrame(() => this.paint());
    };
    const leave = () => {
      cancelAnimationFrame(this.frame);
      this.frame = 0;
      this.point = null;
      delete this.element.dataset['spot'];
    };
    this.element.addEventListener('pointermove', move, { passive: true });
    this.element.addEventListener('pointerleave', leave, { passive: true });
    inject(DestroyRef).onDestroy(() => {
      cancelAnimationFrame(this.frame);
      this.element.removeEventListener('pointermove', move);
      this.element.removeEventListener('pointerleave', leave);
    });
  }

  private paint() {
    this.frame = 0;
    const event = this.point;
    if (!event) return;
    const box = this.element.getBoundingClientRect();
    const x = event.clientX - box.left,
      y = event.clientY - box.top;
    this.element.dataset['spot'] = '';
    this.element.style.setProperty('--spot-x', `${x}px`);
    this.element.style.setProperty('--spot-y', `${y}px`);
  }
}
