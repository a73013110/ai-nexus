import type { ParamMap, Params } from '@angular/router';
import type { LogLevel } from '@core/api/schema';
import { formatDateTimeInput } from '@shared/browser/format';
import type { QueryChip, QueryStateOptions } from '@shared/ui/query-state';
import {
  CUSTOM_RANGE,
  hoursAgo,
  timeRangeBounds,
  timeRangeLabel,
  type TimeRange,
  type TimeRangePreset,
} from '@shared/ui/time-range-picker';
import { emptyLogFilter, type LogFilter } from './diagnostics-api';

/** GUID 格式的搜尋要比對哪個欄位：同一種格式可能是 Job、Run 或 Operation，由使用者指定。 */
export type LogGuidField = 'runId' | 'jobId' | 'operationId';
/**
 * 日誌頁的查詢條件。查證代碼、TraceId、GUID 與訊息文字共用一個搜尋欄，依格式判斷（`searchKind`）。
 */
export interface LogQuery {
  range: TimeRange;
  levels: LogLevel[];
  search: string;
  guid: LogGuidField;
  category: string;
  eventName: string;
  errorCode: string;
  instance: string;
}
export type LogMoreField = 'category' | 'eventName' | 'errorCode' | 'instance';

export const LOG_LEVELS: LogLevel[] = [
  'Trace',
  'Debug',
  'Information',
  'Warning',
  'Error',
  'Critical',
];
export const LOG_RANGES: TimeRangePreset[] = [
  { value: '15m', label: '最近 15 分鐘', duration: hoursAgo(0.25) },
  { value: '1h', label: '最近 1 小時', duration: hoursAgo(1) },
  { value: '24h', label: '最近 24 小時', duration: hoursAgo(24) },
  { value: '7d', label: '最近 7 天', duration: hoursAgo(24 * 7) },
];
export const LOG_GUID_FIELDS: { value: LogGuidField; label: string }[] = [
  { value: 'runId', label: 'RunId' },
  { value: 'jobId', label: 'JobId' },
  { value: 'operationId', label: 'OperationId' },
];
export const LOG_MORE_FIELDS: { key: LogMoreField; label: string; maxLength: number }[] = [
  { key: 'category', label: '模組 Category', maxLength: 180 },
  { key: 'eventName', label: '事件名稱', maxLength: 100 },
  { key: 'errorCode', label: '錯誤碼', maxLength: 80 },
  { key: 'instance', label: '執行個體', maxLength: 100 },
];
const DEFAULT_RANGE = '24h';
const DAY = hoursAgo(24);
/**
 * 與伺服器 `Diagnostics.MaxQueryDays` 的預設值相同；調整設定時伺服器仍會擋，這裡只是先給明確的訊息。
 */
export const MAX_QUERY_DAYS = 31;
const TRACE = /^[a-f\d]{32}$/i;
const ISSUE = /^NX-[a-f\d]{32}$/i;
const GUID = /^[a-f\d]{8}-[a-f\d]{4}-[a-f\d]{4}-[a-f\d]{4}-[a-f\d]{12}$/i;

export const emptyLogQuery = (): LogQuery => ({
  range: { preset: DEFAULT_RANGE, from: '', to: '' },
  levels: [],
  search: '',
  guid: 'runId',
  category: '',
  eventName: '',
  errorCode: '',
  instance: '',
});

export type LogSearchKind = '' | 'issueCode' | 'traceId' | 'guid' | 'text';
/** `NX-…` 是查證代碼、32 碼 hex 是 TraceId、GUID 是 Job／Run／Operation，其餘當訊息文字。 */
export function searchKind(search: string): LogSearchKind {
  const value = search.trim();
  if (!value) return '';
  if (ISSUE.test(value)) return 'issueCode';
  if (TRACE.test(value)) return 'traceId';
  if (GUID.test(value)) return 'guid';
  return 'text';
}

/**
 * 無法查詢的原因，可查詢時為空字串。
 * 訊息文字是子字串搜尋、走不到索引，伺服器限制 72 字且一天內；這裡先擋，避免送出必定失敗的查詢。
 */
export function validateLogQuery(query: LogQuery, now = Date.now()): string {
  const bounds = timeRangeBounds(query.range, LOG_RANGES, now);
  if (typeof bounds === 'string') return bounds;
  if (!bounds.from || !bounds.to) return '請選擇起訖時間。';
  if (Date.parse(bounds.to) - Date.parse(bounds.from) > MAX_QUERY_DAYS * DAY)
    return `查詢範圍最長 ${MAX_QUERY_DAYS} 天，請縮小時間範圍。`;
  const search = query.search.trim();
  if (/^NX-/i.test(search) && !ISSUE.test(search)) return '查證代碼是 NX- 加 32 碼英數字。';
  if (searchKind(search) === 'text') {
    if (search.length > 72) return '訊息文字最多 72 字。';
    if (Date.parse(bounds.to) - Date.parse(bounds.from) > DAY)
      return '訊息文字查詢限一天內，請縮小時間範圍，或改用查證代碼、TraceId 查詢。';
  }
  return '';
}

/** 轉成 API 參數；預設區間以呼叫當下計算起訖，所以每次查詢都是最新的「最近 N 小時」。 */
export function logFilter(query: LogQuery, now = Date.now()): LogFilter | string {
  const error = validateLogQuery(query, now);
  if (error) return error;
  const bounds = timeRangeBounds(query.range, LOG_RANGES, now) as { from: string; to: string };
  const search = query.search.trim();
  const kind = searchKind(search);
  return {
    ...emptyLogFilter(bounds.from, bounds.to),
    levels: query.levels,
    category: query.category.trim(),
    eventName: query.eventName.trim(),
    errorCode: query.errorCode.trim(),
    instance: query.instance.trim(),
    // 伺服器只認大寫的 `NX-`；手貼或舊書籤可能是小寫。
    issueCode: kind === 'issueCode' ? search.toUpperCase() : '',
    traceId: kind === 'traceId' ? search.toLowerCase() : '',
    text: kind === 'text' ? search : '',
    ...(kind === 'guid' ? { [query.guid]: search.toLowerCase() } : {}),
  };
}

function logSearchLabel(query: Pick<LogQuery, 'search' | 'guid'>): string {
  const search = query.search.trim();
  switch (searchKind(search)) {
    case 'issueCode':
      return '查證代碼：' + search;
    case 'traceId':
      return 'TraceId：' + search;
    case 'guid':
      return (
        (LOG_GUID_FIELDS.find((field) => field.value === query.guid)?.label ?? 'ID') + '：' + search
      );
    default:
      return '訊息：' + search;
  }
}

export const logMoreCount = (query: LogQuery) =>
  LOG_MORE_FIELDS.filter((field) => query[field.key].trim()).length;

function chips(query: LogQuery): QueryChip<LogQuery>[] {
  const defaults = emptyLogQuery();
  const list: QueryChip<LogQuery>[] = [];
  if (query.range.preset !== DEFAULT_RANGE)
    list.push({
      id: 'range',
      label: '時間：' + timeRangeLabel(query.range, LOG_RANGES),
      clear: { range: defaults.range },
    });
  if (query.levels.length)
    list.push({ id: 'levels', label: '等級：' + query.levels.join('、'), clear: { levels: [] } });
  if (query.search.trim())
    list.push({ id: 'search', label: logSearchLabel(query), clear: { search: '' } });
  for (const field of LOG_MORE_FIELDS)
    if (query[field.key].trim())
      list.push({
        id: field.key,
        label: field.label + '：' + query[field.key].trim(),
        clear: { [field.key]: '' },
      });
  return list;
}

function toParams(query: LogQuery): Params {
  const bounds =
    query.range.preset === CUSTOM_RANGE ? timeRangeBounds(query.range, LOG_RANGES) : null;
  const search = query.search.trim();
  return {
    range:
      query.range.preset !== DEFAULT_RANGE && query.range.preset !== CUSTOM_RANGE
        ? query.range.preset
        : undefined,
    // 自訂起訖寫成 ISO 時間點，與活動稽核「查證相關日誌」連結的格式相同，兩邊的連結可以共用。
    from: bounds && typeof bounds !== 'string' ? bounds.from : undefined,
    to: bounds && typeof bounds !== 'string' ? bounds.to : undefined,
    level: query.levels.length ? query.levels : undefined,
    q: search || undefined,
    // 預設比對 RunId，只有改成 Job／Operation 時才寫 match，網址保持簡短。
    match: searchKind(search) === 'guid' && query.guid !== 'runId' ? query.guid : undefined,
    category: query.category.trim() || undefined,
    event: query.eventName.trim() || undefined,
    error: query.errorCode.trim() || undefined,
    instance: query.instance.trim() || undefined,
  };
}

/**
 * 讀取 `q`，也相容舊版連結的單一 ID 參數（`traceId`、`issueCode`、`jobId`、`runId`、`operationId`），
 * 稽核頁的「查證相關日誌」與已存的書籤仍會帶這些參數。
 */
function fromParams(params: ParamMap, defaults: LogQuery): LogQuery {
  const text = (key: string, max: number) => (params.get(key) ?? '').trim().slice(0, max);
  const legacy = (['issueCode', 'traceId', 'jobId', 'runId', 'operationId'] as const).find((key) =>
    (key === 'issueCode' ? ISSUE : key === 'traceId' ? TRACE : GUID).test(params.get(key) ?? ''),
  );
  const match = params.get('match');
  const guid: LogGuidField =
    legacy === 'jobId' || legacy === 'operationId'
      ? legacy
      : LOG_GUID_FIELDS.some((field) => field.value === match)
        ? (match as LogGuidField)
        : defaults.guid;
  const from = Date.parse(params.get('from') ?? ''),
    to = Date.parse(params.get('to') ?? '');
  const preset = params.get('range') ?? '';
  const range: TimeRange =
    Number.isFinite(from) && Number.isFinite(to) && from < to
      ? {
          preset: CUSTOM_RANGE,
          from: formatDateTimeInput(new Date(from)),
          to: formatDateTimeInput(new Date(to)),
        }
      : LOG_RANGES.some((item) => item.value === preset)
        ? { preset, from: '', to: '' }
        : defaults.range;
  return {
    range,
    levels: LOG_LEVELS.filter((level) => params.getAll('level').includes(level)),
    search: legacy ? text(legacy, 40) : text('q', 72),
    guid,
    category: text('category', 180),
    eventName: text('event', 100),
    errorCode: text('error', 80),
    instance: text('instance', 100),
  };
}

export const logQueryOptions: QueryStateOptions<LogQuery> = {
  defaults: emptyLogQuery,
  toParams,
  fromParams,
  chips,
  validate: (query) => validateLogQuery(query),
};
