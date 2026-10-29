/**
 * 讀動效權杖（`tokens.scss` 的 `--motion*`、`--ease*`）給 Web Animations 用，CSS 過渡與 JS 動畫共用同一組數值。
 * 正式建置會壓縮 CSS（`240ms` 變成 `.24s`），而本機開發不壓縮，單位讀錯只會在正式機現形；
 * 一律經過這裡換算，見 docs/frontend/script-reads-css.md。
 */
export type MotionDurationToken = `--motion${'' | `-${string}`}`;
export type MotionEasingToken = `--ease${'' | `-${string}`}`;

/** 權杖時長（毫秒）。讀不到（例如測試環境沒有全域樣式）時為 0，直接到位。 */
export function motionDuration(element: Element, token: MotionDurationToken): number {
  const value = getComputedStyle(element).getPropertyValue(token).trim();
  const match = /^(\d*\.?\d+)(ms|s)$/.exec(value);
  if (!match) return 0;
  const amount = Number(match[1]);
  return match[2] === 's' ? amount * 1000 : amount;
}

/** 時長加曲線，可直接當 `animate()` 的選項。 */
export function motionTiming(
  element: Element,
  duration: MotionDurationToken,
  easing: MotionEasingToken = '--ease',
) {
  return {
    duration: motionDuration(element, duration),
    easing: getComputedStyle(element).getPropertyValue(easing).trim() || 'linear',
  };
}
