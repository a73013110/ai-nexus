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
  panel.style.maxHeight = `${Math.max(80, Math.min(maximumHeight, innerHeight - 24))}px`;
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
  panel.style.maxHeight = `${Math.max(80, Math.min(maximumHeight, innerHeight - 24))}px`;
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
  side: 'top' | 'right' | 'bottom' | 'left',
  pointX: number,
  pointY: number,
) {
  const { offsetWidth: width, offsetHeight: height } = panel;
  panel.dataset['side'] = side;
  panel.style.transformOrigin = `${Math.max(0, Math.min(pointX, width))}px ${Math.max(0, Math.min(pointY, height))}px`;
}
