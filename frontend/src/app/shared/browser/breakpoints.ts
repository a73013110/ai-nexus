/**
 * 視口級距，與 `styles/breakpoints.scss` 同步：每個值都是其級距中最寬的視口，
 * 因此 `below(tier)` 與 `above(tier)` 在小數寬度時既不重疊也不留縫隙。
 */
export const breakpoints = {
  compact: 520,
  phone: 640,
  /** 工作區側邊欄變成覆蓋式抽屜。 */
  narrow: 859,
  tablet: 1100,
  wide: 1200,
} as const;

export type Breakpoint = keyof typeof breakpoints;

/** `tier` 以下（含）視口的媒體查詢，供 `mediaQuery()` 使用。 */
export const below = (tier: Breakpoint) => `(width <= ${breakpoints[tier]}px)`;

/** 比 `tier` 寬的視口的媒體查詢：正好是 `below(tier)` 的補集。 */
export const above = (tier: Breakpoint) => `(width > ${breakpoints[tier]}px)`;
