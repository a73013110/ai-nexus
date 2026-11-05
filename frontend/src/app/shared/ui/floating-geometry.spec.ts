import { describe, expect, it } from 'vitest';
import {
  anchorOf,
  collapseTo,
  expandFrom,
  FLOATING_LIMITS,
  moved,
  place,
  rectOf,
  resized,
  snapped,
} from './floating-geometry';

const viewport = { width: 1200, height: 800 };

describe('floating geometry', () => {
  it('keeps the side it was anchored to when the window resizes', () => {
    const placement = {
      ...anchorOf({ x: 784, y: 16, width: 400, height: 600 }, viewport),
      width: 400,
      height: 600,
    };
    expect(placement).toMatchObject({ side: 'end', inline: 16, edge: 'top', block: 16 });
    expect(rectOf(placement, { width: 1600, height: 900 })).toEqual({
      x: 1184,
      y: 16,
      width: 400,
      height: 600,
    });
  });

  it('shrinks into a small window and never below the minimum when there is room', () => {
    const placement = {
      side: 'start',
      inline: 16,
      edge: 'top',
      block: 16,
      width: 200,
      height: 2000,
    } as const;
    expect(rectOf(placement, viewport)).toEqual({
      x: 16,
      y: 16,
      width: FLOATING_LIMITS.minWidth,
      height: viewport.height - 2 * FLOATING_LIMITS.margin,
    });
    expect(rectOf(placement, { width: 300, height: 300 }).width).toBe(268);
  });

  it('drags inside the window and snaps to an edge only when released near it', () => {
    const start = { x: 400, y: 100, width: 300, height: 400 };
    expect(moved(start, -900, 0, viewport).x).toBe(0);
    expect(snapped({ ...start, x: 30 }, viewport).x).toBe(16);
    expect(snapped({ ...start, x: 870 }, viewport).x).toBe(884);
    expect(snapped(start, viewport)).toEqual(start);
  });

  it('resizes from the dragged edge while the opposite edge stays put', () => {
    const start = { x: 600, y: 100, width: 400, height: 400 };
    const wider = resized(start, 'start', -100, 0, viewport);
    expect(wider).toEqual({ x: 500, y: 100, width: 500, height: 400 });
    const floor = resized(start, 'bottom-end', -500, -500, viewport);
    expect(floor).toMatchObject({
      x: 600,
      width: FLOATING_LIMITS.minWidth,
      height: FLOATING_LIMITS.minHeight,
    });
    expect(resized(start, 'bottom', 0, 900, viewport).height).toBe(800 - 100 - 16);
  });

  it('collapses into the nearer bottom corner and grows back from where the island is', () => {
    const panel = { x: 784, y: 16, width: 400, height: 600 };
    const anchor = collapseTo(panel, viewport);
    expect(anchor).toEqual({ side: 'end', inline: 16, edge: 'bottom', block: 184 });
    const island = place(anchor, { width: 160, height: 40 }, viewport);
    expect(island).toEqual({ x: 1024, y: 576, width: 160, height: 40 });
    expect(rectOf(expandFrom(island, panel, viewport), viewport)).toEqual(panel);
    // 島被拖到左上：面板從島的角落往下長，推回視窗內。
    const moved = expandFrom({ x: 16, y: 16, width: 160, height: 40 }, panel, viewport);
    expect(rectOf(moved, viewport)).toEqual({ x: 16, y: 16, width: 400, height: 600 });
  });
});
