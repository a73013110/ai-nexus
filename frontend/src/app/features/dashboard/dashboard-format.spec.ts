import { describe, expect, it } from 'vitest';
import {
  dayRange,
  figureMoney,
  fillDays,
  metricLabel,
  metricText,
  relativeTime,
  workspaceLink,
} from './dashboard-format';

describe('dashboard format', () => {
  it('shortens long amounts so headline money always fits its tile', () => {
    // Intl joins the code and the number with a no-break space, which keeps them on one line.
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
    expect(metricLabel({ value: 3, format: 'count', unit: '個' })).toBe('3 個');
    expect(metricLabel({ value: null, format: 'count', unit: '個' })).toBe('—');
  });

  it('only turns workspace paths into router links', () => {
    expect(workspaceLink('/tasks?job=abc')).toEqual({ path: '/tasks', query: { job: 'abc' } });
    expect(workspaceLink('/chat/1')).toEqual({ path: '/chat/1', query: {} });
    expect(workspaceLink('//evil.example')).toBeNull();
    expect(workspaceLink('https://example.com')).toBeNull();
    expect(workspaceLink('')).toBeNull();
  });

  it('fills every day of the range', () => {
    const days = dayRange('2026-02-27', '2026-03-02');
    expect(days).toEqual(['2026-02-27', '2026-02-28', '2026-03-01', '2026-03-02']);
    expect(
      fillDays(days, [
        { date: '2026-03-01', value: 2 },
        { date: '2026-03-01', value: 1 },
      ]),
    ).toEqual([
      { date: '2026-02-27', value: 0 },
      { date: '2026-02-28', value: 0 },
      { date: '2026-03-01', value: 3 },
      { date: '2026-03-02', value: 0 },
    ]);
  });

  it('describes recent times relative to now', () => {
    const now = Date.parse('2026-10-12T12:00:00Z');
    expect(relativeTime('2026-10-12T11:59:40Z', now)).toBe('剛剛');
    expect(relativeTime('2026-10-12T11:55:00Z', now)).toBe('5 分鐘前');
    expect(relativeTime('2026-08-01T00:00:00Z', now)).toBe('2026-08-01');
  });
});
