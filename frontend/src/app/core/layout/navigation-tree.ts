import type { NavigationGroupDto } from '@core/api/schema';
import { ICON_NAMES } from '@shared/ui/icon';

/** 選單需要功能的哪些欄位；`/me` 的功能與管理目錄都有。 */
export interface NavigationEntry {
  id: string;
  groupId?: string | null;
  sortOrder: number;
}

/** 選單群組，含其自己的功能與子群組，各自依顯示順序排列。 */
export interface NavigationNode<T> {
  group: NavigationGroupDto;
  features: T[];
  groups: NavigationNode<T>[];
}

/** 選單最上層：先是沒有群組的功能，接著是最上層群組。 */
export interface NavigationTree<T> {
  features: T[];
  groups: NavigationNode<T>[];
}

/** 一個群組（或最上層，`id` 為空字串）扁平化成一個區段，供不巢狀的列表使用。 */
export interface NavigationSection<T> {
  id: string;
  /** 群組名稱；最上層則為 `topName`。 */
  name: string;
  /** 祖先名稱加上群組自己的名稱；扁平列表仍要能分辨巢狀群組。 */
  path: string;
  depth: number;
  features: T[];
}

const byOrder = (
  first: { sortOrder: number; id: string },
  second: { sortOrder: number; id: string },
) =>
  first.sortOrder - second.sortOrder || (first.id < second.id ? -1 : first.id > second.id ? 1 : 0);

/**
 * 由 API 的扁平列表建立選單。每一層都是功能在前、子群組在後，
 * 各自依 `sortOrder`，再依 id 排序（與伺服器列出的順序相同）。上層未知的功能或群組放在最上層，
 * 因此不會因為過期的參照而隱藏已授權的內容；循環中的群組也以同樣方式處理。
 * 子樹中沒有功能的群組會被丟棄，除非 `keepEmpty`（管理員的編輯器會顯示所有群組）。
 */
export function navigationTree<T extends NavigationEntry>(
  features: readonly T[],
  groups: readonly NavigationGroupDto[],
  { keepEmpty = false } = {},
): NavigationTree<T> {
  const known = new Map(groups.map((group) => [group.id, group]));
  const parentOf = (group: NavigationGroupDto) =>
    group.parentId && known.has(group.parentId) && !inCycle(group, known) ? group.parentId : '';
  const childGroups = new Map<string, NavigationGroupDto[]>();
  for (const group of groups) push(childGroups, parentOf(group), group);
  const childFeatures = new Map<string, T[]>();
  for (const feature of features)
    push(
      childFeatures,
      feature.groupId && known.has(feature.groupId) ? feature.groupId : '',
      feature,
    );

  const build = (group: NavigationGroupDto): NavigationNode<T> | null => {
    const node: NavigationNode<T> = {
      group,
      features: [...(childFeatures.get(group.id) ?? [])].sort(byOrder),
      groups: nodes(group.id),
    };
    return keepEmpty || node.features.length || node.groups.length ? node : null;
  };
  const nodes = (parent: string): NavigationNode<T>[] =>
    [...(childGroups.get(parent) ?? [])]
      .sort(byOrder)
      .map(build)
      .filter((node): node is NavigationNode<T> => !!node);
  return { features: [...(childFeatures.get('') ?? [])].sort(byOrder), groups: nodes('') };
}

/**
 * 依選單順序（深度優先）回傳區段樹。第一個區段是最上層，名稱為
 * `topName`；沒有功能時會省略，除非 `keepTop`。
 */
export function navigationSections<T>(
  tree: NavigationTree<T>,
  { topName = '', keepTop = false } = {},
): NavigationSection<T>[] {
  const sections: NavigationSection<T>[] = [];
  if (keepTop || tree.features.length)
    sections.push({ id: '', name: topName, path: topName, depth: 0, features: tree.features });
  const visit = (node: NavigationNode<T>, depth: number, trail: string[]) => {
    const path = [...trail, node.group.name];
    sections.push({
      id: node.group.id,
      name: node.group.name,
      path: path.join(' › '),
      depth,
      features: node.features,
    });
    for (const child of node.groups) visit(child, depth + 1, path);
  };
  for (const node of tree.groups) visit(node, 0, []);
  return sections;
}

/** 依選單順序列出樹中的每個功能。 */
export function navigationOrder<T>(tree: NavigationTree<T>): T[] {
  return navigationSections(tree).flatMap((section) => section.features);
}

/** 群組與其所有子孫的 id；群組不能移到其中任何一個之下。 */
export function descendantIds(groups: readonly NavigationGroupDto[], id: string): Set<string> {
  const ids = new Set([id]);
  for (let grown = true; grown;) {
    grown = false;
    for (const group of groups)
      if (group.parentId && ids.has(group.parentId) && !ids.has(group.id)) {
        ids.add(group.id);
        grown = true;
      }
  }
  return ids;
}

/** 選單能繪製的圖示名稱：已知時用 API 的值，否則用一個中性的備用圖示。 */
export function featureIcon(icon: string | null | undefined): string {
  return icon && ICON_NAMES.has(icon) ? icon : 'document';
}

function push<K, V>(map: Map<K, V[]>, key: K, value: V) {
  const list = map.get(key);
  if (list) list.push(value);
  else map.set(key, [value]);
}

function inCycle(group: NavigationGroupDto, known: ReadonlyMap<string, NavigationGroupDto>) {
  const seen = new Set<string>();
  for (let current: NavigationGroupDto | undefined = group; current?.parentId;) {
    if (seen.has(current.id)) return true;
    seen.add(current.id);
    current = known.get(current.parentId);
  }
  return false;
}
