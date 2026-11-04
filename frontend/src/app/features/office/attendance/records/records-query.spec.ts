import { convertToParamMap } from '@angular/router';
import { describe, expect, it } from 'vitest';
import { queryChips, queryDefaults, readQuery, writeQuery } from '@shared/ui/query-state';
import { CUSTOM_RANGE } from '@shared/ui/time-range-picker';
import {
  cardKey,
  recordFilters,
  recordsQueryOptions,
  recordsRange,
  recordsRangeLabel,
} from './records-query';

// 2026-10-31 20:00 台北＝當天 UTC 12:00；瀏覽器在別的時區也以台北日期換算。
const NOW = Date.parse('2026-10-31T12:00:00Z');
const options = recordsQueryOptions({ person: () => '王小明', unit: () => undefined });
const empty = () => queryDefaults(options);
const preset = (value: string) => ({ preset: value, from: '', to: '' });

describe('records query', () => {
  it('turns presets into Taipei dates when applied', () => {
    expect(recordsRange(preset('month'), NOW)).toEqual({ from: '2026-10-01', to: '2026-10-31' });
    expect(recordsRange(preset('last-month'), NOW)).toEqual({
      from: '2026-09-01',
      to: '2026-09-30',
    });
    expect(recordsRange(preset('7d'), NOW)).toEqual({ from: '2026-10-25', to: '2026-10-31' });
    expect(recordsRange(preset('quarter'), NOW)).toEqual({ from: '2026-10-01', to: '2026-12-31' });
    expect(recordsRange(preset('last-quarter'), NOW)).toEqual({
      from: '2026-07-01',
      to: '2026-09-30',
    });
    expect(recordsRange(preset('year'), NOW)).toEqual({ from: '2026-01-01', to: '2026-12-31' });
    expect(recordsRange(preset('last-year'), NOW)).toEqual({
      from: '2025-01-01',
      to: '2025-12-31',
    });
  });

  it('wraps the last quarter back into the previous year', () => {
    const february = Date.parse('2026-02-10T04:00:00Z');
    expect(recordsRange(preset('quarter'), february)).toEqual({
      from: '2026-01-01',
      to: '2026-03-31',
    });
    expect(recordsRange(preset('last-quarter'), february)).toEqual({
      from: '2025-10-01',
      to: '2025-12-31',
    });
  });

  it('refuses custom ranges over a year or backwards', () => {
    const custom = (from: string, to: string) => ({ preset: CUSTOM_RANGE, from, to });
    // 閏年也放得下整年：366 天是上限。
    expect(recordsRange(custom('2028-01-01', '2028-12-31'))).toEqual({
      from: '2028-01-01',
      to: '2028-12-31',
    });
    expect(recordsRange(custom('2026-01-01', '2027-01-02'))).toContain('最長一年');
    expect(recordsRange(custom('2026-10-02', '2026-10-01'))).toContain('不能早於');
    expect(recordsRange(custom('2026-10-02', ''))).toContain('起訖');
  });

  it('keeps applied conditions in the URL and reads them back', () => {
    const query = {
      ...empty(),
      range: { preset: CUSTOM_RANGE, from: '2026-09-15', to: '2026-10-14' },
      person: '3f2504e0-4f89-11d3-9a0c-0305e82c3301',
      includeDaysOff: true,
      coverage: 'Unexplained' as const,
    };
    const params = writeQuery(options, query);
    expect(params).toEqual({
      from: '2026-09-15',
      to: '2026-10-14',
      user: '3f2504e0-4f89-11d3-9a0c-0305e82c3301',
      holidays: 'true',
      coverage: 'unexplained',
    });
    expect(readQuery(options, convertToParamMap(params))).toEqual(query);
    // 缺口切換不是 chip（查詢列前方已看得到），但一樣送給伺服器並寫進網址。
    expect(recordFilters(query, NOW)).toMatchObject({ coverage: 'Unexplained' });
    expect(writeQuery(options, empty())).toEqual({});
    expect(queryChips(options, query).map((chip) => chip.label)).toEqual([
      '區間：2026/09/15 — 10/14',
      '人員：王小明',
      '含放假日',
    ]);
  });

  it('sends data without an account by its card number, never as a user', () => {
    const query = {
      ...empty(),
      range: preset('year'),
      person: cardKey('3368'),
    };
    expect(writeQuery(options, query)).toEqual({ range: 'year', card: '3368' });
    expect(readQuery(options, convertToParamMap({ card: ' 3368 ' }))).toMatchObject({
      person: cardKey('3368'),
    });
    expect(recordFilters(query, NOW)).toMatchObject({ userId: undefined, card: '3368' });
    // 人員選單還沒讀到時，chip 以卡號顯示。
    const unnamed = recordsQueryOptions({ person: () => undefined, unit: () => undefined });
    expect(queryChips(unnamed, query).map((chip) => chip.label)).toEqual([
      '區間：今年（' + recordsRangeLabel(preset('year')).split('（')[1],
      '人員：卡號 3368',
    ]);
  });

  it('ignores malformed link parameters', () => {
    const query = readQuery(
      options,
      convertToParamMap({
        range: 'decade',
        user: 'not-a-guid',
        card: '9'.repeat(33),
        unit: '1',
        holidays: '1',
        coverage: 'Covered',
      }),
    );
    expect(query).toEqual(empty());
    expect(recordFilters(query, NOW)).toEqual({
      from: '2026-10-01',
      to: '2026-10-31',
      userId: undefined,
      card: undefined,
      unitId: undefined,
      includeDaysOff: undefined,
    });
  });
});
