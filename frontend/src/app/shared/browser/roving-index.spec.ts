import { describe, expect, it } from 'vitest';
import { rovingIndex } from './roving-index';

describe('rovingIndex', () => {
  it('wraps along the chosen axis and jumps to either end', () => {
    expect(rovingIndex('ArrowDown', 2, 3)).toBe(0);
    expect(rovingIndex('ArrowUp', 0, 3)).toBe(2);
    expect(rovingIndex('ArrowDown', -1, 3)).toBe(0);
    expect(rovingIndex('ArrowRight', 2, 3, { orientation: 'horizontal' })).toBe(0);
    expect(rovingIndex('Home', 1, 3)).toBe(0);
    expect(rovingIndex('End', 1, 3)).toBe(2);
  });
  it('stops at the edges without wrapping and ignores keys of the other axis', () => {
    expect(rovingIndex('ArrowDown', 2, 3, { wrap: false })).toBe(2);
    expect(rovingIndex('ArrowUp', 0, 3, { wrap: false })).toBe(0);
    expect(rovingIndex('ArrowLeft', 1, 3)).toBeNull();
    expect(rovingIndex('ArrowDown', 1, 3, { orientation: 'horizontal' })).toBeNull();
    expect(rovingIndex('Enter', 1, 3)).toBeNull();
  });
});
