import { describe, expect, it } from 'vitest';
import { hotkey } from './hotkeys';

const press = (init: KeyboardEventInit) => hotkey(new KeyboardEvent('keydown', init));

describe('hotkey', () => {
  it('treats Ctrl and ⌘ as the same modifier', () => {
    expect(press({ key: 'k', code: 'KeyK', ctrlKey: true })).toBe('mod+k');
    expect(press({ key: 'k', code: 'KeyK', metaKey: true })).toBe('mod+k');
  });
  it('reads letters from the physical key so Option combinations still match', () => {
    expect(press({ key: '˜', code: 'KeyN', metaKey: true, altKey: true })).toBe('mod+alt+n');
    expect(press({ key: 'L', code: 'KeyL', ctrlKey: true, shiftKey: true })).toBe('mod+shift+l');
  });
  it('keeps named keys', () => {
    expect(press({ key: 'Escape', code: 'Escape' })).toBe('escape');
  });
});
