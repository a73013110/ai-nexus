/** A feature's place: the server lists features by `sortOrder`, then `id`. */
export interface FeatureOrder {
  id: string;
  sortOrder: number;
}
export type OrderShift = number | 'first' | 'last';

/** The largest `sortOrder` the server accepts (`SaveFeature`). */
export const MAX_SORT_ORDER = 10000;
const STEP = 10;

/**
 * Features in display order with `overrides` (values being saved) applied, the way the sidebar
 * and the catalog order them; `groupFeatures` keeps this order inside each group.
 */
export function orderFeatures<T extends FeatureOrder>(
  features: readonly T[],
  overrides: Readonly<Record<string, number>> = {},
): T[] {
  return features
    .map((feature) =>
      feature.id in overrides ? { ...feature, sortOrder: overrides[feature.id] } : feature,
    )
    .sort((a, b) => a.sortOrder - b.sortOrder || (a.id < b.id ? -1 : a.id > b.id ? 1 : 0));
}

/** `ids` with `id` moved by `shift` places (or to either end), clamped to the list. */
export function shiftId(ids: readonly string[], id: string, shift: OrderShift): string[] {
  const from = ids.indexOf(id);
  if (from < 0) return [...ids];
  const to =
    shift === 'first'
      ? 0
      : shift === 'last'
        ? ids.length - 1
        : Math.max(0, Math.min(ids.length - 1, from + shift));
  const next = ids.filter((x) => x !== id);
  next.splice(to, 0, id);
  return next;
}

/**
 * The `sortOrder` writes that put one group in the order `ids` after `moved` changed place.
 * `current` is the group in its present order. The other features keep their values, so a move is
 * usually one write: the moved feature takes the middle of the gap it lands in, or 10 beyond an
 * end. Without room (equal neighbours, a gap of 1, the 0–10000 range) the group is renumbered
 * 10, 20, 30… so later moves fit again. Only values that change are returned.
 */
export function reorderWrites(
  current: readonly FeatureOrder[],
  moved: string,
  ids: readonly string[],
): FeatureOrder[] {
  const values = new Map(current.map((feature) => [feature.id, feature.sortOrder]));
  const index = ids.indexOf(moved);
  if (index < 0 || ids.join() === current.map((feature) => feature.id).join()) return [];
  const before = index > 0 ? values.get(ids[index - 1])! : null;
  const after = index < ids.length - 1 ? values.get(ids[index + 1])! : null;
  const value = between(before, after);
  const next =
    value === null
      ? ids.map((id, position) => ({ id, sortOrder: (position + 1) * STEP }))
      : [{ id: moved, sortOrder: value }];
  return next.filter((write) => values.get(write.id) !== write.sortOrder);
}

function between(before: number | null, after: number | null): number | null {
  if (before === null && after === null) return null;
  const value =
    before === null
      ? Math.max(0, after! - STEP)
      : after === null
        ? Math.min(MAX_SORT_ORDER, before + STEP)
        : Math.floor((before + after) / 2);
  return (before === null || value > before) && (after === null || value < after) ? value : null;
}
