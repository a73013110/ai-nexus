/**
 * 圖層的共用規則。對話框、詳情抽屜、浮層、提示與放大的表格都在 top layer：瀏覽器依進入順序疊放，
 * 後開的在上面，不受祖先的 transform、containment、overflow 與 z-index 影響，元件不用 z-index 排圖層。
 */

/** 開著的圖層：對話框（含非模態的詳情抽屜）與 popover（含放大的表格）。 */
export const OPEN_LAYERS = 'dialog[open], :popover-open';

// top layer 的順序沒有 API 可讀，記下每個圖層最後一次進入的序號（對話框與 popover 都有 beforetoggle）。
// 開始記錄前就開著的圖層算 0：它們一定在之後才開的圖層下面。
const entered = new WeakMap<Element, number>();
let sequence = 0;
if (typeof document !== 'undefined')
  document.addEventListener(
    'beforetoggle',
    (event) => {
      if ((event as ToggleEvent).newState === 'open' && event.target instanceof Element)
        entered.set(event.target, ++sequence);
    },
    { capture: true },
  );

/** `layer` 是否疊在 `other` 上面（比 `other` 晚進入 top layer）。 */
export function isAbove(layer: Element, other: Element) {
  return (entered.get(layer) ?? 0) > (entered.get(other) ?? 0);
}

/**
 * 隱藏 popover 時瀏覽器會把裡面的焦點還給開啟前的元素；圖層只是換位置（重新進入、量尺寸）時，
 * 焦點要留在原處。
 */
export function keepingFocus(change: () => void) {
  const focused = document.activeElement;
  change();
  if (focused instanceof HTMLElement && focused.isConnected && document.activeElement !== focused)
    focused.focus({ preventScroll: true });
}

/**
 * 把開著的 popover 移到最上面（重新進入 top layer 就排到最後），例如被放大的表格蓋住的抽屜換了內容、
 * 對話框上方來了新提示。已在最上面就不動；自己裡面的浮層不算蓋住它。
 */
export function bringToFront(layer: HTMLElement) {
  if (!layer.showPopover || !layer.matches(':popover-open')) return;
  const covered = [...document.querySelectorAll(OPEN_LAYERS)].some(
    (open) => !layer.contains(open) && isAbove(open, layer),
  );
  if (!covered) return;
  keepingFocus(() => {
    layer.hidePopover();
    layer.showPopover();
  });
}

/**
 * 這次 Esc 是否輪到 `layer`（呼叫端接著 `preventDefault`，外層就不再處理）：
 * - 已被處理過，或開著不包住 `layer` 的模態對話框、auto popover：交給它們。它們由瀏覽器處理 Esc，
 *   焦點也可能還在觸發鈕上，所以不看焦點。
 * - 焦點在 `layer` 裡：與 `layer` 之間沒有另一個開著的圖層才算（裡面的浮層先處理自己的 Esc）。
 * - 焦點不在任何圖層裡（在頁面上）：也算，非模態的詳情抽屜靠這條在頁面上按 Esc 關閉。
 */
export function ownsEscape(layer: Element, event: Event) {
  if (event.defaultPrevented) return false;
  for (const open of document.querySelectorAll(
    'dialog:modal, :popover-open:is([popover=""], [popover="auto"])',
  ))
    if (!open.contains(layer)) return false;
  const target = event.target instanceof Element ? event.target : null;
  const nearest = target?.closest(OPEN_LAYERS);
  if (!target || !layer.contains(target)) return !nearest;
  return !nearest || nearest === layer || nearest.contains(layer);
}
