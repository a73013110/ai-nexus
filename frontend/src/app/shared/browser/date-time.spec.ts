import { describe, expect, it } from 'vitest';
import {
  formatCalendarValue,
  parseCalendarText,
  parseLocalDateTime,
  shiftCalendarMonth,
} from './date-time';

describe('calendar adapters and wall-clock arithmetic', () => {
  it('round-trips ROC dates without changing the Gregorian value or seconds', () => {
    expect(formatCalendarValue('2026-10-08T14:30:45', 'roc')).toBe('民國 115/10/08 14:30:45');
    expect(parseCalendarText('115/10/08 14:30:45', 'roc')).toBe('2026-10-08T14:30:45');
    expect(parseCalendarText('民國 001/01/01', 'roc', false)).toBe('1912-01-01');
    expect(parseCalendarText('000/01/01', 'roc', false)).toBe('');
  });
  it('rejects impossible dates and clock overflows rather than silently normalizing them', () => {
    for (const value of [
      '2026/02/29 12:00:00',
      '2026/04/31 12:00:00',
      '2026/10/08 24:00:00',
      '2026/10/08 23:60:00',
      '2026/10/08 23:59:60',
      '10000/01/01 00:00:00',
      '26/10/08 23:59:59',
    ])
      expect(parseCalendarText(value)).toBe('');
    expect(parseCalendarText('2024/02/29 23:59:59')).toBe('2024-02-29T23:59:59');
    expect(parseLocalDateTime('2026-02-30T00:00:00')).toBeNull();
  });
  it('clamps month/year navigation to the last valid day, including leap years', () => {
    const last = parseLocalDateTime('2024-01-31T12:30:45')!;
    expect(shiftCalendarMonth(last, 1)).toEqual({ ...last, month: 2, day: 29 });
    expect(shiftCalendarMonth({ ...last, month: 2, day: 29 }, 12)).toEqual({
      ...last,
      year: 2025,
      month: 2,
      day: 28,
    });
  });
});
