import { describe, expect, it } from 'vitest';
import { hotkey, keyCaps, keyText } from './hotkeys';

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
  it('ignores the bare keydown Chrome autofill dispatches', () => {
    expect(hotkey(new Event('keydown') as KeyboardEvent)).toBe('');
  });
});

describe('keyCaps', () => {
  it('shows the platform modifiers', () => {
    expect(keyCaps('mod+alt+n', false)).toEqual(['Ctrl', 'Alt', 'N']);
    expect(keyCaps('mod+alt+n', true)).toEqual(['⌘', '⌥', 'N']);
  });
  it('names keys the way they are printed', () => {
    expect(keyCaps('escape', false)).toEqual(['Esc']);
    expect(keyCaps('f2', false)).toEqual(['F2']);
    expect(keyCaps('shift+enter', true)).toEqual(['⇧', 'Enter']);
  });
  it('joins plain text by platform convention', () => {
    expect(keyText('mod+k', false)).toBe('Ctrl+K');
    expect(keyText('mod+k', true)).toBe('⌘K');
  });
});
