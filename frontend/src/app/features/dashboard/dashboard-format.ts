import type { DashboardMetricDto } from '../../core/api/schema';
import { formatBytes, formatNumber } from '../../shared/browser/format';

/** Tones the backend uses for metrics, items and facts, as badge tones and colors. */
export type DashboardTone = 'neutral' | 'info' | 'success' | 'warning' | 'danger';
export function tone(value: string): DashboardTone {
  return (['info', 'success', 'warning', 'danger'] as const).find((x) => x === value) ?? 'neutral';
}
/** Tones as colors: the accent for information, status colors only for states that are real. */
export const TONE_COLORS: Record<DashboardTone, string> = {
  neutral: 'var(--muted)',
  info: 'var(--accent)',
  success: 'var(--success)',
  warning: 'var(--warning)',
  danger: 'var(--danger)',
};

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
/** A metric read aloud or shown as one string: value and unit. */
export function metricLabel(metric: Pick<DashboardMetricDto, 'value' | 'format' | 'unit'>) {
  const text = metricText(metric.value, metric.format);
  return metric.unit && metric.value !== null ? `${text} ${metric.unit}` : text;
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
/** Local days from `from` through `through`, inclusive, as `YYYY-MM-DD`. */
export function dayRange(from: string, through: string): string[] {
  const days: string[] = [];
  const day = new Date(from + 'T00:00:00'),
    end = new Date(through + 'T00:00:00');
  for (let i = 0; day <= end && i < 367; i++, day.setDate(day.getDate() + 1))
    days.push(
      `${day.getFullYear()}-${String(day.getMonth() + 1).padStart(2, '0')}-${String(day.getDate()).padStart(2, '0')}`,
    );
  return days;
}
/** A daily series filled with zeros for days without data. */
export function fillDays(
  days: readonly string[],
  points: readonly { date: string; value: number }[],
) {
  const values = new Map<string, number>();
  for (const point of points) values.set(point.date, (values.get(point.date) ?? 0) + point.value);
  return days.map((date) => ({ date, value: values.get(date) ?? 0 }));
}

const SECTION_ICONS: Record<string, string> = {
  chat: 'chat',
  'web-search': 'globe',
  knowledge: 'library',
  files: 'files',
  tasks: 'tasks',
  projects: 'projects',
  artifacts: 'document',
  shared: 'share',
  quality: 'thumb-up',
  repositories: 'git',
  notifications: 'bell',
};
/** Icon for a section; unknown sections (new features) get a neutral chart. */
export function sectionIcon(id: string) {
  return SECTION_ICONS[id] ?? 'chart';
}

/** Splits a workspace path from the backend into router parts; anything else is not a link. */
export function workspaceLink(link: string | null | undefined) {
  if (!link || !link.startsWith('/') || link.startsWith('//')) return null;
  const [path, search = ''] = link.split('?', 2);
  return { path, query: Object.fromEntries(new URLSearchParams(search)) };
}
