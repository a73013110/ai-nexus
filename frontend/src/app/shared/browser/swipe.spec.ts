import { describe, expect, it } from 'vitest';
import { SwipeTracker } from './swipe';

describe('SwipeTracker', () => {
  it('follows the way out and resists the other way', () => {
    const down = new SwipeTracker(1, 100, 0);
    expect(down.move(160, 50)).toBe(60);
    expect(down.move(40, 100)).toBe(-10);
    const left = new SwipeTracker(-1, 300, 0);
    expect(left.move(200, 50)).toBe(-100);
    expect(left.move(360, 100)).toBe(10);
  });
  it('dismisses past the distance', () => {
    const swipe = new SwipeTracker(1, 0, 0);
    swipe.move(130, 1000);
    expect(swipe.dismisses(120, 1300)).toBe(true);
    expect(swipe.dismisses(140, 1300)).toBe(false);
  });
  it('dismisses a quick flick short of the distance', () => {
    const swipe = new SwipeTracker(1, 0, 0);
    swipe.move(10, 20);
    swipe.move(40, 60);
    expect(swipe.dismisses(120, 64)).toBe(true);
  });
  it('ignores speed once the pointer paused, and speed the wrong way', () => {
    const paused = new SwipeTracker(1, 0, 0);
    paused.move(40, 40);
    expect(paused.dismisses(120, 400)).toBe(false);
    const back = new SwipeTracker(1, 0, 0);
    back.move(80, 400);
    back.move(40, 440);
    expect(back.velocity(440)).toBeLessThan(0);
    expect(back.dismisses(120, 440)).toBe(false);
  });
});
