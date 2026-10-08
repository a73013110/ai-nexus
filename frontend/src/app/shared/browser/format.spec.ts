import { describe, expect, it } from 'vitest';
import {
  formatModelDisplayName,
  formatModelName,
  formatDateTimeInput,
  parseDateTimeInput,
  formatDate,
} from './format';

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

describe('workspace date inputs', () => {
  it('round-trips Taipei dates across a UTC date boundary, independently of browser time zone', () => {
    const date = new Date('2026-12-31T16:05:00Z');
    expect(formatDateTimeInput(date)).toBe('2027-01-01T00:05:00');
    expect(parseDateTimeInput('2027-01-01T00:05').toISOString()).toBe(date.toISOString());
    expect(Number.isNaN(parseDateTimeInput('').getTime())).toBe(true);
    expect(formatDate('2026-12-31T16:05:09Z')).toBe('2027/01/01 00:05:09');
    expect(formatDate('2026-12-31T16:05:09Z', 'roc')).toBe('民國 116/01/01 00:05:09');
    expect(Number.isNaN(parseDateTimeInput('2026-02-30T00:00:00').getTime())).toBe(true);
  });
});
