export type CalendarSystem = 'gregory' | 'roc';
export interface LocalDateTime {
  year: number;
  month: number;
  day: number;
  hour: number;
  minute: number;
  second: number;
}
const pad = (value: number, length = 2) => String(value).padStart(length, '0');

/** Calendar arithmetic uses UTC as a wall-clock container, never the browser's zone. */
export function calendarDate(parts: LocalDateTime) {
  const date = new Date(0);
  date.setUTCFullYear(parts.year, parts.month - 1, parts.day);
  date.setUTCHours(parts.hour, parts.minute, parts.second, 0);
  return date;
}
export function validDateTime(parts: LocalDateTime) {
  if (Object.values(parts).some((value) => !Number.isInteger(value))) return false;
  if (parts.year < 1 || parts.year > 9999) return false;
  const date = calendarDate(parts);
  return (
    date.getUTCFullYear() === parts.year &&
    date.getUTCMonth() + 1 === parts.month &&
    date.getUTCDate() === parts.day &&
    date.getUTCHours() === parts.hour &&
    date.getUTCMinutes() === parts.minute &&
    date.getUTCSeconds() === parts.second
  );
}
export function parseLocalDateTime(value: string): LocalDateTime | null {
  const match = /^(\d{4})-(\d{2})-(\d{2})(?:T(\d{2}):(\d{2})(?::(\d{2}))?)?$/.exec(value);
  if (!match) return null;
  const parts: LocalDateTime = {
    year: Number(match[1]),
    month: Number(match[2]),
    day: Number(match[3]),
    hour: Number(match[4] ?? 0),
    minute: Number(match[5] ?? 0),
    second: Number(match[6] ?? 0),
  };
  return validDateTime(parts) ? parts : null;
}
export function serializeLocalDateTime(parts: LocalDateTime, withTime = true) {
  return (
    pad(parts.year, 4) +
    '-' +
    pad(parts.month) +
    '-' +
    pad(parts.day) +
    (withTime ? 'T' + pad(parts.hour) + ':' + pad(parts.minute) + ':' + pad(parts.second) : '')
  );
}
export function formatCalendarValue(
  value: string,
  calendar: CalendarSystem = 'gregory',
  withTime = true,
) {
  const parts = parseLocalDateTime(value);
  if (!parts) return '';
  const year = calendar === 'roc' ? parts.year - 1911 : parts.year;
  return (
    (calendar === 'roc' ? '民國 ' : '') +
    pad(year, calendar === 'roc' ? 3 : 4) +
    '/' +
    pad(parts.month) +
    '/' +
    pad(parts.day) +
    (withTime ? ' ' + pad(parts.hour) + ':' + pad(parts.minute) + ':' + pad(parts.second) : '')
  );
}
export function parseCalendarText(
  value: string,
  calendar: CalendarSystem = 'gregory',
  withTime = true,
) {
  const match = /^(?:民國\s*)?(\d{1,4})\/(\d{2})\/(\d{2})(?: (\d{2}):(\d{2}):(\d{2}))?$/.exec(
    value.trim(),
  );
  if (!match || (withTime && !match[4]) || (!withTime && match[4])) return '';
  if (calendar === 'gregory' && match[1].length !== 4) return '';
  const year = Number(match[1]);
  if (calendar === 'roc' && year < 1) return '';
  const parts: LocalDateTime = {
    year: year + (calendar === 'roc' ? 1911 : 0),
    month: Number(match[2]),
    day: Number(match[3]),
    hour: Number(match[4] ?? 0),
    minute: Number(match[5] ?? 0),
    second: Number(match[6] ?? 0),
  };
  return validDateTime(parts) ? serializeLocalDateTime(parts, withTime) : '';
}
export function shiftCalendarMonth(parts: LocalDateTime, months: number): LocalDateTime {
  const date = calendarDate({ ...parts, day: 1 });
  date.setUTCMonth(date.getUTCMonth() + months);
  const last = new Date(date);
  last.setUTCMonth(last.getUTCMonth() + 1, 0);
  return {
    ...parts,
    year: date.getUTCFullYear(),
    month: date.getUTCMonth() + 1,
    day: Math.min(parts.day, last.getUTCDate()),
  };
}
