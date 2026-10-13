import type { ModelDto } from '../../core/api/schema';
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
/** ISO wall-clock values use the workspace zone, independently of browser locale and zone. */
export function formatDateTimeInput(date: Date) {
  if (!Number.isFinite(date.getTime())) return '';
  const parts = workspaceDateTime.formatToParts(date);
  const part = (type: Intl.DateTimeFormatPartTypes) => parts.find((p) => p.type === type)!.value;
  return `${part('year').padStart(4, '0')}-${part('month')}-${part('day')}T${part('hour')}:${part('minute')}:${part('second')}`;
}
export const parseDateTimeInput = (value: string) =>
  parseLocalDateTime(value) && value.includes('T') ? new Date(value + '+08:00') : new Date(NaN);
export const formatNumber = (value: number) => value.toLocaleString('zh-TW');
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
export const formatDuration = (milliseconds: number) =>
  milliseconds < 60_000
    ? `${(milliseconds / 1000).toFixed(1)} 秒`
    : `${Math.floor(milliseconds / 60_000)} 分 ${Math.floor((milliseconds % 60_000) / 1000)} 秒`;
export const formatModelName = (model: Pick<ModelDto, 'displayName'>) =>
  model.displayName?.trim() || 'AI 助理';
export const formatModelDisplayName = (model: { modelDisplayName?: string | null }) =>
  model.modelDisplayName?.trim() || 'AI 助理';
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
/** The number part of a metric, without its unit; unknown values never read as zero. */
export function metricText(value: number | null | undefined, format: string): string {
  if (value === null || value === undefined || !Number.isFinite(value)) return '—';
  switch (format) {
    case 'bytes':
      return formatBytes(value);
    case 'percent':
      return `${value >= 10 ? Math.round(value) : Math.round(value * 10) / 10}%`;
    case 'duration':
      return value < 1000
        ? `${Math.round(value)} ms`
        : value < 60_000
          ? `${(value / 1000).toFixed(1)} 秒`
          : `${Math.floor(value / 60_000)} 分 ${Math.round((value % 60_000) / 1000)} 秒`;
    default:
      return formatNumber(Math.round(value * 100) / 100);
  }
}
export function metricFormatter(format: string): (value: number) => string {
  return (value) => metricText(value, format);
}
/** Compact numbers for hero figures: 12.3K, 4.5M. */
export function compactNumber(value: number) {
  return new Intl.NumberFormat('zh-TW', { notation: 'compact', maximumFractionDigits: 1 }).format(
    value,
  );
}
/**
 * Money as a headline figure that fits any tile: cents as usual, sub-unit amounts to three
 * significant digits, and from ten thousand on the compact form (`USD 1.23萬`). Exact amounts stay
 * with `money()` in tables, tooltips and the accessible name.
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
