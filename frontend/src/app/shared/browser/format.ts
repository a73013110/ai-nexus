import type { Model } from '../../core/api/types';
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
export const formatModelName = (model: Pick<Model, 'displayName'>) =>
  model.displayName?.trim() || 'AI 助理';
export const formatModelDisplayName = (model: { modelDisplayName?: string | null }) =>
  model.modelDisplayName?.trim() || 'AI 助理';
