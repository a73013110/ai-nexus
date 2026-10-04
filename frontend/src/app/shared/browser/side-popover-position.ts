/** Align a reading preview to its actual tick and keep it inside the conversation body. */
export function positionSidePopover(trigger: HTMLElement, panel: HTMLElement, body: HTMLElement) {
  const anchor = trigger.getBoundingClientRect(),
    bounds = body.getBoundingClientRect();
  const leftEdge = Math.max(12, bounds.left + 12),
    rightEdge = Math.min(innerWidth - 12, bounds.right - 12);
  const width = Math.min(320, Math.max(220, rightEdge - leftEdge));
  panel.style.width = `${width}px`;
  panel.style.maxHeight = `${Math.max(100, Math.min(420, bounds.height - 24))}px`;
  const height = panel.getBoundingClientRect().height;
  panel.style.left = `${Math.max(leftEdge, Math.min(anchor.left - width - 8, rightEdge - width))}px`;
  const topEdge = Math.max(12, bounds.top + 12),
    bottomEdge = Math.min(innerHeight - 12, bounds.bottom - 12);
  panel.style.top = `${Math.max(topEdge, Math.min(anchor.top + anchor.height / 2 - height / 2, bottomEdge - height))}px`;
}
