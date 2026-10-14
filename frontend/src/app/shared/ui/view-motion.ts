import {
  afterRenderEffect,
  DestroyRef,
  Directive,
  ElementRef,
  inject,
  Injector,
  input,
} from '@angular/core';
import { ThemeService } from '@core/preferences/theme-service';

function timing(element: HTMLElement) {
  const style = getComputedStyle(element);
  return {
    duration: parseFloat(style.getPropertyValue('--motion-panel')) || 240,
    easing: style.getPropertyValue('--ease').trim() || 'ease-out',
  };
}

/** Layout size, unaffected by the dialog's own open/close scale. */
function size(element: HTMLElement) {
  return { width: element.offsetWidth, height: element.offsetHeight };
}

/** Shown: an open dialog or popover; a closing one is still painted but no longer counts. */
function shown(element: HTMLElement) {
  return element instanceof HTMLDialogElement ? element.open : element.matches(':popover-open');
}

const frames = new WeakMap<Element, FrameMotion>();

/** The nearest frame around an element, if any. */
function frameAround(element: Element) {
  for (let node = element.parentElement; node; node = node.parentElement) {
    const frame = frames.get(node);
    if (frame) return frame;
  }
  return undefined;
}

/**
 * A keyed panel transition keeps tabs and their keyboard focus stationary. Inside a frame (every
 * `nxCompactDialog`, see `FrameMotion`) the frame follows the new view's size at the same time.
 */
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
      if (changed) frameAround(this.element)?.morph();
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

/**
 * A dialog or popover whose size follows its content: when a view inside changes (`ViewMotion`) or
 * its owner calls `morph()` after a render, it moves from the size it had to the new one instead
 * of jumping. Width and height come from the content and CSS, so a frame needs no fixed height to
 * keep tabs still. It remembers its laid-out size only while shown and forgets it on close, so
 * opening never animates; the native dialog keeps focus, Escape and viewport limits.
 */
@Directive({
  selector: '[nxFrameMotion]',
  host: { '(close)': 'forget()', '(toggle)': 'toggled($event)' },
})
export class FrameMotion {
  private readonly element = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  /** Read when morphing: every dialog is a frame, including ones in unit tests without layout. */
  private readonly injector = inject(Injector);
  private animation?: Animation;
  private rectangle?: { width: number; height: number };
  private readonly observer =
    typeof ResizeObserver === 'undefined'
      ? undefined
      : new ResizeObserver(() => {
          if (shown(this.element) && this.animation?.playState !== 'running')
            this.rectangle = size(this.element);
        });
  constructor() {
    if (this.observer) {
      this.observer.observe(this.element);
      frames.set(this.element, this);
    }
    inject(DestroyRef).onDestroy(() => {
      this.observer?.disconnect();
      this.animation?.cancel();
    });
  }
  toggled(event: Event) {
    if ((event as ToggleEvent).newState === 'closed') this.forget();
  }
  forget() {
    this.animation?.cancel();
    this.rectangle = undefined;
  }
  /** Call after the render that changed the content; the size before comes from the observer. */
  morph() {
    if (!this.observer) return;
    if (!shown(this.element)) return this.forget();
    const before = this.animation?.playState === 'running' ? size(this.element) : this.rectangle;
    this.animation?.cancel();
    const after = size(this.element);
    this.rectangle = after;
    if (
      this.injector.get(ThemeService).reducedMotion() ||
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
  }
}
