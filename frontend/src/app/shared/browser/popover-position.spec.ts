import { describe, expect, it } from 'vitest';
import { placeBeside } from './popover-position';

const viewport = { width: 1000, height: 800 };
const size = { width: 300, height: 200 };
const box = (left: number, top: number, width = 100, height = 40) => ({ left, top, width, height });

describe('placeBeside', () => {
  it('uses the preferred side and centers on the anchor', () => {
    expect(placeBeside(box(400, 400), size, 'top', viewport)).toEqual({
      left: 300,
      top: 188,
      side: 'top',
      originX: 150,
      originY: 200,
    });
  });

  it('falls back to another side and stays inside the viewport', () => {
    const place = placeBeside(box(20, 20), size, 'top', viewport);
    expect(place).toMatchObject({ left: 12, top: 72, side: 'bottom', originX: 58, originY: 0 });
  });

  it('puts the panel beside a tall anchor', () => {
    const place = placeBeside(box(50, 20, 200, 760), size, 'left', viewport);
    expect(place).toMatchObject({ left: 262, top: 300, side: 'right', originX: 0, originY: 100 });
  });

  it('overlaps the bottom of the viewport when no side fits, and centers without an anchor', () => {
    expect(placeBeside(box(0, 0, 1000, 800), size, 'bottom', viewport)).toMatchObject({
      left: 350,
      top: 588,
    });
    expect(placeBeside(undefined, size, 'bottom', viewport)).toMatchObject({
      left: 350,
      top: 300,
    });
  });
});
