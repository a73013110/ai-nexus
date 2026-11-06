/**
 * 元素目前的退場動畫（transition、animation）都結束後 resolve；沒有動畫（減少動態、單元測試）時立即 resolve。
 * 宿主要等退場結束才清空內容時用，例如關閉的對話框不會帶著空白退場。
 * 在觸發退場的變更（`dialog.close()`、`hidePopover()`）之後呼叫：退場的 transition 在樣式重新計算後
 * 才出現在 `getAnimations()`，而讀取本身會觸發重新計算。
 */
export function exitFinished(element: Element): Promise<void> {
  const animations = element.getAnimations?.() ?? [];
  return Promise.allSettled(animations.map((animation) => animation.finished)).then(
    () => undefined,
  );
}
