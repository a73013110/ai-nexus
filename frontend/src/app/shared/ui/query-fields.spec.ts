import { convertToParamMap } from '@angular/router';
import { describe, expect, it } from 'vitest';
import { queryField } from './query-fields';
import { queryChips, readQuery, writeQuery, type QueryStateOptions } from './query-state';
import { CUSTOM_RANGE, type TimeRange } from './time-range-picker';

interface Sample {
  status: '' | 'Open' | 'Closed';
  file: string;
  results: string[];
  archived: boolean;
  range: TimeRange;
}

const options: QueryStateOptions<Sample> = {
  fields: {
    status: queryField.choice('status', [
      { value: '', label: '全部' },
      { value: 'Open', label: '進行中', param: 'open' },
      { value: 'Closed', label: '已結束', param: 'closed' },
    ]),
    file: queryField.text('file', {
      max: 12,
      pattern: /^\d{8}\.txt$/,
      normalize: (value) => (/^\d{8}$/.test(value) ? value + '.txt' : value),
      chip: '檔案',
    }),
    results: queryField.choices(
      'result',
      [
        { value: 'ok', label: '完成' },
        { value: 'failed', label: '失敗' },
      ],
      { chip: '結果' },
    ),
    archived: queryField.flag('archived', '含封存'),
    range: queryField.range(
      [
        { value: 'all', label: '不限時間' },
        { value: '7d', label: '最近 7 天' },
      ],
      {
        initial: 'all',
        to: 'until',
        chip: '時間',
      },
    ),
  },
};
const read = (params: Record<string, string | string[]>) =>
  readQuery(options, convertToParamMap(params));

describe('queryField', () => {
  it('round-trips each kind of condition through the URL', () => {
    const query: Sample = {
      status: 'Closed',
      file: ' 20261001 ',
      results: ['failed', 'ok'],
      archived: true,
      range: { preset: CUSTOM_RANGE, from: '2026-10-01', to: '2026-10-02' },
    };
    const params = writeQuery(options, query);
    expect(params).toEqual({
      status: 'closed',
      file: '20261001.txt',
      result: ['ok', 'failed'],
      archived: 'true',
      from: '2026-10-01',
      until: '2026-10-02',
    });
    expect(read(params)).toEqual({ ...query, file: '20261001.txt', results: ['ok', 'failed'] });
    expect(queryChips(options, query).map((chip) => chip.label)).toEqual([
      '檔案：20261001.txt',
      '結果：完成、失敗',
      '含封存',
      '時間：2026/10/01 — 10/02',
    ]);
  });

  it('falls back to defaults for values it does not understand', () => {
    expect(
      read({ status: 'Closed', file: 'notes.txt', result: 'lost', archived: '1', range: '9y' }),
    ).toEqual({
      status: '',
      file: '',
      results: [],
      archived: false,
      range: { preset: 'all', from: '', to: '' },
    });
    expect(read({ range: '7d' }).range).toEqual({ preset: '7d', from: '', to: '' });
  });

  it('leaves conditions without a chip name to their own controls', () => {
    expect(queryChips(options, { ...read({}), status: 'Open' as const })).toEqual([]);
  });
});
