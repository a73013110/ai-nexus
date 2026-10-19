import { DestroyRef, effect, inject, signal, untracked, type Signal } from '@angular/core';

export interface MorphSize {
  width: number;
  height: number;
}

/**
 * 動態島的變形：量測內層內容（`width: max-content`）的邊框尺寸，外框把寬高設成這個值，
 * 再由 CSS transition 以彈簧過渡，因此不依賴 `interpolate-size`。外框要 `overflow: clip`，
 * 內容變大時先被裁切、隨外框展開露出。沒有元素或沒有 ResizeObserver（單元測試）時為 `null`，
 * 外框退回自動尺寸。需在 injection context 中呼叫。
 */
export function morphSize(content: () => HTMLElement | undefined): Signal<MorphSize | null> {
  const size = signal<MorphSize | null>(null);
  if (typeof ResizeObserver === 'undefined') return size;
  const observer = new ResizeObserver(([entry]) => {
    const box = entry.borderBoxSize?.[0];
    if (box) size.set({ width: box.inlineSize, height: box.blockSize });
  });
  effect((onCleanup) => {
    const element = content();
    if (!element) return untracked(() => size.set(null));
    observer.observe(element);
    onCleanup(() => observer.unobserve(element));
  });
  inject(DestroyRef).onDestroy(() => observer.disconnect());
  return size;
}
