import { isFeatureId } from '@core/api/catalog';
import type { DashboardSectionDto } from '@core/api/schema';
import type { WorkspaceSession } from '@core/auth/workspace-session';
import { localDateKey } from '@shared/browser/calendar-day';
import type { BadgeTone } from '@shared/ui/count-badge';

/** 後端用於指標、比例與項目的色調，同時作為徽章色調與顏色。 */
export function tone(value: string): BadgeTone {
  return (
    (['info', 'success', 'warning', 'danger'] as const).find((item) => item === value) ?? 'neutral'
  );
}
/** 以顏色表示的色調：資訊用強調色，狀態色只用於真正的狀態。 */
export const TONE_COLORS: Record<BadgeTone, string> = {
  neutral: 'var(--muted)',
  info: 'var(--accent)',
  success: 'var(--success)',
  warning: 'var(--warning)',
  danger: 'var(--danger)',
};

/** 從 `from` 到 `through`（含）的本地日期，格式為 `YYYY-MM-DD`。 */
export function dayRange(from: string, through: string): string[] {
  const days: string[] = [];
  const day = new Date(from + 'T00:00:00'),
    end = new Date(through + 'T00:00:00');
  for (let index = 0; day <= end && index < 367; index++, day.setDate(day.getDate() + 1))
    days.push(localDateKey(day));
  return days;
}
/** 沒有資料的日子以零填滿的每日數列。 */
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
/** 區段的圖示；未知的區段（例如新功能）使用中性的圖表。 */
export function sectionIcon(id: string) {
  return SECTION_ICONS[id] ?? 'chart';
}

/** 把後端的工作區路徑拆成路由片段；其他的都不是連結。 */
export function workspaceLink(link: string | null | undefined) {
  if (!link || !link.startsWith('/') || link.startsWith('//')) return null;
  const [path, search = ''] = link.split('?', 2);
  return { path, query: Object.fromEntries(new URLSearchParams(search)) };
}

/** 區段的入口；只連到前端目錄認得、且目前帳號持有的功能，其他區段不是連結。 */
export function sectionLink(
  section: Pick<DashboardSectionDto, 'feature' | 'route'>,
  session: WorkspaceSession,
) {
  return isFeatureId(section.feature) && session.has(section.feature)
    ? workspaceLink(section.route)
    : null;
}
