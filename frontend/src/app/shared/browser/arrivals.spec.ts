import { describe, expect, it } from 'vitest';
import { arrivals } from './arrivals';

describe('arrivals', () => {
  it('treats only items that appear after the first list as new, including ones that come back', () => {
    const arrived = arrivals();
    const first = arrived(['a', 'b']);
    expect(first('a')).toBe(false);
    expect(first('b')).toBe(false);

    const second = arrived(['a', 'c']);
    expect(second('a')).toBe(false);
    expect(second('c')).toBe(true);

    // b 消失過，再出現時是新的元素。
    expect(arrived(['a', 'b'])('b')).toBe(true);
  });

  it('treats everything as new when the first list was empty', () => {
    const arrived = arrivals();
    arrived([]);
    expect(arrived(['a'])('a')).toBe(true);
  });
});
