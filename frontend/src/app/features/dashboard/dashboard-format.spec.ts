import { describe, expect, it } from 'vitest';
import { dayRange, fillDays, metricLabel, workspaceLink } from './dashboard-format';
import { relativeTime } from '../../shared/browser/format';

describe('dashboard format', () => {
  it('reads a metric aloud with its unit, and unknown values as a dash', () => {
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
