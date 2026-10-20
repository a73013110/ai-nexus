import type { ParamMap, Params } from '@angular/router';
import type { QueryChip, QueryStateOptions } from '@shared/ui/query-state';
import type { SelectOption } from '@shared/ui/select';
import {
  CUSTOM_RANGE,
  hoursAgo,
  timeRangeBounds,
  timeRangeLabel,
  type TimeRange,
  type TimeRangePreset,
} from '@shared/ui/time-range-picker';
import type { MailFilters } from '../mail-api';

/** 寄送紀錄的查詢條件；`status` 是上方的狀態切換，切換即套用。 */
export interface MailQuery {
  status: string;
  search: string;
  category: string;
  range: TimeRange;
}

export const MAIL_STATUSES: SelectOption[] = [
  { value: '', label: '全部' },
  { value: 'queued', label: '等待中' },
  { value: 'sending', label: '寄送中' },
  { value: 'sent', label: '已寄出' },
  { value: 'failed', label: '失敗' },
  { value: 'cancelled', label: '已取消' },
];

export const MAIL_RANGES: TimeRangePreset[] = [
  { value: 'all', label: '不限時間' },
  { value: '24h', label: '最近 24 小時', duration: hoursAgo(24) },
  { value: '7d', label: '最近 7 天', duration: hoursAgo(24 * 7) },
  { value: '30d', label: '最近 30 天', duration: hoursAgo(24 * 30) },
];

const DAY = /^\d{4}-\d{2}-\d{2}$/;
const CATEGORY = /^[a-z0-9.-]{1,64}$/;

export const emptyMailQuery = (): MailQuery => ({
  status: '',
  search: '',
  category: '',
  range: { preset: 'all', from: '', to: '' },
});

export function mailFilters(query: MailQuery, now = Date.now()): MailFilters | string {
  const bounds = timeRangeBounds(query.range, MAIL_RANGES, now);
  if (typeof bounds === 'string') return bounds;
  const category = query.category.trim();
  if (category && !CATEGORY.test(category)) return '分類只能包含小寫英文、數字、點與連字號。';
  return {
    status: query.status || undefined,
    search: query.search.trim() || undefined,
    category: category || undefined,
    from: bounds.from,
    until: bounds.to,
  };
}

function toParams(query: MailQuery): Params {
  const custom = query.range.preset === CUSTOM_RANGE;
  return {
    status: query.status || undefined,
    search: query.search.trim() || undefined,
    category: query.category.trim() || undefined,
    range: !custom && query.range.preset !== 'all' ? query.range.preset : undefined,
    from: custom ? query.range.from || undefined : undefined,
    until: custom ? query.range.to || undefined : undefined,
  };
}

/** 連結帶入的條件；格式不對的參數沿用預設值。日期只收 `YYYY-MM-DD`。 */
function fromParams(params: ParamMap, defaults: MailQuery): MailQuery {
  const day = (key: string) => (DAY.test(params.get(key) ?? '') ? params.get(key)! : '');
  const from = day('from'),
    until = day('until');
  const status = params.get('status') ?? '';
  const category = params.get('category') ?? '';
  const preset = params.get('range') ?? '';
  return {
    status: MAIL_STATUSES.some((item) => item.value === status) ? status : '',
    search: (params.get('search') ?? '').slice(0, 254),
    category: CATEGORY.test(category) ? category : '',
    range:
      from || until
        ? { preset: CUSTOM_RANGE, from, to: until }
        : MAIL_RANGES.some((item) => item.value === preset)
          ? { preset, from: '', to: '' }
          : defaults.range,
  };
}

export const mailQueryOptions: QueryStateOptions<MailQuery> = {
  defaults: emptyMailQuery,
  toParams,
  fromParams,
  validate: (query) => {
    const filters = mailFilters(query);
    return typeof filters === 'string' ? filters : '';
  },
  chips: (query) => {
    const list: QueryChip<MailQuery>[] = [];
    if (query.search.trim())
      list.push({ id: 'search', label: '搜尋：' + query.search.trim(), clear: { search: '' } });
    if (query.category.trim())
      list.push({
        id: 'category',
        label: '分類：' + query.category.trim(),
        clear: { category: '' },
      });
    if (query.range.preset !== 'all')
      list.push({
        id: 'range',
        label: '時間：' + timeRangeLabel(query.range, MAIL_RANGES),
        clear: { range: emptyMailQuery().range },
      });
    return list;
  },
};
