import { describe, expect, it } from 'vitest';
import {
  descendantIds,
  featureIcon,
  navigationOrder,
  navigationSections,
  navigationTree,
} from './navigation-tree';

const groups = [
  { id: 'system', parentId: null, name: '系統管理', sortOrder: 30 },
  { id: 'work', parentId: null, name: 'AI 工作台', sortOrder: 10 },
  { id: 'diagnosis', parentId: 'system', name: '診斷', sortOrder: 10 },
  { id: 'empty', parentId: null, name: '空分群', sortOrder: 40 },
];
const feature = (id: string, groupId: string | null, sortOrder: number) => ({
  id,
  groupId,
  sortOrder,
});
const features = [
  feature('admin', 'system', 90),
  feature('logs', 'diagnosis', 94),
  feature('chat', 'work', 10),
  feature('files', 'work', 20),
  feature('dashboard', null, 5),
  feature('extra', null, 50),
];
const ids = (list: readonly { id: string }[]) => list.map((x) => x.id);

describe('navigation tree', () => {
  it('puts top-level features first and orders every level by sortOrder, then id', () => {
    const tree = navigationTree(features, groups);
    expect(ids(tree.features)).toEqual(['dashboard', 'extra']);
    expect(tree.groups.map((node) => node.group.id)).toEqual(['work', 'system']);
    const system = tree.groups[1];
    expect(ids(system.features)).toEqual(['admin']);
    expect(system.groups.map((node) => [node.group.id, ids(node.features)])).toEqual([
      ['diagnosis', ['logs']],
    ]);
    expect(ids(navigationOrder(tree))).toEqual([
      'dashboard',
      'extra',
      'chat',
      'files',
      'admin',
      'logs',
    ]);
  });

  it('drops groups with nothing in their subtree unless asked to keep them', () => {
    expect(navigationTree(features, groups).groups.map((node) => node.group.id)).not.toContain(
      'empty',
    );
    const all = navigationTree([], groups, { keepEmpty: true });
    expect(all.groups.map((node) => node.group.id)).toEqual(['work', 'system', 'empty']);
    expect(all.groups[1].groups[0].group.id).toBe('diagnosis');
    // A parent with features only in a subgroup is kept for it.
    const nested = navigationTree([feature('logs', 'diagnosis', 1)], groups);
    expect(nested.groups.map((node) => [node.group.id, node.groups.length])).toEqual([
      ['system', 1],
    ]);
  });

  it('keeps features and groups with unknown or cyclic parents at the top level', () => {
    const tree = navigationTree(
      [feature('lost', 'missing', 1), feature('looped', 'a', 2)],
      [
        { id: 'a', parentId: 'b', name: 'A', sortOrder: 1 },
        { id: 'b', parentId: 'a', name: 'B', sortOrder: 2 },
        { id: 'orphan', parentId: 'missing', name: 'O', sortOrder: 3 },
      ],
      { keepEmpty: true },
    );
    expect(ids(tree.features)).toEqual(['lost']);
    expect(tree.groups.map((node) => node.group.id)).toEqual(['a', 'b', 'orphan']);
    expect(ids(tree.groups[0].features)).toEqual(['looped']);
  });

  it('flattens to sections with paths and depth, the top level first when it has features', () => {
    const sections = navigationSections(navigationTree(features, groups), { topName: '最上層' });
    expect(sections.map((x) => [x.id, x.path, x.depth, ids(x.features)])).toEqual([
      ['', '最上層', 0, ['dashboard', 'extra']],
      ['work', 'AI 工作台', 0, ['chat', 'files']],
      ['system', '系統管理', 0, ['admin']],
      ['diagnosis', '系統管理 › 診斷', 1, ['logs']],
    ]);
    expect(navigationSections(navigationTree([], groups))).toEqual([]);
    expect(navigationSections(navigationTree([], []), { keepTop: true })).toHaveLength(1);
  });

  it('lists a group with all its descendants', () => {
    expect([...descendantIds(groups, 'system')].sort()).toEqual(['diagnosis', 'system']);
    expect([...descendantIds(groups, 'work')]).toEqual(['work']);
  });

  it('falls back to one neutral icon for names the menu cannot draw', () => {
    expect(featureIcon('chat')).toBe('chat');
    expect(featureIcon('no-such-icon')).toBe('document');
    expect(featureIcon('')).toBe('document');
  });
});
