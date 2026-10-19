import { describe, expect, it } from 'vitest';
import type { NavigationItemDto } from '@core/api/schema';
import {
  MAX_SORT_ORDER,
  applyWrites,
  canPlace,
  depthOf,
  editorTree,
  heightOf,
  level,
  moveWrites,
  shiftId,
  type EditorNode,
} from './navigation-order';

const item = (
  id: string,
  parentId: string | null,
  sortOrder: number,
  kind = 'page',
  featureId: string | null = kind === 'page' ? id : null,
): NavigationItemDto => ({
  id,
  parentId,
  kind,
  featureId,
  name: kind === 'page' ? null : id,
  url: kind === 'link' ? 'https://example.com/' : null,
  openInNewTab: true,
  icon: null,
  sortOrder,
  audienceGroupIds: [],
});
const group = (id: string, parentId: string | null, sortOrder: number) =>
  item(id, parentId, sortOrder, 'group');
const items = [
  group('system', null, 40),
  item('dashboard', null, 10),
  group('work', null, 20),
  item('chat', 'work', 10),
  item('files', 'work', 10),
  item('projects', 'work', 15),
  group('system.access', 'system', 10),
  item('identity.users', 'system.access', 10),
  item('wiki', 'system', 20, 'link'),
];
const ids = (list: readonly { id: string }[]) => list.map((entry) => entry.id);
const shape = (nodes: EditorNode[]): unknown[] =>
  nodes.map((node) => (node.children.length ? [node.item.id, shape(node.children)] : node.item.id));

describe('navigation order', () => {
  it('builds the tree in server order and keeps unknown or cyclic parents at the top', () => {
    expect(shape(editorTree(items))).toEqual([
      'dashboard',
      ['work', ['chat', 'files', 'projects']],
      ['system', [['system.access', ['identity.users']], 'wiki']],
    ]);
    const broken = [
      group('a', 'b', 1),
      group('b', 'a', 2),
      item('lost', 'missing', 3),
      item('inside', 'a', 1),
    ];
    expect(shape(editorTree(broken))).toEqual([['a', ['inside']], 'b', 'lost']);
  });

  it('previews a placement anywhere in the tree', () => {
    expect(shape(editorTree(items, { id: 'files', parentId: null, index: 1 }))).toEqual([
      'dashboard',
      'files',
      ['work', ['chat', 'projects']],
      ['system', [['system.access', ['identity.users']], 'wiki']],
    ]);
    expect(shape(editorTree(items, { id: 'chat', parentId: 'work', index: 2 }))[1]).toEqual([
      'work',
      ['files', 'projects', 'chat'],
    ]);
  });

  it('measures depth and height and offers only the places the server accepts', () => {
    expect(depthOf(items, null)).toBe(0);
    expect(depthOf(items, 'identity.users')).toBe(3);
    expect(heightOf(items, 'system')).toBe(3);
    expect(heightOf(items, 'wiki')).toBe(1);
    expect(canPlace(items, 'files', 'system.access')).toBe(true);
    // 上層必須是分群；分群不能移到自己的子孫之下。
    expect(canPlace(items, 'files', 'chat')).toBe(false);
    expect(canPlace(items, 'system', 'system.access')).toBe(false);
    // 同層不可重複同一頁面，換到其他層可以。
    const copy = [...items, item('chat-copy', null, 50, 'page', 'chat')];
    expect(canPlace(copy, 'chat-copy', 'work')).toBe(false);
    expect(canPlace(copy, 'chat-copy', 'system')).toBe(true);
    // 最多四層：第三層的分群之下還能放葉節點，不能再放有子節點的分群。
    const deep = [
      ...items,
      group('system.access.more', 'system.access', 20),
      item('leaf', 'system.access.more', 10),
    ];
    expect(canPlace(deep, 'files', 'system.access.more')).toBe(true);
    expect(canPlace(deep, 'work', 'system.access.more')).toBe(false);
    expect(canPlace(deep, 'work', 'system.access')).toBe(true);
  });

  it('shifts within the list and clamps at either end', () => {
    const list = ['a', 'b', 'c', 'd'];
    expect(shiftId(list, 'c', -1)).toEqual(['a', 'c', 'b', 'd']);
    expect(shiftId(list, 'b', 'last')).toEqual(['a', 'c', 'd', 'b']);
    expect(shiftId(list, 'a', -1)).toEqual(list);
    expect(shiftId(list, 'missing', 1)).toEqual(list);
  });

  it('writes only the moved node when its new gap has room, with its new parent', () => {
    // chat（10）與 files（10）同值時依 id 排；projects 移到兩者之間沒有空間，整層重新編號。
    expect(moveWrites(items, 'projects', 'work', 1)).toEqual([
      { id: 'projects', parentId: 'work', sortOrder: 20 },
      { id: 'files', parentId: 'work', sortOrder: 30 },
    ]);
    // 移到其他層的最前、最後與中間。
    expect(moveWrites(items, 'files', null, 0)).toEqual([
      { id: 'files', parentId: null, sortOrder: 0 },
    ]);
    expect(moveWrites(items, 'files', null, 3)).toEqual([
      { id: 'files', parentId: null, sortOrder: 50 },
    ]);
    expect(moveWrites(items, 'files', null, 1)).toEqual([
      { id: 'files', parentId: null, sortOrder: 15 },
    ]);
    // 移進空分群：從 10 開始。
    expect(moveWrites([...items, group('empty', null, 60)], 'chat', 'empty', 0)).toEqual([
      { id: 'chat', parentId: 'empty', sortOrder: 10 },
    ]);
    expect(moveWrites(items, 'chat', 'work', 0)).toEqual([]);
  });

  it('renumbers the target level with even spacing when there is no room', () => {
    const last = [item('a', null, 5), item('b', null, MAX_SORT_ORDER)];
    expect(moveWrites(last, 'a', null, 1)).toEqual([
      { id: 'b', parentId: null, sortOrder: 10 },
      { id: 'a', parentId: null, sortOrder: 20 },
    ]);
    const first = [item('a', null, 0), item('b', null, 20)];
    expect(moveWrites(first, 'b', null, 0)).toEqual([
      { id: 'b', parentId: null, sortOrder: 10 },
      { id: 'a', parentId: null, sortOrder: 20 },
    ]);
  });

  it('keeps every write inside the accepted range and the result in the requested order', () => {
    let current = [
      item('dashboard', null, 5),
      item('chat', null, 10),
      item('projects', null, 15),
      item('files', null, 20),
      item('knowledge', null, 30),
    ];
    for (let step = 0; step < 40; step++) {
      const list = ids(level(current, null));
      const moved = list[(step * 7) % list.length];
      const target = shiftId(list, moved, step % 3 === 0 ? 'first' : step % 2 ? 1 : -2);
      const writes = moveWrites(current, moved, null, target.indexOf(moved));
      for (const write of writes) {
        expect(write.sortOrder).toBeGreaterThanOrEqual(0);
        expect(write.sortOrder).toBeLessThanOrEqual(MAX_SORT_ORDER);
      }
      current = applyWrites(current, writes);
      expect(ids(level(current, null))).toEqual(target);
    }
  });
});
