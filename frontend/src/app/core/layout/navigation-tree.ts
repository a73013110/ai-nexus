import type { NavigationGroupDto } from '@core/api/schema';
import { ICON_NAMES } from '@shared/ui/icon';

/** What the menu needs from a feature; both `/me` features and the admin catalog have it. */
export interface NavigationEntry {
  id: string;
  groupId?: string | null;
  sortOrder: number;
}

/** A menu group with its own features and subgroups, each list in display order. */
export interface NavigationNode<T> {
  group: NavigationGroupDto;
  features: T[];
  groups: NavigationNode<T>[];
}

/** The menu's top level: features without a group first, then the top-level groups. */
export interface NavigationTree<T> {
  features: T[];
  groups: NavigationNode<T>[];
}

/** One group (or the top level, `id` '') as a flat section, for lists that do not nest. */
export interface NavigationSection<T> {
  id: string;
  /** The group's name, or `topName` for the top level. */
  name: string;
  /** Ancestor names and the group's own, for a flat list that must still tell nested groups apart. */
  path: string;
  depth: number;
  features: T[];
}

const byOrder = (a: { sortOrder: number; id: string }, b: { sortOrder: number; id: string }) =>
  a.sortOrder - b.sortOrder || (a.id < b.id ? -1 : a.id > b.id ? 1 : 0);

/**
 * Builds the menu from the API's flat lists. At every level features come first, then subgroups,
 * each ordered by `sortOrder` and then id (the same order the server lists them in). A feature or
 * group whose parent is unknown sits at the top level, so nothing granted is ever hidden by a
 * stale reference; a group inside a cycle is treated the same way. Groups with no feature in
 * their subtree are dropped unless `keepEmpty` (the administrator's editor shows every group).
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
 * The tree as sections in menu order (depth first). The top level is the first section, named
 * `topName`, and is left out when it has no features unless `keepTop`.
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

/** Every feature of the tree in menu order. */
export function navigationOrder<T>(tree: NavigationTree<T>): T[] {
  return navigationSections(tree).flatMap((section) => section.features);
}

/** The group and its descendants' ids; a group cannot move under any of them. */
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

/** An icon name the menu can draw: the API's value when known, otherwise one neutral fallback. */
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
