import { describe, expect, it } from 'vitest';
import { navigationSections, navigationTree } from '@core/layout/navigation-tree';
import {
  MAX_SORT_ORDER,
  nextSortOrder,
  orderFeatures,
  reorderWrites,
  shiftId,
} from './feature-order';

const work = [
  { id: 'dashboard', sortOrder: 5 },
  { id: 'chat', sortOrder: 10 },
  { id: 'projects', sortOrder: 15 },
  { id: 'files', sortOrder: 20 },
  { id: 'knowledge', sortOrder: 30 },
];
const ids = (list: readonly { id: string }[]) => list.map((x) => x.id);

describe('feature order', () => {
  it('orders like the server, then groups like the sidebar', () => {
    const features = [
      { id: 'admin', sortOrder: 90, groupId: 'system' },
      { id: 'files', sortOrder: 20, groupId: 'work' },
      { id: 'chat', sortOrder: 20, groupId: 'work' },
      { id: 'dashboard', sortOrder: 5, groupId: 'work' },
    ];
    const groups = [
      { id: 'work', parentId: null, name: '工作台', sortOrder: 10 },
      { id: 'system', parentId: null, name: '系統管理', sortOrder: 30 },
    ];
    expect(ids(orderFeatures(features))).toEqual(['dashboard', 'chat', 'files', 'admin']);
    const overridden = orderFeatures(features, { dashboard: 95 });
    expect(ids(overridden)).toEqual(['chat', 'files', 'admin', 'dashboard']);
    expect(overridden.at(-1)!.sortOrder).toBe(95);
    expect(features[3].sortOrder).toBe(5);
    expect(
      navigationSections(navigationTree(overridden, groups)).map((section) => [
        section.id,
        ids(section.features),
      ]),
    ).toEqual([
      ['work', ['chat', 'files', 'dashboard']],
      ['system', ['admin']],
    ]);
  });

  it('places a moved feature or group after its new siblings, within range', () => {
    expect(nextSortOrder([])).toBe(10);
    expect(nextSortOrder(work)).toBe(40);
    expect(nextSortOrder([{ sortOrder: MAX_SORT_ORDER - 3 }])).toBe(MAX_SORT_ORDER);
  });

  it('shifts within the list and clamps at either end', () => {
    const list = ids(work);
    expect(shiftId(list, 'files', -1)).toEqual([
      'dashboard',
      'chat',
      'files',
      'projects',
      'knowledge',
    ]);
    expect(shiftId(list, 'chat', 'last')).toEqual([
      'dashboard',
      'projects',
      'files',
      'knowledge',
      'chat',
    ]);
    expect(shiftId(list, 'dashboard', -1)).toEqual(list);
    expect(shiftId(list, 'missing', 1)).toEqual(list);
  });

  it('writes only the moved feature when its new gap has room', () => {
    // Between chat (10) and projects (15).
    expect(reorderWrites(work, 'files', shiftId(ids(work), 'files', -1))).toEqual([
      { id: 'files', sortOrder: 12 },
    ]);
    // Past either end: 10 beyond the neighbour, never below 0.
    expect(reorderWrites(work, 'chat', shiftId(ids(work), 'chat', 'last'))).toEqual([
      { id: 'chat', sortOrder: 40 },
    ]);
    expect(reorderWrites(work, 'files', shiftId(ids(work), 'files', 'first'))).toEqual([
      { id: 'files', sortOrder: 0 },
    ]);
    expect(reorderWrites(work, 'chat', ids(work))).toEqual([]);
  });

  it('renumbers the group with even spacing when there is no room', () => {
    const tight = [
      { id: 'a', sortOrder: 10 },
      { id: 'b', sortOrder: 11 },
      { id: 'c', sortOrder: 30 },
    ];
    expect(reorderWrites(tight, 'c', ['a', 'c', 'b'])).toEqual([
      { id: 'c', sortOrder: 20 },
      { id: 'b', sortOrder: 30 },
    ]);
    const tied = [
      { id: 'a', sortOrder: 10 },
      { id: 'b', sortOrder: 10 },
      { id: 'c', sortOrder: 40 },
    ];
    expect(reorderWrites(tied, 'c', ['a', 'c', 'b'])).toEqual([
      { id: 'c', sortOrder: 20 },
      { id: 'b', sortOrder: 30 },
    ]);
    const first = [
      { id: 'a', sortOrder: 0 },
      { id: 'b', sortOrder: 20 },
    ];
    expect(reorderWrites(first, 'b', ['b', 'a'])).toEqual([
      { id: 'b', sortOrder: 10 },
      { id: 'a', sortOrder: 20 },
    ]);
    const last = [
      { id: 'a', sortOrder: 5 },
      { id: 'b', sortOrder: MAX_SORT_ORDER },
    ];
    expect(reorderWrites(last, 'a', ['b', 'a'])).toEqual([
      { id: 'b', sortOrder: 10 },
      { id: 'a', sortOrder: 20 },
    ]);
  });

  it('keeps every write inside the accepted range and the result in the requested order', () => {
    let group = work.map((feature) => ({ ...feature }));
    for (let step = 0; step < 40; step++) {
      const moved = group[(step * 7) % group.length].id;
      const target = shiftId(ids(group), moved, step % 3 === 0 ? 'first' : step % 2 ? 1 : -2);
      for (const write of reorderWrites(group, moved, target)) {
        expect(write.sortOrder).toBeGreaterThanOrEqual(0);
        expect(write.sortOrder).toBeLessThanOrEqual(MAX_SORT_ORDER);
        group.find((feature) => feature.id === write.id)!.sortOrder = write.sortOrder;
      }
      group = orderFeatures(group);
      expect(ids(group)).toEqual(target);
    }
  });
});
