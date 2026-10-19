/** 功能（或選單群組）的位置：伺服器依 `sortOrder`、再依 `id` 排列。 */
export interface FeatureOrder {
  id: string;
  sortOrder: number;
}
export type OrderShift = number | 'first' | 'last';

/** 伺服器接受的最大 `sortOrder`（見 `NavigationRules`）。 */
export const MAX_SORT_ORDER = 10000;
const STEP = 10;

/**
 * 依顯示順序排列的功能，套用 `overrides`（正在儲存的值）；側邊欄與目錄也是這樣排序，
 * `navigationTree` 會在每個群組內保留這個順序。
 */
export function orderFeatures<T extends FeatureOrder>(
  features: readonly T[],
  overrides: Readonly<Record<string, number>> = {},
): T[] {
  return features
    .map((feature) =>
      feature.id in overrides ? { ...feature, sortOrder: overrides[feature.id] } : feature,
    )
    .sort(
      (first, second) =>
        first.sortOrder - second.sortOrder ||
        (first.id < second.id ? -1 : first.id > second.id ? 1 : 0),
    );
}

/** `siblings` 中最後一個之後的位置（沒有則為 10），並限制在可接受的範圍內。 */
export function nextSortOrder(siblings: readonly { sortOrder: number }[]): number {
  return Math.min(MAX_SORT_ORDER, Math.max(0, ...siblings.map((item) => item.sortOrder)) + STEP);
}

/** 把 `id` 往前或往後移動 `shift` 個位置（或直接移到兩端），並限制在列表範圍內。 */
export function shiftId(ids: readonly string[], id: string, shift: OrderShift): string[] {
  const from = ids.indexOf(id);
  if (from < 0) return [...ids];
  const to =
    shift === 'first'
      ? 0
      : shift === 'last'
        ? ids.length - 1
        : Math.max(0, Math.min(ids.length - 1, from + shift));
  const next = ids.filter((item) => item !== id);
  next.splice(to, 0, id);
  return next;
}

/**
 * 在 `moved` 改變位置後，讓群組依 `ids` 排序所需的 `sortOrder` 寫入。
 * `current` 是目前順序的群組。其他功能保留原值，因此一次移動通常只需一筆寫入：
 * 移動的功能取落點間隙的中間值，
 * 超出末端時取 10 之外的值。沒有空間（相鄰值相同、間隙為 1、0–10000 範圍）時，群組會重新編號為
 * 10、20、30…，讓之後的移動又能放得下。只回傳有變更的值。
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

/** 兩組 ID 是否相同（不計順序）；角色、群組的勾選與使用者的角色指派都用它判斷有沒有變更。 */
export function sameIds(items: readonly string[], items2: readonly string[]) {
  return items.length === items2.length && items.every((id) => items2.includes(id));
}
