import { motionTiming } from './motion-tokens';

/**
 * 拖曳排序共用的動效（選單編輯表、`SortableRows`）：其他列以 FLIP 從舊位置滑到新位置，放手的列以彈簧落定。
 * 減少動態時由呼叫端略過（`ThemeService.reducedMotion()`）。
 */

/** 落定與換位的彈簧參數，取自動效權杖（`--motion-morph`、`--ease-spring`）。 */
export function settleMotion(): KeyframeAnimationOptions {
  return motionTiming(document.documentElement, '--motion-morph', '--ease-spring');
}

/**
 * 執行 `change`（必須同步更新 DOM，例如在裡面呼叫 `ApplicationRef.tick()`），再讓 `rows()` 中位置變了的元素
 * 從舊位置滑過去。元素以 `key` 對應前後；`except` 的元素不動（被拖曳、正跟著指標走的列）。
 * `before` 給定時從那些位置開始（例如放手前各列讓位後的位置），否則量 `change` 前的位置。
 * 用 `composite: 'add'`，連續換位時動畫疊加，不會跳回起點。
 */
export function flipRows(
  rows: () => Iterable<HTMLElement>,
  key: (row: HTMLElement) => string | undefined,
  change: () => void,
  options: { reduced: boolean; except?: string; before?: ReadonlyMap<string | undefined, number> },
) {
  const before =
    options.before ??
    new Map([...rows()].map((row) => [key(row), row.getBoundingClientRect().top]));
  change();
  if (options.reduced) return;
  const motion = settleMotion();
  for (const row of rows()) {
    const id = key(row),
      old = before.get(id);
    if (old === undefined || id === options.except) continue;
    const dy = old - row.getBoundingClientRect().top;
    if (Math.abs(dy) >= 1)
      row.animate([{ translate: `0 ${dy}px` }, { translate: '0 0' }], {
        ...motion,
        composite: 'add',
      });
  }
}

/**
 * 元素在版面上的上下緣（視窗座標），不含 `translate`（拖曳的位移與 FLIP 動畫）：拖曳時判斷指標在哪一列要用它，
 * 否則讓位中的列位置一直在變，落點會隨動畫進度來回跳。
 * 用畫面位置扣掉計算後的 `translate`（含動畫），不用 `offsetTop`：列一旦 `position: relative`（被拖曳的列要浮起），
 * `offsetParent` 就從 `table` 跳到捲動容器外的祖先，`offsetTop` 不含容器的捲動量，捲動後算出的位置會差一整段。
 * 只認 px 的 `translate`（拖曳與 FLIP 都只寫 px）。
 */
export function layoutBox(element: HTMLElement) {
  const box = element.getBoundingClientRect();
  const top = box.top - translateY(getComputedStyle(element).translate);
  return { top, bottom: top + box.height, height: box.height };
}

/** 計算後 `translate`（`none`、`x` 或 `x y`）的 y 位移，單位 px。 */
function translateY(value: string) {
  const [, y] = value.split(' ');
  return y ? parseFloat(y) || 0 : 0;
}

/** 列內最近的可捲動容器（拖到邊緣時自動捲動用）；找不到時回傳 null。 */
export function scrollParent(element: HTMLElement): HTMLElement | null {
  for (let current = element.parentElement; current; current = current.parentElement) {
    const overflow = getComputedStyle(current).overflowY;
    if (
      (overflow === 'auto' || overflow === 'scroll') &&
      current.scrollHeight > current.clientHeight
    )
      return current;
  }
  return null;
}

/** 指標距容器上下緣 `edge` 以內時的捲動速度（負值往上）；不在邊緣為 0。 */
export function edgeScrollSpeed(box: DOMRect, pointY: number, edge = 48, speed = 12) {
  if (pointY < box.top + edge) return -speed;
  if (pointY > box.bottom - edge) return speed;
  return 0;
}
