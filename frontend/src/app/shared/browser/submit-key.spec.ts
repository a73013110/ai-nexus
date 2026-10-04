import { describe, expect, it } from 'vitest';
import { isSubmitKey } from './submit-key';
describe('shared composer submit key', () => {
  it('never submits during IME composition or a requested line break', () => {
    expect(
      isSubmitKey(new KeyboardEvent('keydown', { key: 'Enter', isComposing: true }), false, true),
    ).toBe(false);
    expect(isSubmitKey(new KeyboardEvent('keydown', { key: 'Enter' }), true, true)).toBe(false);
    expect(
      isSubmitKey(new KeyboardEvent('keydown', { key: 'Enter', shiftKey: true }), false, true),
    ).toBe(false);
  });
  it('requires Ctrl or Meta when Enter is configured to insert a line break', () => {
    expect(isSubmitKey(new KeyboardEvent('keydown', { key: 'Enter' }), false, false)).toBe(false);
    expect(
      isSubmitKey(new KeyboardEvent('keydown', { key: 'Enter', ctrlKey: true }), false, false),
    ).toBe(true);
  });
});
