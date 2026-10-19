import type { UnitDto } from '@core/api/schema';

/** 與後端 `UnitRules.MaxDepth` 相同；伺服器仍會以 `organization_depth_exceeded` 拒絕。 */
export const MAX_DEPTH = 8;

/** 樹狀清單的一列：`depth` 由 1 起算，`path` 是由最上層到自己的名稱。 */
export interface UnitRow {
  unit: UnitDto;
  depth: number;
  path: string[];
  children: number;
  /** 自己或任一上層停用：整棵下層都不參與資料範圍。 */
  inactive: boolean;
}

const byOrder = (a: UnitDto, b: UnitDto) =>
  a.sortOrder - b.sortOrder || a.name.localeCompare(b.name, 'zh-Hant');

function childrenOf(units: readonly UnitDto[]) {
  const children = new Map<string | null, UnitDto[]>();
  for (const unit of units) {
    const key = unit.parentId ?? null;
    children.set(key, [...(children.get(key) ?? []), unit]);
  }
  for (const list of children.values()) list.sort(byOrder);
  return children;
}

/**
 * 伺服器的平面清單依樹的先序排列。上層不在清單裡的單位（不應發生）當成最上層，不讓它消失；
 * `collapsed` 裡的單位只列自己、不列下層。
 */
export function unitRows(units: readonly UnitDto[], collapsed: ReadonlySet<string> = new Set()) {
  const children = childrenOf(units);
  const ids = new Set(units.map((unit) => unit.id));
  const rows: UnitRow[] = [];
  const visit = (list: UnitDto[], trail: string[], inactive: boolean) => {
    for (const unit of list) {
      const path = [...trail, unit.name];
      const below = children.get(unit.id) ?? [];
      const off = inactive || !unit.enabled;
      rows.push({ unit, depth: path.length, path, children: below.length, inactive: off });
      if (!collapsed.has(unit.id) && path.length <= MAX_DEPTH) visit(below, path, off);
    }
  };
  const roots = units.filter((unit) => !unit.parentId || !ids.has(unit.parentId)).sort(byOrder);
  visit(roots, [], false);
  return rows;
}

/** 單位與它所有下層的 ID；搬移時這些不能當新的上層。 */
export function subtree(units: readonly UnitDto[], id: string) {
  const children = childrenOf(units);
  const result = new Set<string>();
  const pending = [id];
  while (pending.length) {
    const current = pending.pop()!;
    if (result.has(current)) continue;
    result.add(current);
    for (const child of children.get(current) ?? []) pending.push(child.id);
  }
  return result;
}

/** 單位所在的層數（最上層為 1）。 */
export function depthOf(units: readonly UnitDto[], id: string) {
  const byId = new Map(units.map((unit) => [unit.id, unit]));
  let depth = 0;
  for (let at = byId.get(id); at && depth <= MAX_DEPTH; at = byId.get(at.parentId ?? '')) depth++;
  return depth;
}

/** 以 `id` 為根的子樹有幾層（只有自己為 1）。 */
export function heightOf(units: readonly UnitDto[], id: string) {
  const children = childrenOf(units);
  const height = (current: string, level: number): number =>
    level > MAX_DEPTH
      ? level
      : Math.max(
          level,
          ...(children.get(current) ?? []).map((child) => height(child.id, level + 1)),
        );
  return height(id, 1);
}

/**
 * 把單位在同層上移（-1）或下移（+1）後，該層的完整順序；已在邊界時回 `null`。
 * 送出整層是伺服器的要求：期間有人改過這一層時它會拒絕，而不是排出意料之外的順序。
 */
export function moved(units: readonly UnitDto[], id: string, delta: -1 | 1) {
  const unit = units.find((item) => item.id === id);
  if (!unit) return null;
  const siblings = units.filter((item) => (item.parentId ?? null) === (unit.parentId ?? null));
  const ids = siblings.sort(byOrder).map((item) => item.id);
  const index = ids.indexOf(id);
  const target = index + delta;
  if (target < 0 || target >= ids.length) return null;
  [ids[index], ids[target]] = [ids[target], ids[index]];
  return { parentId: unit.parentId ?? null, unitIds: ids };
}
