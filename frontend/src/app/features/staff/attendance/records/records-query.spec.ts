import { convertToParamMap } from '@angular/router';
import { describe, expect, it } from 'vitest';
import { CUSTOM_RANGE } from '@shared/ui/time-range-picker';
import {
  emptyRecordsQuery,
  recordFilters,
  recordsQueryOptions,
  recordsRange,
} from './records-query';

// 2026-10-31 20:00 台北＝當天 UTC 12:00；瀏覽器在別的時區也以台北日期換算。
const NOW = Date.parse('2026-10-31T12:00:00Z');
const options = recordsQueryOptions({ person: () => '王小明', unit: () => undefined });

describe('records query', () => {
  it('turns presets into Taipei dates when applied', () => {
    expect(recordsRange({ preset: 'month', from: '', to: '' }, NOW)).toEqual({
      from: '2026-10-01',
      to: '2026-10-31',
    });
    expect(recordsRange({ preset: 'last-month', from: '', to: '' }, NOW)).toEqual({
      from: '2026-09-01',
      to: '2026-09-30',
    });
    expect(recordsRange({ preset: '7d', from: '', to: '' }, NOW)).toEqual({
      from: '2026-10-25',
      to: '2026-10-31',
    });
  });

  it('refuses custom ranges over 31 days or backwards', () => {
    const custom = (from: string, to: string) => ({ preset: CUSTOM_RANGE, from, to });
    expect(recordsRange(custom('2026-10-01', '2026-10-31'))).toEqual({
      from: '2026-10-01',
      to: '2026-10-31',
    });
    expect(recordsRange(custom('2026-10-01', '2026-11-01'))).toContain('31 天');
    expect(recordsRange(custom('2026-10-02', '2026-10-01'))).toContain('不能早於');
    expect(recordsRange(custom('2026-10-02', ''))).toContain('起訖');
  });

  it('keeps applied conditions in the URL and reads them back', () => {
    const query = {
      ...emptyRecordsQuery(),
      range: { preset: CUSTOM_RANGE, from: '2026-09-15', to: '2026-10-14' },
      userId: '3f2504e0-4f89-11d3-9a0c-0305e82c3301',
      includeExcluded: true,
    };
    const params = options.toParams!(query);
    expect(params).toEqual({
      range: undefined,
      from: '2026-09-15',
      to: '2026-10-14',
      user: '3f2504e0-4f89-11d3-9a0c-0305e82c3301',
      unit: undefined,
      excluded: 'true',
    });
    const clean = Object.fromEntries(Object.entries(params).filter(([, value]) => value));
    expect(options.fromParams!(convertToParamMap(clean), emptyRecordsQuery())).toEqual(query);
    expect(options.toParams!(emptyRecordsQuery())).toEqual({
      range: undefined,
      from: undefined,
      to: undefined,
      user: undefined,
      unit: undefined,
      excluded: undefined,
    });
    expect(options.chips!(query).map((chip) => chip.label)).toEqual([
      '區間：2026/09/15 — 10/14',
      '人員：王小明',
      '含不列入出勤的人',
    ]);
  });

  it('ignores malformed link parameters', () => {
    const query = options.fromParams!(
      convertToParamMap({ range: 'year', user: 'not-a-guid', unit: '1', excluded: 'yes' }),
      emptyRecordsQuery(),
    );
    expect(query).toEqual(emptyRecordsQuery());
    expect(recordFilters(query, NOW)).toEqual({
      from: '2026-10-01',
      to: '2026-10-31',
      userId: undefined,
      unitId: undefined,
      includeExcluded: undefined,
    });
  });
});
