/**
 * 使用者剛啟動的控制項：`click` 涵蓋滑鼠、觸控，以及鍵盤在按鈕、連結上按 Enter／空白鍵。
 * 圖層用它知道自己是從哪裡開的（對話框由那裡長出來，見 `shared/ui/origin-motion.ts`），開啟點不必逐一傳元素。
 */

/** 啟動後多久內開的圖層算是它開的：涵蓋延後載入與讀取，再久就與那次點擊無關（計時器、背景事件）。 */
const ACTIVATION_WINDOW_MS = 2000;
/** 點到控制項裡的圖示或文字時，取整個控制項。 */
const CONTROL =
  'a, button, summary, label, [role="button"], [role="menuitem"], [role="option"], [role="tab"]';

let last: { element: Element; at: number } | undefined;

if (typeof document !== 'undefined') {
  // 新的按鍵或按下代表使用者在做別的事（快捷鍵、打字），之前的啟動作廢；它們自己若是一次啟動，
  // 隨後的 click 會再記上。
  const forget = () => (last = undefined);
  const options = { capture: true, passive: true };
  document.addEventListener('pointerdown', forget, options);
  document.addEventListener('keydown', forget, options);
  document.addEventListener(
    'click',
    (event) => {
      const target = event.target;
      if (target instanceof Element)
        last = { element: target.closest(CONTROL) ?? target, at: performance.now() };
    },
    options,
  );
}

/** 剛啟動、仍在頁面上的控制項；沒有（快捷鍵、計時器開的圖層）為 null。 */
export function recentActivation(): Element | null {
  if (!last || performance.now() - last.at > ACTIVATION_WINDOW_MS) return null;
  return last.element.isConnected ? last.element : null;
}
