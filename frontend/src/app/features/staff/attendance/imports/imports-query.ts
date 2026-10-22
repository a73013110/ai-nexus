import type { ParamMap, Params } from '@angular/router';
import type { QueryChip, QueryStateOptions } from '@shared/ui/query-state';
import {
  CUSTOM_RANGE,
  hoursAgo,
  timeRangeBounds,
  timeRangeLabel,
  type TimeRange,
  type TimeRangePreset,
} from '@shared/ui/time-range-picker';
import type { PunchImportFilters } from '../attendance-api';

/** 匯入紀錄的條件；`status` 是上方的結果切換，切換即套用。 */
export interface ImportsQuery {
  status: string;
  fileName: string;
  range: TimeRange;
}

export const IMPORT_STATUSES = [
  { value: '', label: '全部' },
  { value: 'succeeded', label: '成功' },
  { value: 'failed', label: '失敗' },
];
export const IMPORT_RANGES: TimeRangePreset[] = [
  { value: 'all', label: '不限時間' },
  { value: '7d', label: '最近 7 天', duration: hoursAgo(24 * 7) },
  { value: '30d', label: '最近 30 天', duration: hoursAgo(24 * 30) },
];
/** 一次最多重匯幾天（幾個檔）；與後端 `ReimportPunchFiles.MaxDays` 相同，送出前先擋，不等伺服器回 400。 */
export const MAX_REIMPORT_DAYS = 31;
/** 打卡機只輸出 `yyyyMMdd.txt`；其他檔名一律不匯入，所以篩選只收這個格式。 */
export const FILE_NAME = /^\d{8}\.txt$/;
const DAY = /^\d{4}-\d{2}-\d{2}$/;

export const emptyImportsQuery = (): ImportsQuery => ({
  status: '',
  fileName: '',
  range: { preset: 'all', from: '', to: '' },
});

/** 檔名欄位也接受 `yyyyMMdd` 或 `yyyy-MM-dd`，補成檔名。 */
export function normalizeFileName(value: string) {
  const text = value.trim();
  if (/^\d{8}$/.test(text)) return text + '.txt';
  if (DAY.test(text)) return text.replaceAll('-', '') + '.txt';
  return text;
}

export function importFilters(query: ImportsQuery, now = Date.now()): PunchImportFilters | string {
  const bounds = timeRangeBounds(query.range, IMPORT_RANGES, now);
  if (typeof bounds === 'string') return bounds;
  const fileName = normalizeFileName(query.fileName);
  if (fileName && !FILE_NAME.test(fileName)) return '檔名格式是 yyyyMMdd.txt，例如 20261001.txt。';
  return {
    status: query.status || undefined,
    fileName: fileName || undefined,
    from: bounds.from,
    until: bounds.to,
  };
}

function toParams(query: ImportsQuery): Params {
  const custom = query.range.preset === CUSTOM_RANGE;
  return {
    status: query.status || undefined,
    file: normalizeFileName(query.fileName) || undefined,
    range: !custom && query.range.preset !== 'all' ? query.range.preset : undefined,
    from: custom ? query.range.from || undefined : undefined,
    until: custom ? query.range.to || undefined : undefined,
  };
}

function fromParams(params: ParamMap, defaults: ImportsQuery): ImportsQuery {
  const day = (key: string) => (DAY.test(params.get(key) ?? '') ? params.get(key)! : '');
  const from = day('from'),
    until = day('until');
  const status = params.get('status') ?? '';
  const file = params.get('file') ?? '';
  const preset = params.get('range') ?? '';
  return {
    status: IMPORT_STATUSES.some((item) => item.value === status) ? status : '',
    fileName: FILE_NAME.test(file) ? file : '',
    range:
      from || until
        ? { preset: CUSTOM_RANGE, from, to: until }
        : IMPORT_RANGES.some((item) => item.value === preset)
          ? { preset, from: '', to: '' }
          : defaults.range,
  };
}

export const importsQueryOptions: QueryStateOptions<ImportsQuery> = {
  defaults: emptyImportsQuery,
  toParams,
  fromParams,
  validate: (query) => {
    const filters = importFilters(query);
    return typeof filters === 'string' ? filters : '';
  },
  chips: (query) => {
    const list: QueryChip<ImportsQuery>[] = [];
    const fileName = normalizeFileName(query.fileName);
    if (fileName) list.push({ id: 'file', label: '檔案：' + fileName, clear: { fileName: '' } });
    if (query.range.preset !== 'all')
      list.push({
        id: 'range',
        label: '時間：' + timeRangeLabel(query.range, IMPORT_RANGES),
        clear: { range: emptyImportsQuery().range },
      });
    return list;
  },
};
