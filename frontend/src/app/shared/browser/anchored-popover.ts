import { DestroyRef, inject, type Signal, signal } from '@angular/core';

export interface AnchoredPopoverOptions {
  /** `popover` 元素；它可能隨視圖出現或消失。 */
  panel: () => HTMLElement | undefined;
  /** 把開啟的面板對齊到其錨點，例如使用 `positionPopover`。 */
  place: (panel: HTMLElement) => void;
  /**
   * `popover="manual"` 的輕關閉：在 `within`（預設為面板）以外按下指標，或在任何地方按 Escape，
   * 都會呼叫此函式。
   * 自動 popover 則由瀏覽器處理。
   */
  dismiss?: () => void;
  within?: () => Node | undefined;
}

export interface AnchoredPopover {
  readonly opened: Signal<boolean>;
  /** 顯示並放置面板；`source` 成為它的 Tab 順序觸發者。 */
  show(source?: HTMLElement): void;
  hide(): void;
  /** 綁定到面板的 `(toggle)`，讓原生的輕關閉能更新 `opened`。 */
  sync(event: Event): void;
  reposition(): void;
}

/**
 * 固定在錨點上的最上層面板：`opened` 跟隨面板，開啟期間在縮放與任何地方捲動時（每幀一次）會重新放置。
 * 焦點與鍵盤規則仍由呼叫端決定。在 injection context 中呼叫；監聽器隨呼叫端結束。
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
