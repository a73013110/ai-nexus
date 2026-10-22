import type { ParamMap, Params } from '@angular/router';
import type { QueryChip, QueryStateOptions } from '@shared/ui/query-state';
import { CUSTOM_RANGE, type TimeRange, type TimeRangePreset } from '@shared/ui/time-range-picker';
import type { AttendanceRecordFilters } from '../attendance-api';
import { monthBounds, shiftMonth, taipeiToday } from '../attendance-presentation';

/** 出勤紀錄的查詢條件；區間預設本月，套用當下才換算成起訖（跨月後重新查詢就是新的月份）。 */
export interface RecordsQuery {
  range: TimeRange;
  userId: string;
  unitId: string;
  includeExcluded: boolean;
}

/** 與後端 `ListAttendanceRecords.MaxRangeDays` 相同；伺服器仍會以 `attendance_records_range_invalid` 拒絕。 */
export const MAX_RANGE_DAYS = 31;
export const RECORD_RANGES: TimeRangePreset[] = [
  { value: 'month', label: '本月' },
  { value: 'last-month', label: '上個月' },
  { value: '7d', label: '最近 7 天' },
];
const DEFAULT_RANGE = 'month';
const DAY = /^\d{4}-\d{2}-\d{2}$/;
const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export const emptyRecordsQuery = (): RecordsQuery => ({
  range: { preset: DEFAULT_RANGE, from: '', to: '' },
  userId: '',
  unitId: '',
  includeExcluded: false,
});

const dayNumber = (date: string) => Date.parse(date + 'T00:00:00Z') / 86_400_000;

/** 區間的起訖（出勤日，都含）；預設區間以台北今天換算。 */
export function recordsRange(
  range: TimeRange,
  now = Date.now(),
): { from: string; to: string } | string {
  const today = taipeiToday(now);
  switch (range.preset) {
    case 'month':
      return monthBounds(today.slice(0, 7));
    case 'last-month':
      return monthBounds(shiftMonth(today.slice(0, 7), -1));
    case '7d': {
      const start = new Date((dayNumber(today) - 6) * 86_400_000).toISOString().slice(0, 10);
      return { from: start, to: today };
    }
    case CUSTOM_RANGE: {
      if (!DAY.test(range.from) || !DAY.test(range.to)) return '請填寫起訖日期。';
      const days = dayNumber(range.to) - dayNumber(range.from) + 1;
      if (!Number.isFinite(days) || days < 1) return '結束日期不能早於開始日期。';
      if (days > MAX_RANGE_DAYS) return `區間最長 ${MAX_RANGE_DAYS} 天，一次查一個月。`;
      return { from: range.from, to: range.to };
    }
    default:
      return '請選擇區間。';
  }
}

export function recordFilters(
  query: RecordsQuery,
  now = Date.now(),
): AttendanceRecordFilters | string {
  const range = recordsRange(query.range, now);
  if (typeof range === 'string') return range;
  return {
    ...range,
    userId: query.userId || undefined,
    unitId: query.unitId || undefined,
    includeExcluded: query.includeExcluded || undefined,
  };
}

function toParams(query: RecordsQuery): Params {
  const custom = query.range.preset === CUSTOM_RANGE;
  return {
    range: !custom && query.range.preset !== DEFAULT_RANGE ? query.range.preset : undefined,
    from: custom ? query.range.from || undefined : undefined,
    to: custom ? query.range.to || undefined : undefined,
    user: query.userId || undefined,
    unit: query.unitId || undefined,
    excluded: query.includeExcluded ? 'true' : undefined,
  };
}

/** 連結帶入的條件；格式不對的參數沿用預設值，自訂區間超過上限時由 `validate` 擋下並說明。 */
function fromParams(params: ParamMap, defaults: RecordsQuery): RecordsQuery {
  const day = (key: string) => (DAY.test(params.get(key) ?? '') ? params.get(key)! : '');
  const id = (key: string) =>
    GUID.test(params.get(key) ?? '') ? params.get(key)!.toLowerCase() : '';
  const from = day('from'),
    to = day('to');
  const preset = params.get('range') ?? '';
  return {
    range:
      from || to
        ? { preset: CUSTOM_RANGE, from, to }
        : RECORD_RANGES.some((item) => item.value === preset)
          ? { preset, from: '', to: '' }
          : defaults.range,
    userId: id('user'),
    unitId: id('unit'),
    includeExcluded: params.get('excluded') === 'true',
  };
}

/** 區間的一行摘要：預設區間名稱加上實際起訖。 */
export function recordsRangeLabel(range: TimeRange, now = Date.now()) {
  const bounds = recordsRange(range, now);
  const span =
    typeof bounds === 'string'
      ? ''
      : `${bounds.from.replaceAll('-', '/')} — ${bounds.to.slice(5).replace('-', '/')}`;
  const preset = RECORD_RANGES.find((item) => item.value === range.preset)?.label;
  return preset ? `${preset}（${span}）` : span;
}

/** 人員與單位 chip 的名稱由頁面提供（讀到的名單、單位清單）；還不知道名稱時顯示「指定的人員」。 */
export function recordsQueryOptions(names: {
  person: (id: string) => string | undefined;
  unit: (id: string) => string | undefined;
}): QueryStateOptions<RecordsQuery> {
  return {
    defaults: emptyRecordsQuery,
    toParams,
    fromParams,
    validate: (query) => {
      const range = recordsRange(query.range);
      return typeof range === 'string' ? range : '';
    },
    chips: (query) => {
      const list: QueryChip<RecordsQuery>[] = [];
      if (query.range.preset !== DEFAULT_RANGE)
        list.push({
          id: 'range',
          label: '區間：' + recordsRangeLabel(query.range),
          clear: { range: emptyRecordsQuery().range },
        });
      if (query.userId)
        list.push({
          id: 'user',
          label: '人員：' + (names.person(query.userId) ?? '指定的人員'),
          clear: { userId: '' },
        });
      if (query.unitId)
        list.push({
          id: 'unit',
          label: '單位：' + (names.unit(query.unitId) ?? '指定的單位（含下層）'),
          clear: { unitId: '' },
        });
      if (query.includeExcluded)
        list.push({ id: 'excluded', label: '含不列入出勤的人', clear: { includeExcluded: false } });
      return list;
    },
  };
}
