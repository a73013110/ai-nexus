import { describe, expect, it } from 'vitest';
import { applySortWrites, bySortOrder, shiftId, sortWrites, type Sortable } from './sort-order';

const item = (id: string, sortOrder: number | null): Sortable => ({ id, sortOrder });
const byId = (first: Sortable, second: Sortable) => (first.id < second.id ? -1 : 1);
const MAX = 1_000_000;

describe('sort order', () => {
  const mixed = [item('a', 10), item('b', 20), item('c', 30), item('d', null), item('e', null)];

  it('writes only the moved item between its new neighbours', () => {
    expect(sortWrites(mixed, 'c', 1, MAX)).toEqual([{ id: 'c', sortOrder: 15 }]);
    expect(sortWrites(mixed, 'a', 2, MAX)).toEqual([{ id: 'a', sortOrder: 40 }]);
    expect(sortWrites(mixed, 'c', 0, MAX)).toEqual([{ id: 'c', sortOrder: 0 }]);
  });

  it('keeps unsorted items unsorted unless the move passes them', () => {
    // 移到已排序的最後、未排序者之前：只寫它自己。
    expect(sortWrites(mixed, 'e', 3, MAX)).toEqual([{ id: 'e', sortOrder: 40 }]);
    // 移到未排序者之後：它上方的未排序者依序接上，之後的人仍未排序。
    expect(sortWrites(mixed, 'a', 3, MAX)).toEqual([
      { id: 'd', sortOrder: 40 },
      { id: 'a', sortOrder: 50 },
    ]);
    const unsorted = [item('x', null), item('y', null), item('z', null)];
    expect(sortWrites(unsorted, 'z', 0, MAX)).toEqual([{ id: 'z', sortOrder: 10 }]);
    expect(sortWrites(unsorted, 'x', 1, MAX)).toEqual([
      { id: 'y', sortOrder: 10 },
      { id: 'x', sortOrder: 20 },
    ]);
  });

  it('writes nothing when dropped in place', () => {
    expect(sortWrites(mixed, 'b', 1, MAX)).toEqual([]);
    expect(sortWrites(mixed, 'e', 4, MAX)).toEqual([]);
  });

  it('renumbers the sorted range when there is no room left', () => {
    const tight = [item('a', 10), item('b', 11), item('c', 12), item('d', null)];
    expect(sortWrites(tight, 'c', 1, MAX)).toEqual([
      { id: 'c', sortOrder: 20 },
      { id: 'b', sortOrder: 30 },
    ]);
    // 上限不夠間隔 10 時縮小間隔。
    const full = [item('a', 1), item('b', 2), item('c', 3)];
    expect(sortWrites(full, 'a', 2, 3)).toEqual([
      { id: 'b', sortOrder: 1 },
      { id: 'c', sortOrder: 2 },
      { id: 'a', sortOrder: 3 },
    ]);
  });

  it('always writes an item that comes from another level', () => {
    expect(sortWrites([item('a', 10)], 'new', 1, MAX)).toEqual([{ id: 'new', sortOrder: 20 }]);
    expect(sortWrites([], 'new', 0, MAX)).toEqual([{ id: 'new', sortOrder: 10 }]);
  });

  it('applies writes and orders sorted items before unsorted ones', () => {
    const next = applySortWrites(mixed, sortWrites(mixed, 'e', 0, MAX)).sort(bySortOrder(byId));
    expect(next.map((entry) => entry.id)).toEqual(['e', 'a', 'b', 'c', 'd']);
  });

  it('shifts an id within bounds or to either end', () => {
    const ids = ['a', 'b', 'c', 'd'];
    expect(shiftId(ids, 'c', -1)).toEqual(['a', 'c', 'b', 'd']);
    expect(shiftId(ids, 'b', 'last')).toEqual(['a', 'c', 'd', 'b']);
    expect(shiftId(ids, 'b', 10)).toEqual(['a', 'c', 'd', 'b']);
    expect(shiftId(ids, 'missing', 1)).toEqual(ids);
  });
});
