import type { ParamMap, Params } from '@angular/router';
import type { QueryChip, QueryStateOptions } from '@shared/ui/query-state';
import { CUSTOM_RANGE, type TimeRange, type TimeRangePreset } from '@shared/ui/time-range-picker';
import type { AttendanceRecordFilters } from '../attendance-api';
import { monthBounds, shiftMonth, taipeiToday } from '../attendance-presentation';

/**
 * 出勤紀錄的查詢條件；區間預設本月，套用當下才換算成起訖（跨月後重新查詢就是新的月份）。
 * `person` 是使用者 ID，或一張卡上只有姓名的持有人（`card:卡號`，見 `cardKey`）。
 * `coverage` 是查詢列前方的切換（全部／未補足／流程中），切換即套用。
 */
export interface RecordsQuery {
  range: TimeRange;
  person: string;
  unitId: string;
  includeDaysOff: boolean;
  coverage: RecordsCoverage;
}

/** 只看遲到、早退或異常是這個狀態的日子；與後端 `AttendanceRecordsFilter.Coverage` 接受的值相同，空字串為全部。 */
export type RecordsCoverage = '' | 'Unexplained' | 'Pending';
export const RECORD_COVERAGES: readonly { value: RecordsCoverage; label: string; param: string }[] =
  [
    { value: '', label: '全部', param: '' },
    { value: 'Unexplained', label: '未補足', param: 'unexplained' },
    { value: 'Pending', label: '流程中', param: 'pending' },
  ];

/** 與後端 `AttendanceRecordsFilter.MaxRangeDays` 相同；伺服器仍會以 `attendance_records_range_invalid` 拒絕。 */
export const MAX_RANGE_DAYS = 366;
/** 與後端 `Card.CardNumberMaxLength` 相同。 */
const CARD_MAX = 32;
export const RECORD_RANGES: TimeRangePreset[] = [
  { value: 'month', label: '本月' },
  { value: 'last-month', label: '上個月' },
  { value: '7d', label: '最近 7 天' },
  { value: 'quarter', label: '本季' },
  { value: 'last-quarter', label: '上一季' },
  { value: 'year', label: '今年' },
  { value: 'last-year', label: '去年' },
];
const DEFAULT_RANGE = 'month';
const DAY = /^\d{4}-\d{2}-\d{2}$/;
const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const CARD = 'card:';

/** 沒有帳號的資料依附在卡號：人員選單與條件裡的值是 `card:卡號`，與使用者 ID（GUID）不會撞在一起。 */
export const cardKey = (cardNumber: string) => CARD + cardNumber;
/** `cardKey` 的卡號；不是卡號時為空字串。 */
export const cardOf = (person: string) =>
  person.startsWith(CARD) ? person.slice(CARD.length) : '';

export const emptyRecordsQuery = (): RecordsQuery => ({
  range: { preset: DEFAULT_RANGE, from: '', to: '' },
  person: '',
  unitId: '',
  includeDaysOff: false,
  coverage: '',
});

const dayNumber = (date: string) => Date.parse(date + 'T00:00:00Z') / 86_400_000;

/** `month` 起的連續 `count` 個月的第一天到最後一天。 */
function monthsBounds(month: string, count: number) {
  return { from: monthBounds(month).from, to: monthBounds(shiftMonth(month, count - 1)).to };
}

/** 區間的起訖（出勤日，都含）；預設區間以台北今天換算。 */
export function recordsRange(
  range: TimeRange,
  now = Date.now(),
): { from: string; to: string } | string {
  const today = taipeiToday(now);
  const month = today.slice(0, 7);
  const quarter = `${month.slice(0, 5)}${String(Math.floor((Number(month.slice(5)) - 1) / 3) * 3 + 1).padStart(2, '0')}`;
  const year = Number(today.slice(0, 4));
  switch (range.preset) {
    case 'month':
      return monthBounds(month);
    case 'last-month':
      return monthBounds(shiftMonth(month, -1));
    case '7d': {
      const start = new Date((dayNumber(today) - 6) * 86_400_000).toISOString().slice(0, 10);
      return { from: start, to: today };
    }
    case 'quarter':
      return monthsBounds(quarter, 3);
    case 'last-quarter':
      return monthsBounds(shiftMonth(quarter, -3), 3);
    case 'year':
      return { from: `${year}-01-01`, to: `${year}-12-31` };
    case 'last-year':
      return { from: `${year - 1}-01-01`, to: `${year - 1}-12-31` };
    case CUSTOM_RANGE: {
      if (!DAY.test(range.from) || !DAY.test(range.to)) return '請填寫起訖日期。';
      const days = dayNumber(range.to) - dayNumber(range.from) + 1;
      if (!Number.isFinite(days) || days < 1) return '結束日期不能早於開始日期。';
      if (days > MAX_RANGE_DAYS) return '區間最長一年；更長的期間請分次匯出。';
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
  const card = cardOf(query.person);
  return {
    ...range,
    userId: card ? undefined : query.person || undefined,
    card: card || undefined,
    unitId: query.unitId || undefined,
    includeDaysOff: query.includeDaysOff || undefined,
    coverage: query.coverage || undefined,
  };
}

function toParams(query: RecordsQuery): Params {
  const custom = query.range.preset === CUSTOM_RANGE;
  const card = cardOf(query.person);
  return {
    range: !custom && query.range.preset !== DEFAULT_RANGE ? query.range.preset : undefined,
    from: custom ? query.range.from || undefined : undefined,
    to: custom ? query.range.to || undefined : undefined,
    user: !card ? query.person || undefined : undefined,
    card: card || undefined,
    unit: query.unitId || undefined,
    holidays: query.includeDaysOff ? 'true' : undefined,
    coverage: RECORD_COVERAGES.find((item) => item.value === query.coverage)?.param || undefined,
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
  const card = (params.get('card') ?? '').trim();
  return {
    range:
      from || to
        ? { preset: CUSTOM_RANGE, from, to }
        : RECORD_RANGES.some((item) => item.value === preset)
          ? { preset, from: '', to: '' }
          : defaults.range,
    person: id('user') || (card && card.length <= CARD_MAX ? cardKey(card) : ''),
    unitId: id('unit'),
    includeDaysOff: params.get('holidays') === 'true',
    coverage:
      RECORD_COVERAGES.find((item) => item.param && item.param === params.get('coverage'))?.value ??
      '',
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

/**
 * 人員與單位 chip 的名稱由頁面提供（人員選單、單位清單）；還不知道名稱時顯示「指定的人員」，卡號則顯示卡號。
 */
export function recordsQueryOptions(names: {
  person: (id: string) => string | undefined;
  unit: (id: string) => string | undefined;
}): QueryStateOptions<RecordsQuery> {
  return {
    defaults: emptyRecordsQuery,
    toParams,
    fromParams,
    // 人員選單依單位重讀，換單位時原本選的人可能不在新單位。
    dependents: { unitId: ['person'] },
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
      if (query.unitId)
        list.push({
          id: 'unit',
          label: '單位：' + (names.unit(query.unitId) ?? '指定的單位（含下層）'),
          clear: { unitId: '' },
        });
      if (query.person)
        list.push({
          id: 'person',
          label:
            '人員：' +
            (names.person(query.person) ??
              (cardOf(query.person) ? `卡號 ${cardOf(query.person)}` : '指定的人員')),
          clear: { person: '' },
        });
      if (query.includeDaysOff)
        list.push({ id: 'holidays', label: '含放假日', clear: { includeDaysOff: false } });
      return list;
    },
  };
}
