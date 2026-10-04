/** Fixed top-layer placement shared by selects and action menus. */
export function positionPopover(
  trigger: HTMLElement,
  panel: HTMLElement,
  minimum = 220,
  align: 'left' | 'right' = 'left',
) {
  const anchor = trigger.getBoundingClientRect();
  const width = Math.min(Math.max(anchor.width, minimum), innerWidth - 24);
  panel.style.width = `${width}px`;
  panel.style.maxHeight = `${Math.max(80, Math.min(360, innerHeight - 24))}px`;
  const height = panel.getBoundingClientRect().height;
  panel.style.left = `${Math.max(12, Math.min(align === 'right' ? anchor.right - width : anchor.left, innerWidth - width - 12))}px`;
  panel.style.top = `${Math.max(12, Math.min(anchor.bottom + height + 8 < innerHeight ? anchor.bottom + 6 : anchor.top - height - 6, innerHeight - height - 12))}px`;
}
