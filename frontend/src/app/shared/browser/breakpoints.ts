/**
 * Viewport tiers, mirrored from `styles/breakpoints.scss`: each value is the widest viewport of its
 * tier, so `below(tier)` and `above(tier)` never overlap or leave a gap at fractional widths.
 */
export const breakpoints = {
  compact: 520,
  phone: 640,
  /** The workspace sidebar becomes an overlay drawer. */
  narrow: 859,
  tablet: 1100,
  wide: 1200,
} as const;

export type Breakpoint = keyof typeof breakpoints;

/** Media query for viewports at or below `tier`, for `mediaQuery()`. */
export const below = (tier: Breakpoint) => `(width <= ${breakpoints[tier]}px)`;

/** Media query for viewports wider than `tier`: the exact complement of `below(tier)`. */
export const above = (tier: Breakpoint) => `(width > ${breakpoints[tier]}px)`;
