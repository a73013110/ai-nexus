import type { ModelDto } from '@core/api/schema';
import { formatCalendarValue, parseLocalDateTime, type CalendarSystem } from './date-time';

export const APP_TIME_ZONE = 'Asia/Taipei';
const workspaceDateTime = new Intl.DateTimeFormat('en-CA', {
  timeZone: APP_TIME_ZONE,
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
  hour: '2-digit',
  minute: '2-digit',
  second: '2-digit',
  hourCycle: 'h23',
});
export const formatDate = (value: string, calendar: CalendarSystem = 'gregory') =>
  formatCalendarValue(formatDateTimeInput(new Date(value)), calendar);
/** 時間戳記在工作區時區的日期，不含時間。 */
export const formatDay = (value: string, calendar: CalendarSystem = 'gregory') =>
  formatCalendarValue(formatDateTimeInput(new Date(value)), calendar, false);
/** ISO 的牆上時間一律使用工作區時區，與瀏覽器的語系與時區無關。 */
export function formatDateTimeInput(date: Date) {
  if (!Number.isFinite(date.getTime())) return '';
  const parts = workspaceDateTime.formatToParts(date);
  const part = (type: Intl.DateTimeFormatPartTypes) =>
    parts.find((part2) => part2.type === type)!.value;
  return `${part('year').padStart(4, '0')}-${part('month')}-${part('day')}T${part('hour')}:${part('minute')}:${part('second')}`;
}
export const parseDateTimeInput = (value: string) =>
  parseLocalDateTime(value) && value.includes('T') ? new Date(value + '+08:00') : new Date(NaN);
export const formatNumber = (value: number) => value.toLocaleString('zh-TW');
const percentFormat = new Intl.NumberFormat('zh-TW', {
  style: 'percent',
  maximumFractionDigits: 1,
});
/** 比率（0.25）以最多一位小數的百分比顯示（25%）。 */
export const formatPercent = (ratio: number) => percentFormat.format(ratio);
export const formatBytes = (value: number) =>
  value < 1000
    ? `${formatNumber(value)} bytes`
    : value < 1_000_000
      ? `${(value / 1000).toFixed(1)} KB`
      : value < 1_000_000_000
        ? `${(value / 1_000_000).toFixed(1)} MB`
        : value < 1_000_000_000_000
          ? `${(value / 1_000_000_000).toFixed(2)} GB`
          : `${(value / 1_000_000_000_000).toFixed(2)} TB`;
const DURATION_UNITS = [
  [86_400_000, '天'],
  [3_600_000, '小時'],
  [60_000, '分'],
  [1000, '秒'],
] as const;
/**
 * "4.8 秒", "11 分 4 秒", "11 小時 4 分", "2 天 3 小時": the two largest units, floored, so a long
 * total never reads as hundreds of minutes; a zero second unit is left out ("2 小時").
 */
export function formatDuration(milliseconds: number) {
  if (milliseconds < 60_000) return `${(milliseconds / 1000).toFixed(1)} 秒`;
  const index = DURATION_UNITS.findIndex(([size]) => milliseconds >= size);
  const [size, unit] = DURATION_UNITS[index];
  const [next, nextUnit] = DURATION_UNITS[index + 1];
  const major = Math.floor(milliseconds / size);
  const minor = Math.floor((milliseconds % size) / next);
  return minor ? `${major} ${unit} ${minor} ${nextUnit}` : `${major} ${unit}`;
}
/** 模型沒有顯示名稱（或只有空白）時一律稱為「AI 助理」。 */
const modelName = (name?: string | null) => name?.trim() || 'AI 助理';
export const formatModelName = (model: Pick<ModelDto, 'displayName'>) =>
  modelName(model.displayName);
/** 帶有 `modelDisplayName` 的紀錄（用量、價目、回饋）。 */
export const formatModelDisplayName = (model: { modelDisplayName?: string | null }) =>
  modelName(model.modelDisplayName);
const relative = new Intl.RelativeTimeFormat('zh-TW', { numeric: 'auto' });
/** "3 分鐘前", "昨天"; older than a month falls back to the date. */
export function relativeTime(iso: string, now = Date.now()) {
  const seconds = Math.round((new Date(iso).getTime() - now) / 1000);
  const abs = Math.abs(seconds);
  if (abs < 45) return '剛剛';
  if (abs < 3600) return relative.format(Math.round(seconds / 60), 'minute');
  if (abs < 86400) return relative.format(Math.round(seconds / 3600), 'hour');
  if (abs < 86400 * 30) return relative.format(Math.round(seconds / 86400), 'day');
  return iso.slice(0, 10);
}
/** 指標的數字部分，不含單位；未知的值絕不會被讀成零。 */
export function metricText(value: number | null | undefined, format: string): string {
  if (value === null || value === undefined || !Number.isFinite(value)) return '—';
  switch (format) {
    case 'bytes':
      return formatBytes(value);
    case 'percent':
      return `${value >= 10 ? Math.round(value) : Math.round(value * 10) / 10}%`;
    case 'duration':
      return value < 1000 ? `${Math.round(value)} ms` : formatDuration(value);
    default:
      return formatNumber(Math.round(value * 100) / 100);
  }
}
export function metricFormatter(format: string): (value: number) => string {
  return (value) => metricText(value, format);
}
const compactFormat = new Intl.NumberFormat('zh-TW', {
  notation: 'compact',
  maximumFractionDigits: 1,
});
/** 焦點數字的精簡格式：12.3K、4.5M。 */
export const compactNumber = (value: number) => compactFormat.format(value);
/**
 * 以標題數字呈現的金額，能放進任何磚塊：一般金額保留到分，不足一單位的金額取到三位小數，
 * significant digits, and from ten thousand on the compact form (`USD 1.23萬`). Exact amounts stay
 * 表格、提示與無障礙名稱中都使用 `money()`。
 */
export function figureMoney(amount: number, currency: string) {
  if (!currency) return compactNumber(amount);
  const abs = Math.abs(amount);
  const options: Intl.NumberFormatOptions =
    abs >= 10_000
      ? { notation: 'compact', maximumSignificantDigits: 3 }
      : abs > 0 && abs < 1
        ? { maximumSignificantDigits: 3 }
        : { minimumFractionDigits: 2, maximumFractionDigits: 2 };
  return new Intl.NumberFormat('zh-TW', {
    style: 'currency',
    currency,
    currencyDisplay: 'code',
    ...options,
  }).format(amount);
}
