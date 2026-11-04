import { queryField } from '@shared/ui/query-fields';
import type { QueryStateOptions } from '@shared/ui/query-state';
import {
  hoursAgo,
  timeRangeBounds,
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

export const importsQueryOptions: QueryStateOptions<ImportsQuery> = {
  fields: {
    status: queryField.choice('status', IMPORT_STATUSES),
    fileName: queryField.text('file', {
      normalize: normalizeFileName,
      pattern: FILE_NAME,
      chip: '檔案',
    }),
    range: queryField.range(IMPORT_RANGES, { initial: 'all', to: 'until', chip: '時間' }),
  },
  validate: (query) => {
    const filters = importFilters(query);
    return typeof filters === 'string' ? filters : '';
  },
};
