import { afterRenderEffect, DestroyRef, Directive, ElementRef, inject, input } from '@angular/core';
import { ThemeService } from '../../core/preferences/theme-service';

function timing(element: HTMLElement) {
  const style = getComputedStyle(element);
  return {
    duration: parseFloat(style.getPropertyValue('--motion-panel')) || 240,
    easing: style.getPropertyValue('--ease').trim() || 'ease-out',
  };
}

/** A keyed panel transition keeps tabs and their keyboard focus stationary. */
@Directive({ selector: '[nxViewMotion]' })
export class ViewMotion {
  readonly view = input.required<unknown>({ alias: 'nxViewMotion' });
  private readonly element = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  private readonly theme = inject(ThemeService);
  private animation?: Animation;
  private previous: unknown;
  private initialized = false;
  constructor() {
    afterRenderEffect(() => {
      const view = this.view(),
        reduced = this.theme.reducedMotion();
      const changed = this.initialized && view !== this.previous;
      this.previous = view;
      this.initialized = true;
      this.animation?.cancel();
      if (!changed || reduced || !this.element.getClientRects().length) return;
      this.animation = this.element.animate(
        [
          { opacity: 0, transform: 'translateY(6px) scale(.985)' },
          { opacity: 1, transform: 'none' },
        ],
        timing(this.element),
      );
    });
    inject(DestroyRef).onDestroy(() => this.animation?.cancel());
  }
}

/** Measure only on view changes; native dialog retains focus, Escape and viewport constraints. */
@Directive({ selector: 'dialog[nxDialogView]', host: { '(close)': 'closed()' } })
export class DialogMotion {
  readonly view = input.required<unknown>({ alias: 'nxDialogView' });
  private readonly element = inject<ElementRef<HTMLDialogElement>>(ElementRef).nativeElement;
  private readonly theme = inject(ThemeService);
  private animation?: Animation;
  private previous: unknown;
  private rectangle?: DOMRect;
  private initialized = false;
  closed() {
    this.animation?.cancel();
    this.rectangle = undefined;
  }
  constructor() {
    const observer = new ResizeObserver(() => {
      if (this.element.open && this.animation?.playState !== 'running')
        this.rectangle = this.element.getBoundingClientRect();
    });
    observer.observe(this.element);
    afterRenderEffect(() => {
      const view = this.view(),
        reduced = this.theme.reducedMotion();
      const changed = this.initialized && view !== this.previous;
      this.previous = view;
      this.initialized = true;
      const before =
        this.animation?.playState === 'running'
          ? this.element.getBoundingClientRect()
          : this.rectangle;
      this.animation?.cancel();
      const after = this.element.getBoundingClientRect();
      this.rectangle = after;
      if (
        !changed ||
        reduced ||
        !this.element.open ||
        !before?.width ||
        !before.height ||
        (Math.abs(before.width - after.width) < 1 && Math.abs(before.height - after.height) < 1)
      )
        return;
      this.animation = this.element.animate(
        [
          { width: `${before.width}px`, height: `${before.height}px` },
          { width: `${after.width}px`, height: `${after.height}px` },
        ],
        timing(this.element),
      );
    });
    inject(DestroyRef).onDestroy(() => {
      observer.disconnect();
      this.animation?.cancel();
    });
  }
}
