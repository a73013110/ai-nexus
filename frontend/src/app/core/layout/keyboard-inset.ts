/**
 * 讓停靠的內容（輸入框、底部面板）留在螢幕鍵盤之上。
 * `interactive-widget=resizes-content`（index.html）讓 Chromium
 * 在鍵盤開啟時縮小版面視口（連帶縮小 `dvh`）。
 * Safari 會忽略它，只縮小視覺視口，
 * 因此被遮住的高度會發布為 `--keyboard-inset` 供框架扣除；
 * Safari 為露出欄位而加上的文件捲動也會被抵銷，因為框架已經往上移動。
 */
export function trackKeyboardInset() {
  const viewport = window.visualViewport;
  if (!viewport) return () => undefined;
  const root = document.documentElement;
  let frame = 0;
  // 較小的間隙是桌面的捲軸或收合的工具列，不是鍵盤。
  const keyboard = 80;
  const measure = () => {
    // 雙指縮放也會縮小視覺視口；只有未縮放時的視口，才代表鍵盤開啟。
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
