import { describe, expect, it } from 'vitest';
import {
  formatModelDisplayName,
  formatModelName,
  formatDateTimeInput,
  parseDateTimeInput,
  formatDate,
  formatDay,
  compactNumber,
  formatDuration,
  figureMoney,
  metricFormatter,
  metricText,
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
    expect(formatDay('2026-12-31T16:05:09Z')).toBe('2027/01/01');
    expect(formatDay('2026-12-31T16:05:09Z', 'roc')).toBe('民國 116/01/01');
    expect(Number.isNaN(parseDateTimeInput('2026-02-30T00:00:00').getTime())).toBe(true);
  });
});

describe('figures and metrics', () => {
  it('shortens long amounts so headline money always fits its tile', () => {
    // Intl 會以不斷行空白連接代碼與數字，讓它們保持在同一行。
    const figure = (amount: number, currency: string) =>
      figureMoney(amount, currency).replace(/\s/g, ' ');
    expect(figure(15.1208, 'USD')).toBe('USD 15.12');
    expect(figure(0.0012345, 'USD')).toBe('USD 0.00123');
    expect(figure(0, 'USD')).toBe('USD 0.00');
    expect(figure(12_345.67, 'TWD')).toBe('TWD 1.23萬');
    expect(figure(98_765_432_109.5, 'TWD')).toBe('TWD 988億');
    for (const amount of [1, 9_999.99, 123_456_789, 9.87e14])
      expect(figure(amount, 'USD').length).toBeLessThanOrEqual(14);
  });

  it('formats metrics by kind and never shows unknown as zero', () => {
    expect(metricText(null, 'count')).toBe('—');
    expect(metricText(1234.567, 'count')).toBe('1,234.57');
    expect(metricText(2_500_000, 'bytes')).toBe('2.5 MB');
    expect(metricText(8.26, 'percent')).toBe('8.3%');
    expect(metricText(86.4, 'percent')).toBe('86%');
    expect(metricText(420, 'duration')).toBe('420 ms');
    expect(metricText(4820, 'duration')).toBe('4.8 秒');
    expect(metricText(Number.NaN, 'bytes')).toBe('—');
    expect(metricText(75_000, 'duration')).toBe('1 分 15 秒');
  });

  it('reads durations in the two largest units', () => {
    expect(formatDuration(4820)).toBe('4.8 秒');
    expect(formatDuration(75_000)).toBe('1 分 15 秒');
    expect(formatDuration(120_000)).toBe('2 分');
    expect(formatDuration(39_888_000)).toBe('11 小時 4 分');
    expect(formatDuration(7_200_000)).toBe('2 小時');
    expect(formatDuration(183_600_000)).toBe('2 天 3 小時');
  });

  it('shortens counts for headline figures and reuses one formatter per kind', () => {
    expect(compactNumber(12_345)).toBe('1.2萬');
    expect(compactNumber(980)).toBe('980');
    const percent = metricFormatter('percent');
    expect(percent(4.24)).toBe('4.2%');
    expect(percent(42.4)).toBe('42%');
  });
});
