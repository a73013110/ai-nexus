import { queryField } from '@shared/ui/query-fields';
import type { QueryStateOptions } from '@shared/ui/query-state';
import type { SelectOption } from '@shared/ui/select';
import {
  hoursAgo,
  timeRangeBounds,
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

const CATEGORY = /^[a-z0-9.-]{1,64}$/;

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

export const mailQueryOptions: QueryStateOptions<MailQuery> = {
  fields: {
    status: queryField.choice('status', MAIL_STATUSES),
    search: queryField.text('search', { max: 254, chip: '搜尋' }),
    category: queryField.text('category', { pattern: CATEGORY, chip: '分類' }),
    range: queryField.range(MAIL_RANGES, { initial: 'all', to: 'until', chip: '時間' }),
  },
  validate: (query) => {
    const filters = mailFilters(query);
    return typeof filters === 'string' ? filters : '';
  },
};
