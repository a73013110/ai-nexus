import { DestroyRef, Directive, ElementRef, inject, input } from '@angular/core';
import { ThemeService } from '../../core/preferences/theme-service';

/**
 * Follows the pointer over a surface: writes `--spot-x`/`--spot-y` (pixels) and `data-spot` so
 * the surface can paint a light under the cursor, and with `tilt` leans it slightly toward the
 * pointer through `--tilt-x`/`--tilt-y` (degrees). The styles belong to the host; this only
 * measures, once per frame. Touch and reduced motion leave the surface flat.
 */
@Directive({ selector: '[nxSpotlight]' })
export class Spotlight {
  /** Maximum lean in degrees; 0 keeps the surface flat. */
  readonly tilt = input(0, {
    alias: 'nxSpotlight',
    transform: (value: number | string) => Number(value) || 0,
  });
  private readonly element = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  private readonly theme = inject(ThemeService);
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
      this.element.style.removeProperty('--tilt-x');
      this.element.style.removeProperty('--tilt-y');
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
    const tilt = this.tilt();
    if (!tilt || this.theme.reducedMotion() || !box.width || !box.height) return;
    this.element.style.setProperty('--tilt-x', `${((0.5 - y / box.height) * tilt).toFixed(2)}deg`);
    this.element.style.setProperty('--tilt-y', `${((x / box.width - 0.5) * tilt).toFixed(2)}deg`);
  }
}
