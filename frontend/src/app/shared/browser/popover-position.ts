export type PopoverSide = 'top' | 'right' | 'bottom' | 'left';

/** 面板高度上限：不超過 `maximum`，也不超出視口（上下各留 12px），但至少 80px。 */
const limitHeight = (panel: HTMLElement, maximum: number) => {
  panel.style.maxHeight = `${Math.max(80, Math.min(maximum, innerHeight - 24))}px`;
};

/** 由選取器與動作選單共用的固定最上層定位。 */
export function positionPopover(
  trigger: HTMLElement,
  panel: HTMLElement,
  minimum = 220,
  align: 'left' | 'right' = 'left',
  maximumHeight = 360,
) {
  const anchor = trigger.getBoundingClientRect();
  const width = Math.min(Math.max(anchor.width, minimum), innerWidth - 24);
  panel.style.width = `${width}px`;
  limitHeight(panel, maximumHeight);
  const height = panel.getBoundingClientRect().height;
  const below = anchor.bottom + height + 8 < innerHeight;
  const left = Math.max(
    12,
    Math.min(align === 'right' ? anchor.right - width : anchor.left, innerWidth - width - 12),
  );
  panel.style.left = `${left}px`;
  panel.style.top = `${Math.max(12, Math.min(below ? anchor.bottom + 6 : anchor.top - height - 6, innerHeight - height - 12))}px`;
  setPopoverOrigin(
    panel,
    below ? 'bottom' : 'top',
    anchor.left + anchor.width / 2 - left,
    below ? 0 : height,
  );
}

/**
 * 在圖示欄的觸發器右側開啟，底部與其對齊，供圖示欄上的選單使用。
 * `edge` 是面板要避開的 x 座標，例如圖示欄的右邊緣；預設為觸發器的右邊緣。
 */
export function positionBesidePopover(
  trigger: HTMLElement,
  panel: HTMLElement,
  width = 240,
  maximumHeight = Infinity,
  edge?: number,
) {
  const anchor = trigger.getBoundingClientRect();
  const right = edge ?? anchor.right;
  const size = Math.min(width, innerWidth - right - 20);
  panel.style.width = `${size}px`;
  limitHeight(panel, maximumHeight);
  const height = panel.getBoundingClientRect().height;
  const top = Math.max(12, Math.min(anchor.bottom - height, innerHeight - height - 12));
  panel.style.left = `${right + 8}px`;
  panel.style.top = `${top}px`;
  setPopoverOrigin(panel, 'right', 0, anchor.top + anchor.height / 2 - top);
}

/**
 * 記錄面板在觸發器哪一側開啟，並從觸發器處展開，供共用的 `.popover-panel`
 * 動效使用（styles/popovers.scss）。定位點會被夾在面板之內。
 */
export function setPopoverOrigin(
  panel: HTMLElement,
  side: PopoverSide,
  pointX: number,
  pointY: number,
) {
  const { offsetWidth: width, offsetHeight: height } = panel;
  panel.dataset['side'] = side;
  panel.style.transformOrigin = `${Math.max(0, Math.min(pointX, width))}px ${Math.max(0, Math.min(pointY, height))}px`;
}

interface Box {
  left: number;
  top: number;
  width: number;
  height: number;
}

/**
 * 在錨點的 `preferred` 側放固定大小的面板，留 `gap`；放不下時依序改用下、上、右、左，
 * 都放不下（錨點太大或視口太小）就疊在視口底部。位置一律夾在視口內，四周留 `margin`。
 * 沒有錨點時置中。純函式，回傳左上角、實際方向與從錨點長出的原點（面板座標）。
 */
export function placeBeside(
  anchor: Box | undefined,
  size: { width: number; height: number },
  preferred: PopoverSide,
  viewport: { width: number; height: number },
  gap = 12,
  margin = 12,
) {
  const clampX = (x: number) => Math.max(margin, Math.min(x, viewport.width - size.width - margin));
  const clampY = (y: number) =>
    Math.max(margin, Math.min(y, viewport.height - size.height - margin));
  if (!anchor) {
    const left = clampX((viewport.width - size.width) / 2),
      top = clampY((viewport.height - size.height) / 2);
    return { left, top, side: 'bottom' as PopoverSide, originX: size.width / 2, originY: 0 };
  }
  const right = anchor.left + anchor.width,
    bottom = anchor.top + anchor.height;
  const fits: Record<PopoverSide, boolean> = {
    bottom: bottom + gap + size.height <= viewport.height - margin,
    top: anchor.top - gap - size.height >= margin,
    right: right + gap + size.width <= viewport.width - margin,
    left: anchor.left - gap - size.width >= margin,
  };
  const order: PopoverSide[] = [preferred, 'bottom', 'top', 'right', 'left'];
  const side = order.find((candidate) => fits[candidate]);
  if (!side) {
    const left = clampX(anchor.left + anchor.width / 2 - size.width / 2),
      top = viewport.height - size.height - margin;
    return {
      left,
      top: clampY(top),
      side: 'top' as PopoverSide,
      originX: size.width / 2,
      originY: size.height,
    };
  }
  const vertical = side === 'top' || side === 'bottom';
  const left = vertical
    ? clampX(anchor.left + anchor.width / 2 - size.width / 2)
    : side === 'right'
      ? right + gap
      : anchor.left - gap - size.width;
  const top = vertical
    ? side === 'bottom'
      ? bottom + gap
      : anchor.top - gap - size.height
    : clampY(anchor.top + anchor.height / 2 - size.height / 2);
  return {
    left,
    top,
    side,
    // 原點在面板靠錨點的那一邊，對齊錨點中心。
    originX: vertical ? anchor.left + anchor.width / 2 - left : side === 'right' ? 0 : size.width,
    originY: vertical
      ? side === 'bottom'
        ? 0
        : size.height
      : anchor.top + anchor.height / 2 - top,
  };
}
