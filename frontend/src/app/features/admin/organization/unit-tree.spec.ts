import { describe, expect, it } from 'vitest';
import type { UnitDto } from '@core/api/schema';
import { heightOf, moved, subtree, unitRows } from './unit-tree';

const unit = (id: string, parentId: string | null, sortOrder: number, enabled = true): UnitDto => ({
  id,
  parentId,
  name: id,
  enabled,
  sortOrder,
  memberCount: 0,
  gdDepNo: null,
});
const units = [
  unit('hr', 'company', 20),
  unit('company', null, 10),
  unit('it', 'company', 10),
  unit('systems', 'it', 10),
  unit('closed', 'company', 30, false),
  unit('under-closed', 'closed', 10),
];

describe('unit tree', () => {
  it('lists units depth-first in sibling order with their paths', () => {
    const rows = unitRows(units);
    expect(rows.map((row) => row.unit.id)).toEqual([
      'company',
      'it',
      'systems',
      'hr',
      'closed',
      'under-closed',
    ]);
    expect(rows.find((row) => row.unit.id === 'systems')).toMatchObject({
      depth: 3,
      path: ['company', 'it', 'systems'],
      children: 0,
    });
    expect(rows.find((row) => row.unit.id === 'company')?.children).toBe(3);
  });

  it('marks the whole subtree of a disabled unit inactive', () => {
    const inactive = unitRows(units).filter((row) => row.inactive);
    expect(inactive.map((row) => row.unit.id)).toEqual(['closed', 'under-closed']);
  });

  it('hides the descendants of collapsed units and keeps orphans visible', () => {
    expect(unitRows(units, new Set(['company'])).map((row) => row.unit.id)).toEqual(['company']);
    expect(unitRows([unit('orphan', 'gone', 10)]).map((row) => row.depth)).toEqual([1]);
  });

  it('measures height and the subtree a move cannot target', () => {
    expect(heightOf(units, 'company')).toBe(3);
    expect(heightOf(units, 'systems')).toBe(1);
    expect([...subtree(units, 'it')].sort()).toEqual(['it', 'systems']);
  });

  it('swaps a unit with its neighbour and sends the whole level', () => {
    expect(moved(units, 'hr', -1)).toEqual({
      parentId: 'company',
      unitIds: ['hr', 'it', 'closed'],
    });
    expect(moved(units, 'it', -1)).toBeNull();
    expect(moved(units, 'closed', 1)).toBeNull();
    expect(moved(units, 'company', 1)).toBeNull();
  });
});
