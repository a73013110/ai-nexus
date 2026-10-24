import { DestroyRef, inject, type Signal, signal } from '@angular/core';

export interface AnchoredPopoverOptions {
  /** `popover` 元素；它可能隨視圖出現或消失。 */
  panel: () => HTMLElement | undefined;
  /** 把開啟的面板對齊到其錨點，例如使用 `positionPopover`。 */
  place: (panel: HTMLElement) => void;
  /**
   * 開關面板的觸發鈕：`show()` 沒給 `source` 時用它當 Tab 順序來源。它也是面板的錨點，
   * 按下它不會先被輕關閉、接著 click 又把面板打開；`closeOnFocusOut` 時焦點移到它不算離開。
   */
  trigger?: () => HTMLElement | undefined;
  /** 焦點移出面板與 `trigger` 就關閉（Tab 離開）；popover="auto" 只處理點擊外面與 Escape。 */
  closeOnFocusOut?: boolean;
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
  /** 顯示並放置面板；`source`（預設 `trigger`）成為它的 Tab 順序觸發者。 */
  show(source?: HTMLElement): void;
  hide(): void;
  /** 綁定到面板的 `(toggle)`，讓原生的輕關閉能更新 `opened`。 */
  sync(event: Event): void;
  reposition(): void;
}

/** HTML 的互動內容：點在這些元素上，外層 `<label>` 本來就不會轉送點擊。 */
const interactive =
  'a[href], button, input, select, textarea, label, summary, iframe, [contenteditable]';

/**
 * 固定在錨點上的最上層面板：`opened` 跟隨面板，開啟期間在縮放與任何地方捲動時（每幀一次）會重新放置。
 * 面板在 DOM 上仍在呼叫端的位置，開啟期間另外保證兩件事：
 * - 點在面板的空白或純文字上，焦點留在面板裡（面板 `tabindex="-1"`），不會落到外層可聚焦的
 *   `<main>`，被當成焦點離開而關閉。
 * - 外層是 `<label>`（欄位標籤包住整個選擇器）時，點在面板的非互動處不會被 label 轉送成
 *   觸發鈕的 click，面板不會跟著關閉或重新打開。
 * 其餘焦點與鍵盤規則仍由呼叫端決定。在 injection context 中呼叫；監聽器隨呼叫端結束。
 */
export function anchoredPopover(options: AnchoredPopoverOptions): AnchoredPopover {
  const opened = signal(false);
  let frame = 0;
  let guarded: HTMLElement | undefined;
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
  // 捕獲階段：面板裡的 stopPropagation（例如樹的展開箭頭）擋不住 label 的預設動作，這裡要先攔。
  const label = (event: MouseEvent) => {
    const control = event.target instanceof Element && event.target.closest(interactive);
    if (control && !guarded?.contains(control)) event.preventDefault();
  };
  const focusOut = (event: FocusEvent) => {
    const next = event.relatedTarget;
    // 沒有 relatedTarget 是切換視窗或焦點交給瀏覽器，回來時面板應該還在。
    if (!(next instanceof Node) || guarded?.contains(next)) return;
    if (options.trigger?.()?.contains(next)) return;
    hide();
  };
  const guard = (panel: HTMLElement | undefined) => {
    guarded?.removeEventListener('click', label, true);
    guarded?.removeEventListener('focusout', focusOut);
    guarded = panel;
    if (!panel) return;
    if (!panel.hasAttribute('tabindex')) panel.tabIndex = -1;
    panel.addEventListener('click', label, true);
    if (options.closeOnFocusOut) panel.addEventListener('focusout', focusOut);
  };
  const set = (open: boolean) => {
    if (open === opened()) return;
    opened.set(open);
    if (open) {
      guard(options.panel());
      window.addEventListener('resize', schedule);
      window.addEventListener('scroll', schedule, true);
      if (options.dismiss) {
        document.addEventListener('pointerdown', outside);
        document.addEventListener('keydown', escape);
      }
      return;
    }
    guard(undefined);
    window.removeEventListener('resize', schedule);
    window.removeEventListener('scroll', schedule, true);
    document.removeEventListener('pointerdown', outside);
    document.removeEventListener('keydown', escape);
    cancelAnimationFrame(frame);
    frame = 0;
  };
  const hide = () => {
    const panel = options.panel();
    if (panel?.isConnected) panel.hidePopover();
    set(false);
  };
  inject(DestroyRef).onDestroy(() => set(false));
  return {
    opened: opened.asReadonly(),
    show(source = options.trigger?.()) {
      const panel = options.panel();
      if (!panel?.isConnected) return;
      panel.showPopover({ source });
      set(true);
      options.place(panel);
    },
    hide,
    sync(event) {
      const open = (event as ToggleEvent).newState === 'open';
      const placed = opened();
      set(open);
      if (open && !placed) reposition();
    },
    reposition,
  };
}
