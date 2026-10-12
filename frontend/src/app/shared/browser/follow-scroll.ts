import { DestroyRef, inject, linkedSignal, type WritableSignal } from '@angular/core';
import { ThemeService } from '../../core/preferences/theme-service';

export interface FollowScrollOptions {
  /** The scrolling element; it may come and go with the view. */
  viewport: () => HTMLElement | undefined;
  /** Whether to follow from the start; recomputed when the signals it reads change. */
  initial?: () => boolean;
  /** Distance from the bottom, in pixels, that still counts as reading the latest. */
  threshold?: number;
}

export interface FollowScroll {
  /** True while growing content keeps the view at the bottom. */
  readonly following: WritableSignal<boolean>;
  /** Content grew: scrolls to the bottom on the next frame while following, once per frame. */
  schedule(): void;
  /** Bind to the viewport's `(scroll)`: reaching the bottom resumes following, leaving pauses. */
  scrolled(): void;
  /** Stops following where the view is, e.g. before jumping elsewhere in the content. */
  pause(): void;
  /** Scrolls to the bottom and follows from there; `smooth` honours reduced motion. */
  toLatest(smooth?: boolean): void;
  /** Records a position set in code, so the next scroll event compares against it. */
  mark(): void;
}

/**
 * Stick-to-bottom for streaming views (chat answers, logs): new content keeps the newest line in
 * view until the reader scrolls up, and scrolling back to the bottom resumes it. Call it in an
 * injection context; a pending frame ends with the caller.
 */
export function followScroll(options: FollowScrollOptions): FollowScroll {
  const theme = inject(ThemeService);
  const threshold = options.threshold ?? 96;
  const following = linkedSignal(options.initial ?? (() => true));
  let frame = 0;
  let lastTop = 0;
  const cancel = () => {
    cancelAnimationFrame(frame);
    frame = 0;
  };
  const toLatest = (smooth = true) => {
    const view = options.viewport();
    following.set(true);
    view?.scrollTo({
      top: view.scrollHeight,
      behavior: smooth ? theme.scrollBehavior() : 'instant',
    });
  };
  inject(DestroyRef).onDestroy(cancel);
  return {
    following,
    schedule() {
      if (!following() || frame) return;
      frame = requestAnimationFrame(() => {
        frame = 0;
        if (following()) toLatest(false);
      });
    },
    scrolled() {
      const view = options.viewport();
      if (!view) return;
      const near = view.scrollHeight - view.scrollTop - view.clientHeight < threshold;
      // A smooth jump upwards begins near the bottom; that first event must not resume following.
      following.set(near && (following() || view.scrollTop > lastTop));
      lastTop = view.scrollTop;
      if (!near) cancel();
    },
    pause() {
      following.set(false);
      cancel();
      lastTop = options.viewport()?.scrollTop ?? 0;
    },
    toLatest,
    mark() {
      lastTop = options.viewport()?.scrollTop ?? 0;
    },
  };
}
