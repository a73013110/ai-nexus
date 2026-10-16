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

/** What an ID-shaped search matches: one GUID can be a job, a run or an operation. */
export type LogGuidField = 'runId' | 'jobId' | 'operationId';
/** The log page's conditions; IDs and message text share one search (`searchKind`). */
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
/** `NX-…` is an issue code, 32 hex a TraceId, a GUID a job, run or operation; anything else is message text. */
export function searchKind(search: string): LogSearchKind {
  const value = search.trim();
  if (!value) return '';
  if (ISSUE.test(value)) return 'issueCode';
  if (TRACE.test(value)) return 'traceId';
  if (GUID.test(value)) return 'guid';
  return 'text';
}

/** Why the conditions cannot be queried, or ''. */
export function validateLogQuery(query: LogQuery, now = Date.now()): string {
  const bounds = timeRangeBounds(query.range, LOG_RANGES, now);
  if (typeof bounds === 'string') return bounds;
  if (!bounds.from || !bounds.to) return '請選擇起訖時間。';
  const search = query.search.trim();
  if (/^NX-/i.test(search) && !ISSUE.test(search)) return '查證代碼是 NX- 加 32 碼英數字。';
  if (searchKind(search) === 'text') {
    if (search.length > 72) return '訊息文字最多 72 字。';
    if (Date.parse(bounds.to) - Date.parse(bounds.from) > DAY)
      return '訊息文字查詢限一天內，請縮小時間範圍，或改用查證代碼、TraceId 查詢。';
  }
  return '';
}

/** The request for the conditions, with the time bounds of this moment. */
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
    issueCode: kind === 'issueCode' ? search : '',
    traceId: kind === 'traceId' ? search.toLowerCase() : '',
    text: kind === 'text' ? search : '',
    ...(kind === 'guid' ? { [query.guid]: search.toLowerCase() } : {}),
  };
}

export function logSearchLabel(query: Pick<LogQuery, 'search' | 'guid'>): string {
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
    // Custom ends as instants, the same spelling links from activity audit use.
    from: bounds && typeof bounds !== 'string' ? bounds.from : undefined,
    to: bounds && typeof bounds !== 'string' ? bounds.to : undefined,
    level: query.levels.length ? query.levels : undefined,
    q: search || undefined,
    match: searchKind(search) === 'guid' && query.guid !== 'runId' ? query.guid : undefined,
    category: query.category.trim() || undefined,
    event: query.eventName.trim() || undefined,
    error: query.errorCode.trim() || undefined,
    instance: query.instance.trim() || undefined,
  };
}

/** Reads `q` and the older single-ID links (`traceId`, `issueCode`, `jobId`, `runId`). */
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
