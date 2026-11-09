/**
 * 浮動面板（`FloatingPanel`）的位置與大小，全是純函式。座標以視窗左上為原點、單位 px。
 * 位置記的是「貼著哪一側、離那一側多遠」（`Anchor`），視窗變大變小時仍貼著同一側，
 * 不會因為存的是絕對座標而漂到畫面中間；動態島長大時也往遠離那一側的方向長。
 * 放在水平中間的（吸附到中線）記成 `center`，視窗變寬時仍在中間。
 */

/** 頂端跑道的深度與島之間的間距；比這更下面的島不會碰到跑道。 */
const LANE_DEPTH = 72;
const LANE_GAP = 8;

export interface Size {
  width: number;
  height: number;
}

export interface Rect extends Size {
  x: number;
  y: number;
}

export interface Anchor {
  /**
   * 水平方向貼著左（start）或右（end），`inline` 是與那一邊的距離；
   * `center` 對齊視窗中線，`inline` 是中心點偏離中線的距離（往右為正）。
   */
  side: 'start' | 'center' | 'end';
  inline: number;
  /** 垂直方向貼著上（top）或下（bottom），`block` 是與那一邊的距離。 */
  edge: 'top' | 'bottom';
  block: number;
}

export type FloatingPlacement = Anchor & Size;

/** 調整大小的把手：左、右、下緣與兩個下角（上緣是拖動用的標題列）。 */
export type ResizeEdge = 'start' | 'end' | 'bottom' | 'bottom-start' | 'bottom-end';

/** 與視窗邊緣的間距、放開時吸附的距離，以及面板的最小尺寸。 */
export const FLOATING_LIMITS = { margin: 16, snap: 48, minWidth: 320, minHeight: 360 } as const;

/** 第一次開啟：貼右上，寬 400、高最多 640。 */
export function defaultPlacement(viewport: Size): FloatingPlacement {
  const { margin } = FLOATING_LIMITS;
  return {
    side: 'end',
    inline: margin,
    edge: 'top',
    block: margin,
    width: 400,
    height: Math.min(640, viewport.height - 2 * margin),
  };
}

/** 貼著上緣或下緣、水平置中的錨（常駐的島：桌面在頂端，手機在底部的拇指區）。 */
export function centered(edge: Anchor['edge']): Anchor {
  return { side: 'center', inline: 0, edge, block: FLOATING_LIMITS.margin };
}

/** 以中心點較靠近的那一側為錨；中心點離中線不到吸附距離就以中線為錨。 */
export function anchorOf(rect: Rect, viewport: Size): Anchor {
  const offset = rect.x + rect.width / 2 - viewport.width / 2;
  const bottom = rect.y + rect.height / 2 > viewport.height / 2;
  const vertical = {
    edge: bottom ? 'bottom' : 'top',
    block: bottom ? viewport.height - rect.y - rect.height : rect.y,
  } as const;
  if (Math.abs(offset) < FLOATING_LIMITS.snap)
    return { side: 'center', inline: offset, ...vertical };
  const end = offset > 0;
  return {
    side: end ? 'end' : 'start',
    inline: end ? viewport.width - rect.x - rect.width : rect.x,
    ...vertical,
  };
}

export function placementOf(rect: Rect, viewport: Size): FloatingPlacement {
  return { ...anchorOf(rect, viewport), width: rect.width, height: rect.height };
}

/** 依錨放到目前的視窗，超出邊緣（含 `margin`）就推回來。 */
export function place(anchor: Anchor, size: Size, viewport: Size): Rect {
  const { margin } = FLOATING_LIMITS;
  const x =
    anchor.side === 'end'
      ? viewport.width - anchor.inline - size.width
      : anchor.side === 'center'
        ? (viewport.width - size.width) / 2 + anchor.inline
        : anchor.inline;
  const y = anchor.edge === 'bottom' ? viewport.height - anchor.block - size.height : anchor.block;
  return {
    x: clamp(x, margin, viewport.width - size.width - margin),
    y: clamp(y, margin, viewport.height - size.height - margin),
    ...size,
  };
}

/** 島若落在頂端中央被佔用的範圍內，就讓到右邊（放不下改左邊）；拖到別處的島不受影響。 */
export function clearOfLane(rect: Rect, laneWidth: number, viewport: Size): Rect {
  const { margin } = FLOATING_LIMITS;
  const start = (viewport.width - laneWidth) / 2 - LANE_GAP;
  const end = start + laneWidth + LANE_GAP * 2;
  if (!laneWidth || rect.y > LANE_DEPTH || rect.x + rect.width <= start || rect.x >= end)
    return rect;
  const x = end + rect.width + margin <= viewport.width ? end : start - rect.width;
  return { ...rect, x: Math.max(margin, x) };
}

/** 面板放到目前的視窗：太大就縮到放得下。 */
export function rectOf(placement: FloatingPlacement, viewport: Size): Rect {
  const { margin, minWidth, minHeight } = FLOATING_LIMITS;
  return place(
    placement,
    {
      width: fit(placement.width, minWidth, viewport.width - 2 * margin),
      height: fit(placement.height, minHeight, viewport.height - 2 * margin),
    },
    viewport,
  );
}

/** 按下時指標落在矩形內的相對位置（左上 0、右下 1）。 */
export interface Grip {
  x: number;
  y: number;
}

export function gripOf(rect: Rect, x: number, y: number): Grip {
  return { x: (x - rect.x) / rect.width, y: (y - rect.y) / rect.height };
}

/**
 * 拖動中：按住的那一點留在指標下，整個矩形留在視窗內。以相對位置而不是位移量計算，
 * 拖動中內容改變大小（例：島上的狀態文字變長變短）時，指標仍落在原本按住的地方。
 */
export function moved(size: Size, grip: Grip, x: number, y: number, viewport: Size): Rect {
  const { width, height } = size;
  return {
    x: clamp(x - grip.x * width, 0, viewport.width - width),
    y: clamp(y - grip.y * height, 0, viewport.height - height),
    width,
    height,
  };
}

/** 放開時：離邊緣不到 `snap` 就貼齊到 `margin`，水平方向離中線不到 `snap` 就置中，其餘停在原處。 */
export function snapped(rect: Rect, viewport: Size): Rect {
  const { margin, snap } = FLOATING_LIMITS;
  const toward = (start: number, size: number, extent: number) =>
    start < snap ? margin : extent - start - size < snap ? extent - size - margin : start;
  const middle = (viewport.width - rect.width) / 2;
  const x = toward(rect.x, rect.width, viewport.width);
  return {
    ...rect,
    x: x === rect.x && Math.abs(rect.x - middle) < snap ? middle : x,
    y: toward(rect.y, rect.height, viewport.height),
  };
}

/** 拉邊緣調整大小：拉的那一邊跟著指標，對邊不動；不小於下限、不超出視窗。 */
export function resized(
  start: Rect,
  edge: ResizeEdge,
  dx: number,
  dy: number,
  viewport: Size,
): Rect {
  const { margin, minWidth, minHeight } = FLOATING_LIMITS;
  let { x, width } = start;
  if (edge === 'start' || edge === 'bottom-start') {
    const right = start.x + start.width;
    width = fit(start.width - dx, minWidth, right - margin);
    x = right - width;
  } else if (edge === 'end' || edge === 'bottom-end')
    width = fit(start.width + dx, minWidth, viewport.width - start.x - margin);
  const height = edge.startsWith('bottom')
    ? fit(start.height + dy, minHeight, viewport.height - start.y - margin)
    : start.height;
  return { x, y: start.y, width, height };
}

/**
 * 縮成動態島：收向面板下緣、較靠近的那一側（貼右的面板收到右下角），島以那個角為錨；
 * 展開時（`expandFrom`）從同一個角長回去，看得出兩者是同一個東西。
 */
export function collapseTo(panel: Rect, viewport: Size): Anchor {
  const end = panel.x + panel.width / 2 > viewport.width / 2;
  return {
    side: end ? 'end' : 'start',
    inline: end ? viewport.width - panel.x - panel.width : panel.x,
    edge: 'bottom',
    block: viewport.height - panel.y - panel.height,
  };
}

/** 從島長出面板：對齊島所錨定的那一側與那一緣（頂端的島往下長、右下的島往左上長、置中的往兩側長）。 */
export function expandFrom(island: Rect, panel: Size, viewport: Size): FloatingPlacement {
  const { side, edge } = anchorOf(island, viewport);
  const rect = {
    x:
      side === 'end'
        ? island.x + island.width - panel.width
        : side === 'center'
          ? island.x + (island.width - panel.width) / 2
          : island.x,
    y: edge === 'bottom' ? island.y + island.height - panel.height : island.y,
    width: panel.width,
    height: panel.height,
  };
  return placementOf(rectOf(placementOf(rect, viewport), viewport), viewport);
}

function clamp(value: number, min: number, max: number) {
  return Math.max(min, Math.min(Math.max(min, max), value));
}

/** 視窗小到放不下下限時，以視窗為準。 */
function fit(value: number, min: number, max: number) {
  return Math.min(Math.max(value, min), Math.max(0, max));
}
