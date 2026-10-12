import { DestroyRef, inject, type Signal, signal } from '@angular/core';

export interface AnchoredPopoverOptions {
  /** The `popover` element; it may come and go with the view. */
  panel: () => HTMLElement | undefined;
  /** Places the open panel against its anchor, e.g. with `positionPopover`. */
  place: (panel: HTMLElement) => void;
  /**
   * Light dismiss for `popover="manual"`: a pointerdown outside `within` (the panel by default) or
   * Escape anywhere calls this. Auto popovers get it from the browser.
   */
  dismiss?: () => void;
  within?: () => Node | undefined;
}

export interface AnchoredPopover {
  readonly opened: Signal<boolean>;
  /** Shows and places the panel; `source` becomes its invoker for Tab order. */
  show(source?: HTMLElement): void;
  hide(): void;
  /** Bind to the panel's `(toggle)` so a native light dismiss updates `opened`. */
  sync(event: Event): void;
  reposition(): void;
}

/**
 * A top-layer panel kept on its anchor: `opened` follows the panel, and while it is open the panel
 * is placed again on resize and on scroll anywhere (once per frame). Focus and keyboard rules stay
 * with the caller. Call it in an injection context; listeners end with the caller.
 */
export function anchoredPopover(options: AnchoredPopoverOptions): AnchoredPopover {
  const opened = signal(false);
  let frame = 0;
  const reposition = () => {
    const panel = opened() && options.panel();
    if (panel) options.place(panel);
  };
  const schedule = () => {
    frame ||= requestAnimationFrame(() => {
      frame = 0;
      reposition();
    });
  };
  const outside = (event: PointerEvent) => {
    const within = options.within?.() ?? options.panel();
    if (event.target instanceof Node && !within?.contains(event.target)) options.dismiss?.();
  };
  const escape = (event: KeyboardEvent) => {
    if (event.key !== 'Escape') return;
    event.preventDefault();
    options.dismiss?.();
  };
  const set = (open: boolean) => {
    if (open === opened()) return;
    opened.set(open);
    if (open) {
      window.addEventListener('resize', schedule);
      window.addEventListener('scroll', schedule, true);
      if (options.dismiss) {
        document.addEventListener('pointerdown', outside);
        document.addEventListener('keydown', escape);
      }
      return;
    }
    window.removeEventListener('resize', schedule);
    window.removeEventListener('scroll', schedule, true);
    document.removeEventListener('pointerdown', outside);
    document.removeEventListener('keydown', escape);
    cancelAnimationFrame(frame);
    frame = 0;
  };
  inject(DestroyRef).onDestroy(() => set(false));
  return {
    opened: opened.asReadonly(),
    show(source) {
      const panel = options.panel();
      if (!panel?.isConnected) return;
      panel.showPopover({ source });
      set(true);
      options.place(panel);
    },
    hide() {
      const panel = options.panel();
      if (panel?.isConnected) panel.hidePopover();
      set(false);
    },
    sync(event) {
      const open = (event as ToggleEvent).newState === 'open';
      const placed = opened();
      set(open);
      if (open && !placed) reposition();
    },
    reposition,
  };
}
