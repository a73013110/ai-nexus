import { describe, expect, it } from 'vitest';
import { nexusHalves, nexusOutline, nexusTips, pointAlong } from './nexus-logo';

describe('Nexus mark outline', () => {
  it('splits into two mirrored halves that meet at the stem tips', () => {
    const [a, b] = nexusHalves;
    expect(a.length + b.length - 2).toBe(nexusOutline.length);
    expect(a[0]).toEqual(nexusTips[0]);
    expect(a.at(-1)).toEqual(nexusTips[1]);
    expect(b[0]).toEqual(nexusTips[1]);
    expect(b.at(-1)).toEqual(nexusTips[0]);
    a.forEach((p, i) => {
      expect(b[i].x).toBeCloseTo(-p.x, 9);
      expect(b[i].y).toBeCloseTo(-p.y, 9);
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
