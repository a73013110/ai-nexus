import { describe, expect, it } from 'vitest';
import { formatModelDisplayName, formatModelName } from './format';

describe('model labels', () => {
  it('uses only display names, including when a historical label is missing', () => {
    const model = {
      id: 'ollama/hf.co/provider/private-model:Q3_K_XL',
      provider: 'ollama',
      displayName: '本地助理',
    };
    expect(formatModelName(model)).toBe('本地助理');
    expect(formatModelDisplayName({ modelDisplayName: model.displayName })).toBe('本地助理');
    expect(formatModelDisplayName({ modelDisplayName: null })).toBe('AI 助理');
    expect(formatModelDisplayName({})).toBe('AI 助理');
    expect(formatModelName({ displayName: ' ' })).toBe('AI 助理');
  });
});
