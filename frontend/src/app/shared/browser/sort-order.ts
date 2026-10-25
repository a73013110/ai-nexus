/**
 * 手動排序的共用計算（規範見 docs/architecture/manual-ordering.md）：資料存整數 `sortOrder`，值之間保留間隔，
 * 一次移動通常只改被移動的那一筆；`null` 是未排序，只能排在已排序者之後。
 */

/** 可排序的一筆；`sortOrder` 為 `null` 是未排序。 */
export interface Sortable {
  id: string;
  sortOrder: number | null;
}

/** 一筆要寫入的新值（`PUT .../order` 的內容）。 */
export interface SortWrite {
  id: string;
  sortOrder: number;
}

/** 位移幾格，或直接移到兩端。 */
export type OrderShift = number | 'first' | 'last';

/** 預設間隔：新值取前後的中間，末端加 10，空間用完時整段重新編號為 10、20、30…。 */
export const SORT_STEP = 10;

/** 把 `id` 往前或往後移動 `shift` 格（或移到兩端），限制在列表範圍內；回傳新的 id 順序。 */
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
 * 把 `id` 放到 `level`（顯示順序，已排序的在前、未排序的在後）的第 `index` 個（不算它自己）所需的寫入。
 * `level` 可以不含 `id`（從別層移過來），這時一定會寫入它。
 *
 * - 落在已排序的範圍內：取前後值的中間；沒有空間時把已排序的整段重新編號。
 * - 落在未排序的人之間或之後：它前面的未排序者依序接在最後一個值之後，一起變成已排序，
 *   這樣畫面上的順序就是儲存後的順序；它後面的人維持未排序。
 *
 * 位置沒變時回傳空陣列；超出 `max` 時縮小間隔重新編號。
 */
export function sortWrites(
  level: readonly Sortable[],
  id: string,
  index: number,
  max: number,
): SortWrite[] {
  const moved = level.find((item) => item.id === id) ?? { id, sortOrder: null };
  const siblings = level.filter((item) => item.id !== id);
  const at = Math.max(0, Math.min(index, siblings.length));
  // 原地放下不寫入：畫面沒變，未排序的人也不因此變成已排序。
  if (level.findIndex((item) => item.id === id) === at) return [];
  const ordered = [...siblings];
  ordered.splice(at, 0, moved);
  const prefix = ordered.slice(0, at);
  const firstUnsorted = prefix.findIndex((item) => item.sortOrder === null);
  if (firstUnsorted >= 0) {
    // 前面有未排序的人：從第一個未排序者到被移動的這一筆依序接上。
    const base = firstUnsorted > 0 ? prefix[firstUnsorted - 1].sortOrder! : 0;
    const tail = ordered.slice(firstUnsorted, at + 1);
    if (base + tail.length * SORT_STEP <= max)
      return changed(
        tail.map((item, position) => ({
          id: item.id,
          sortOrder: base + (position + 1) * SORT_STEP,
        })),
        ordered,
        id,
      );
    return renumber(ordered.slice(0, at + 1), max, id);
  }
  const before = at > 0 ? prefix[at - 1].sortOrder : null;
  const after = ordered[at + 1]?.sortOrder ?? null;
  const value = between(before, after, max);
  if (value !== null) return [{ id, sortOrder: value }];
  const sorted = ordered.filter((item, position) => position <= at || item.sortOrder !== null);
  return renumber(sorted, max, id);
}

/** 依序重新編號；寫入只留值有變的與被移動的那一筆。 */
function renumber(items: readonly Sortable[], max: number, id: string): SortWrite[] {
  const step = Math.max(1, Math.min(SORT_STEP, Math.floor(max / Math.max(1, items.length))));
  return changed(
    items.map((item, position) => ({ id: item.id, sortOrder: (position + 1) * step })),
    items,
    id,
  );
}

function changed(writes: SortWrite[], items: readonly Sortable[], id: string): SortWrite[] {
  const values = new Map(items.map((item) => [item.id, item.sortOrder]));
  return writes.filter((write) => write.id === id || values.get(write.id) !== write.sortOrder);
}

/** 前後值之間可用的值；`after` 為 null 代表後面沒有已排序的值。沒有空間時回傳 null。 */
function between(before: number | null, after: number | null, max: number): number | null {
  const value =
    before === null
      ? after === null
        ? SORT_STEP
        : Math.max(0, after - SORT_STEP)
      : after === null
        ? Math.min(max, before + SORT_STEP)
        : Math.floor((before + after) / 2);
  return (before === null || value > before) && (after === null || value < after) ? value : null;
}

/** 套用寫入後的清單（畫面先顯示新位置，失敗時換回原清單）。 */
export function applySortWrites<T extends Sortable>(
  items: readonly T[],
  writes: readonly SortWrite[],
): T[] {
  const values = new Map(writes.map((write) => [write.id, write.sortOrder]));
  return items.map((item) =>
    values.has(item.id) ? { ...item, sortOrder: values.get(item.id)! } : item,
  );
}

/** 顯示順序：已排序的依值、未排序的在後；同值交給 `tie`（例如姓名）。 */
export function bySortOrder<T extends Sortable>(tie: (first: T, second: T) => number) {
  return (first: T, second: T) =>
    first.sortOrder === null || second.sortOrder === null
      ? Number(first.sortOrder === null) - Number(second.sortOrder === null) || tie(first, second)
      : first.sortOrder - second.sortOrder || tie(first, second);
}
