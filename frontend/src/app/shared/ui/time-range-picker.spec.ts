import { describe, expect, it } from 'vitest';
import { CUSTOM_RANGE, hoursAgo, timeRangeBounds, timeRangeLabel } from './time-range-picker';

const presets = [
  { value: 'all', label: '不限時間' },
  { value: '1h', label: '最近 1 小時', duration: hoursAgo(1) },
];
const now = Date.parse('2026-10-16T00:00:00Z');

describe('time range', () => {
  it('ends a preset a minute after now and leaves an unbounded preset open', () => {
    expect(timeRangeBounds({ preset: '1h', from: '', to: '' }, presets, now)).toEqual({
      from: '2026-10-15T23:01:00.000Z',
      to: '2026-10-16T00:01:00.000Z',
    });
    expect(timeRangeBounds({ preset: 'all', from: '', to: '' }, presets, now)).toEqual({});
    expect(timeRangeBounds({ preset: 'gone', from: '', to: '' }, presets, now)).toBe(
      '請選擇時間範圍。',
    );
  });

  it('reads custom ends as Taipei wall-clock, a date alone to its next midnight', () => {
    expect(
      timeRangeBounds(
        { preset: CUSTOM_RANGE, from: '2026-10-01T08:00:00', to: '2026-10-01T09:30:00' },
        presets,
      ),
    ).toEqual({ from: '2026-10-01T00:00:00.000Z', to: '2026-10-01T01:30:00.000Z' });
    expect(
      timeRangeBounds({ preset: CUSTOM_RANGE, from: '2026-10-01', to: '2026-10-01' }, presets),
    ).toEqual({ from: '2026-09-30T16:00:00.000Z', to: '2026-10-01T16:00:00.000Z' });
    expect(timeRangeBounds({ preset: CUSTOM_RANGE, from: '', to: '2026-10-01' }, presets)).toEqual({
      from: undefined,
      to: '2026-10-01T16:00:00.000Z',
    });
  });

  it('rejects inverted or malformed custom ends', () => {
    expect(
      timeRangeBounds(
        { preset: CUSTOM_RANGE, from: '2026-10-02T00:00:00', to: '2026-10-01T00:00:00' },
        presets,
      ),
    ).toBe('結束時間須晚於起始時間。');
    expect(timeRangeBounds({ preset: CUSTOM_RANGE, from: '2026-13-01', to: '' }, presets)).toBe(
      '請輸入有效的起訖時間。',
    );
  });

  it('labels presets and custom ranges', () => {
    expect(timeRangeLabel({ preset: '1h', from: '', to: '' }, presets)).toBe('最近 1 小時');
    expect(
      timeRangeLabel({ preset: CUSTOM_RANGE, from: '2026-10-01', to: '2026-10-03' }, presets),
    ).toBe('2026/10/01 — 2026/10/03');
    expect(timeRangeLabel({ preset: CUSTOM_RANGE, from: '2026-10-01', to: '' }, presets)).toBe(
      '2026/10/01 起',
    );
  });
});
