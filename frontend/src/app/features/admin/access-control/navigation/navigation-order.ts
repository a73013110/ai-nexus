import type { NavigationItemDto, NavigationOrderItem } from '@core/api/schema';
import { sortWrites } from '@shared/browser/sort-order';

export { shiftId, type OrderShift } from '@shared/browser/sort-order';

/** 伺服器接受的最大 `sortOrder`（見後端 `NavigationRules`）。 */
export const MAX_SORT_ORDER = 10000;
/** 選單最多幾層，最上層算第 1 層（後端 `NavigationTree.MaxDepth`）。 */
export const MAX_DEPTH = 4;

/** 管理頁的選單樹節點：伺服器的平面清單依 `parentId` 組回來，每層依 `sortOrder`、再依 id 排列。 */
export interface EditorNode {
  item: NavigationItemDto;
  children: EditorNode[];
}

/** 拖曳中的暫定位置：`index` 是插入目標層（不含被移動的節點本身）的位置。 */
export interface Placement {
  id: string;
  parentId: string | null;
  index: number;
}

/** 與伺服器相同的同層順序：`sortOrder`，同值時依 id 的序數比較。 */
export function bySortOrder(
  first: { sortOrder: number; id: string },
  second: { sortOrder: number; id: string },
) {
  return (
    first.sortOrder - second.sortOrder || (first.id < second.id ? -1 : first.id > second.id ? 1 : 0)
  );
}

/** `parentId` 底下的節點（`null` 為最上層），依顯示順序。 */
export function level(items: readonly NavigationItemDto[], parentId: string | null) {
  return items.filter((item) => (item.parentId ?? null) === parentId).sort(bySortOrder);
}

/**
 * 組回選單樹；有 `placement` 時先把該節點移到暫定位置（拖曳中的預覽）。
 * 上層不存在或成環的節點放到最上層：伺服器會擋下這種資料，但管理頁仍要看得到每個節點才能修正。
 */
export function editorTree(
  items: readonly NavigationItemDto[],
  placement?: Placement | null,
): EditorNode[] {
  const known = new Map(items.map((item) => [item.id, item]));
  const parentOf = (item: NavigationItemDto): string | null => {
    if (placement?.id === item.id) return placement.parentId;
    const parent = item.parentId ?? null;
    if (parent === null || !known.has(parent)) return null;
    // 只有自己在環上才放到最上層；祖先之間的環由祖先自己處理，子孫仍留在原分群下。
    for (let current = known.get(parent), seen = new Set<string>(); current;) {
      if (current.id === item.id) return null;
      if (seen.has(current.id)) break;
      seen.add(current.id);
      current = current.parentId ? known.get(current.parentId) : undefined;
    }
    return parent;
  };
  const children = new Map<string | null, NavigationItemDto[]>();
  for (const item of items) {
    const parent = parentOf(item);
    children.set(parent, [...(children.get(parent) ?? []), item]);
  }
  const build = (parent: string | null): EditorNode[] => {
    const ordered = (children.get(parent) ?? []).sort(bySortOrder);
    if (placement && (placement.parentId ?? null) === parent) {
      const moved = ordered.find((item) => item.id === placement.id);
      if (moved) {
        ordered.splice(ordered.indexOf(moved), 1);
        ordered.splice(Math.min(placement.index, ordered.length), 0, moved);
      }
    }
    return ordered.map((item) => ({ item, children: build(item.id) }));
  };
  return build(null);
}

/** 節點與其所有子孫的 id；節點不能移到其中任何一個之下。 */
export function descendantIds(items: readonly NavigationItemDto[], id: string): Set<string> {
  const ids = new Set([id]);
  for (let grown = true; grown;) {
    grown = false;
    for (const item of items)
      if (item.parentId && ids.has(item.parentId) && !ids.has(item.id)) {
        ids.add(item.id);
        grown = true;
      }
  }
  return ids;
}

/** 節點所在的層數（最上層為 1）；`null` 代表最上層之上（0）。 */
export function depthOf(items: readonly NavigationItemDto[], id: string | null): number {
  const known = new Map(items.map((item) => [item.id, item]));
  let depth = 0;
  for (let current = id ? known.get(id) : undefined; current && depth <= items.length; depth++)
    current = current.parentId ? known.get(current.parentId) : undefined;
  return depth;
}

/** 節點連同子孫佔幾層（沒有子節點為 1）。 */
export function heightOf(items: readonly NavigationItemDto[], id: string): number {
  const below = items.filter((item) => item.parentId === id);
  return 1 + Math.max(0, ...below.map((item) => heightOf(items, item.id)));
}

/**
 * 節點能不能移到 `parentId` 之下，與伺服器的結構規則相同（上層必須是分群、不可成環、最多四層、
 * 同層不可重複同一頁面）。只用來決定要不要提供這個位置；伺服器仍會再檢查一次。
 */
export function canPlace(
  items: readonly NavigationItemDto[],
  id: string,
  parentId: string | null,
): boolean {
  const item = items.find((candidate) => candidate.id === id);
  if (!item) return false;
  if (parentId !== null) {
    const parent = items.find((candidate) => candidate.id === parentId);
    if (parent?.kind !== 'group' || descendantIds(items, id).has(parentId)) return false;
  }
  if (depthOf(items, parentId) + heightOf(items, id) > MAX_DEPTH) return false;
  return (
    !item.featureId ||
    !items.some(
      (other) =>
        other.id !== id &&
        other.featureId === item.featureId &&
        (other.parentId ?? null) === parentId,
    )
  );
}

/**
 * 把 `id` 移到 `parentId` 之下第 `index` 個（不含它自己的位置）所需的批次寫入（`PUT .../order`）。
 * 值的取法見 `sortWrites`：通常只有移動的那一筆，沒有空間時目標層重新編號。位置沒變時回傳空陣列。
 */
export function moveWrites(
  items: readonly NavigationItemDto[],
  id: string,
  parentId: string | null,
  index: number,
): NavigationOrderItem[] {
  if (!items.some((item) => item.id === id)) return [];
  return sortWrites(level(items, parentId), id, index, MAX_SORT_ORDER).map((write) => ({
    ...write,
    parentId,
  }));
}

/** 套用批次寫入後的平面清單，讓畫面先顯示新位置（失敗時換回原值）。 */
export function applyWrites(
  items: readonly NavigationItemDto[],
  writes: readonly NavigationOrderItem[],
): NavigationItemDto[] {
  const changes = new Map(writes.map((write) => [write.id, write]));
  return items.map((item) => {
    const write = changes.get(item.id);
    return write ? { ...item, parentId: write.parentId, sortOrder: write.sortOrder } : item;
  });
}
