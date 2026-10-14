import type { DashboardMetricDto } from '../../core/api/schema';
import { metricText } from '../../shared/browser/format';

export {
  compactNumber,
  figureMoney,
  metricFormatter,
  metricText,
} from '../../shared/browser/format';

/** Tones the backend uses for metrics, shares and items, as badge tones and colors. */
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

/** A metric read aloud or shown as one string: value and unit. */
export function metricLabel(metric: Pick<DashboardMetricDto, 'value' | 'format' | 'unit'>) {
  const text = metricText(metric.value, metric.format);
  return metric.unit && metric.value !== null ? `${text} ${metric.unit}` : text;
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
