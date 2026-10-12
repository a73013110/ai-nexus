/**
 * Keeps docked content (the composer, bottom sheets) above the on-screen keyboard.
 * `interactive-widget=resizes-content` (index.html) makes Chromium shrink the layout viewport, and
 * with it `dvh`, while the keyboard is up. Safari ignores it and only shrinks the visual viewport,
 * so the covered height is published as `--keyboard-inset` for the frame to subtract, and the
 * document scroll Safari adds to reveal the field is undone, since the frame has moved up instead.
 */
export function trackKeyboardInset() {
  const viewport = window.visualViewport;
  if (!viewport) return () => undefined;
  const root = document.documentElement;
  let frame = 0;
  // Smaller gaps are a desktop scrollbar or a collapsing toolbar, not a keyboard.
  const keyboard = 80;
  const measure = () => {
    // Pinch zoom also shrinks the visual viewport; only an unzoomed one means a keyboard.
    const covered = viewport.scale > 1.01 ? 0 : Math.round(window.innerHeight - viewport.height);
    const inset = covered >= keyboard ? covered : 0;
    if (inset) root.style.setProperty('--keyboard-inset', inset + 'px');
    else root.style.removeProperty('--keyboard-inset');
    if (inset && (window.scrollY || window.scrollX)) window.scrollTo(0, 0);
  };
  const schedule = () => {
    cancelAnimationFrame(frame);
    frame = requestAnimationFrame(measure);
  };
  viewport.addEventListener('resize', schedule);
  viewport.addEventListener('scroll', schedule);
  measure();
  return () => {
    cancelAnimationFrame(frame);
    viewport.removeEventListener('resize', schedule);
    viewport.removeEventListener('scroll', schedule);
  };
}
