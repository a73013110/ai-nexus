import { describe, expect, it } from 'vitest';
import { nexusHalves, nexusOutline, nexusTips, pointAlong } from './nexus-logo';

describe('Nexus mark outline', () => {
  it('splits into two mirrored halves that meet at the stem tips', () => {
    const [items, items2] = nexusHalves;
    expect(items.length + items2.length - 2).toBe(nexusOutline.length);
    expect(items[0]).toEqual(nexusTips[0]);
    expect(items.at(-1)).toEqual(nexusTips[1]);
    expect(items2[0]).toEqual(nexusTips[1]);
    expect(items2.at(-1)).toEqual(nexusTips[0]);
    items.forEach((part, index) => {
      expect(items2[index].x).toBeCloseTo(-part.x, 9);
      expect(items2[index].y).toBeCloseTo(-part.y, 9);
    });
  });
  it('travels by arc length, so long edges are not faster than short ones', () => {
    const line = [
      { x: 0, y: 0 },
      { x: 1, y: 0 },
      { x: 1, y: 3 },
    ];
    expect(pointAlong(line, 0)).toEqual({ x: 0, y: 0 });
    expect(pointAlong(line, 0.25)).toEqual({ x: 1, y: 0 });
    expect(pointAlong(line, 0.5).y).toBeCloseTo(1, 9);
    expect(pointAlong(line, 1)).toEqual({ x: 1, y: 3 });
    expect(pointAlong(line, 2)).toEqual({ x: 1, y: 3 });
  });
});
