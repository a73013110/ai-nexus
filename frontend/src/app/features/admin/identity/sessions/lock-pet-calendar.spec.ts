import { describe, expect, it } from 'vitest';
import { nextLookChange, petLook } from './lock-pet-calendar';

/** 本地時間；月份照一般寫法從 1 起算。 */
const at = (year: number, month: number, day: number, hour = 15, minute = 0) =>
  new Date(year, month - 1, day, hour, minute);

describe('petLook', () => {
  it('follows the time of day: stretch in the morning, bento at lunch, bed in the evening', () => {
    expect(petLook(at(2026, 11, 12, 8)).moment).toBe('morning');
    expect(petLook(at(2026, 11, 12, 10)).moment).toBeNull();
    expect(petLook(at(2026, 11, 12, 12))).toEqual({ moment: 'lunch', hat: null, held: 'bento' });
    expect(petLook(at(2026, 11, 12, 15))).toEqual({ moment: null, hat: null, held: null });
    expect(petLook(at(2026, 11, 12, 18)).moment).toBe('evening');
    expect(petLook(at(2026, 11, 12, 5, 59)).moment).toBe('evening');
  });

  it('wears a Santa hat on Christmas Eve and Day, and a party hat on New Year’s Day', () => {
    expect(petLook(at(2026, 12, 24)).hat).toBe('santa');
    expect(petLook(at(2026, 12, 25)).hat).toBe('santa');
    expect(petLook(at(2026, 12, 26)).hat).toBeNull();
    expect(petLook(at(2027, 1, 1)).hat).toBe('party');
  });

  it('holds a red envelope from Lunar New Year’s Eve to the fifth day', () => {
    // 2026 年除夕是 2/16（臘月廿九，那年沒有三十），2027 年是 2/6（臘月三十）。
    expect(petLook(at(2026, 2, 15)).held).toBeNull();
    expect(petLook(at(2026, 2, 16)).held).toBe('red-envelope');
    expect(petLook(at(2026, 2, 21)).held).toBe('red-envelope');
    expect(petLook(at(2026, 2, 22)).held).toBeNull();
    expect(petLook(at(2027, 2, 6)).held).toBe('red-envelope');
    // 節日的道具優先於午餐的便當。
    expect(petLook(at(2026, 2, 17, 12)).held).toBe('red-envelope');
  });

  it('holds a mooncake at the Mid-Autumn Festival', () => {
    expect(petLook(at(2026, 9, 25)).held).toBe('mooncake');
    expect(petLook(at(2026, 9, 26)).held).toBeNull();
  });
});

describe('nextLookChange', () => {
  it('is the next time-of-day boundary, or midnight after the last one', () => {
    expect(nextLookChange(at(2026, 11, 12, 5, 30))).toEqual(at(2026, 11, 12, 6));
    expect(nextLookChange(at(2026, 11, 12, 10))).toEqual(at(2026, 11, 12, 11, 30));
    expect(nextLookChange(at(2026, 11, 12, 13, 30))).toEqual(at(2026, 11, 12, 18));
    expect(nextLookChange(at(2026, 12, 31, 19))).toEqual(at(2027, 1, 1, 0));
  });
});
