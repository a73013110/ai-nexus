/** Fixed top-layer placement shared by selects and action menus. */
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

/** Opens to the right of a rail trigger, bottom-aligned with it, for menus on the icon rail. */
export function positionBesidePopover(trigger: HTMLElement, panel: HTMLElement, width = 240) {
  const anchor = trigger.getBoundingClientRect();
  const size = Math.min(width, innerWidth - anchor.right - 20);
  panel.style.width = `${size}px`;
  panel.style.maxHeight = `${Math.max(80, innerHeight - 24)}px`;
  const height = panel.getBoundingClientRect().height;
  const top = Math.max(12, Math.min(anchor.bottom - height, innerHeight - height - 12));
  panel.style.left = `${anchor.right + 8}px`;
  panel.style.top = `${top}px`;
  setPopoverOrigin(panel, 'right', 0, anchor.top + anchor.height / 2 - top);
}

/**
 * Records which side of its trigger the panel opened on and grows it from the trigger, for the
 * shared `.popover-panel` motion (styles/popovers.scss). The point is clamped to the panel.
 */
export function setPopoverOrigin(
  panel: HTMLElement,
  side: 'top' | 'right' | 'bottom' | 'left',
  x: number,
  y: number,
) {
  const { offsetWidth: width, offsetHeight: height } = panel;
  panel.dataset['side'] = side;
  panel.style.transformOrigin = `${Math.max(0, Math.min(x, width))}px ${Math.max(0, Math.min(y, height))}px`;
}
