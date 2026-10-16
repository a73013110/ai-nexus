import type { ParamMap, Params } from '@angular/router';
import type { AuditActionDto } from '@core/api/schema';
import type { QueryChip, QueryStateOptions } from '@shared/ui/query-state';
import {
  CUSTOM_RANGE,
  hoursAgo,
  timeRangeBounds,
  timeRangeLabel,
  type TimeRange,
  type TimeRangePreset,
} from '@shared/ui/time-range-picker';
import type { AuditFilters } from './audit-api';

/** The audit page's conditions; `category` is the view switch and applies at once. */
export interface AuditQuery {
  category: string;
  search: string;
  action: string;
  results: string[];
  range: TimeRange;
  traceId: string;
}

export const AUDIT_RANGES: TimeRangePreset[] = [
  { value: 'all', label: '不限時間' },
  { value: '24h', label: '最近 24 小時', duration: hoursAgo(24) },
  { value: '7d', label: '最近 7 天', duration: hoursAgo(24 * 7) },
  { value: '30d', label: '最近 30 天', duration: hoursAgo(24 * 30) },
];
/** `success` and `failed` are groups on the server; the others are stored results. */
export const AUDIT_RESULTS = [
  { value: 'success', label: '已受理／完成' },
  { value: 'failed', label: '未完成／拒絕' },
  { value: 'saved', label: '已儲存' },
  { value: 'read', label: '已檢視' },
];
const TRACE = /^[a-f\d]{32}$/i;
const DAY = /^\d{4}-\d{2}-\d{2}$/;

export const emptyAuditQuery = (): AuditQuery => ({
  category: '',
  search: '',
  action: '',
  results: [],
  range: { preset: 'all', from: '', to: '' },
  traceId: '',
});

/** The action filters of a category; every category's when none is chosen. */
export const actionsFor = (actions: readonly AuditActionDto[], category: string) =>
  category ? actions.filter((action) => action.categories.includes(category)) : actions;

export function auditFilters(query: AuditQuery, now = Date.now()): AuditFilters | string {
  const bounds = timeRangeBounds(query.range, AUDIT_RANGES, now);
  if (typeof bounds === 'string') return bounds;
  return {
    category: query.category || undefined,
    search: query.search.trim() || undefined,
    action: query.action || undefined,
    result: query.results.length ? query.results : undefined,
    traceId: query.traceId || undefined,
    from: bounds.from,
    until: bounds.to,
  };
}

function toParams(query: AuditQuery): Params {
  const custom = query.range.preset === CUSTOM_RANGE;
  return {
    category: query.category || undefined,
    search: query.search.trim() || undefined,
    action: query.action || undefined,
    result: query.results.length ? query.results : undefined,
    range: !custom && query.range.preset !== 'all' ? query.range.preset : undefined,
    from: custom ? query.range.from || undefined : undefined,
    until: custom ? query.range.to || undefined : undefined,
    traceId: query.traceId || undefined,
  };
}

/** Links from user details (`search`) and system logs (`traceId`) preset the query. */
function fromParams(params: ParamMap, defaults: AuditQuery): AuditQuery {
  const day = (key: string) => (DAY.test(params.get(key) ?? '') ? params.get(key)! : '');
  const from = day('from'),
    until = day('until');
  const preset = params.get('range') ?? '';
  const traceId = params.get('traceId') ?? '';
  return {
    category: /^[a-z]{1,40}$/.test(params.get('category') ?? '') ? params.get('category')! : '',
    search: (params.get('search') ?? '').slice(0, 120),
    action: /^[a-z_.]{1,120}$/.test(params.get('action') ?? '') ? params.get('action')! : '',
    results: AUDIT_RESULTS.map((item) => item.value).filter((value) =>
      params.getAll('result').includes(value),
    ),
    range:
      from || until
        ? { preset: CUSTOM_RANGE, from, to: until }
        : AUDIT_RANGES.some((item) => item.value === preset)
          ? { preset, from: '', to: '' }
          : defaults.range,
    traceId: TRACE.test(traceId) ? traceId.toLowerCase() : '',
  };
}

/** Options for the page's `QueryState`; chips name actions from the catalog as it loads. */
export function auditQueryOptions(
  actions: () => readonly AuditActionDto[],
): QueryStateOptions<AuditQuery> {
  return {
    defaults: emptyAuditQuery,
    toParams,
    fromParams,
    validate: (query) => {
      const filters = auditFilters(query);
      return typeof filters === 'string' ? filters : '';
    },
    chips: (query) => {
      const list: QueryChip<AuditQuery>[] = [];
      if (query.search.trim())
        list.push({ id: 'search', label: '搜尋：' + query.search.trim(), clear: { search: '' } });
      if (query.action)
        list.push({
          id: 'action',
          label:
            '動作：' +
            (actions().find((item) => item.value === query.action)?.label ?? query.action),
          clear: { action: '' },
        });
      if (query.results.length)
        list.push({
          id: 'results',
          label:
            '結果：' +
            AUDIT_RESULTS.filter((item) => query.results.includes(item.value))
              .map((item) => item.label)
              .join('、'),
          clear: { results: [] },
        });
      if (query.range.preset !== 'all')
        list.push({
          id: 'range',
          label: '時間：' + timeRangeLabel(query.range, AUDIT_RANGES),
          clear: { range: emptyAuditQuery().range },
        });
      if (query.traceId)
        list.push({ id: 'traceId', label: '同一流程：' + query.traceId, clear: { traceId: '' } });
      return list;
    },
  };
}
