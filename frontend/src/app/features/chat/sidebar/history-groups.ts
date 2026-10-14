import type { ConversationDto } from '@core/api/schema';
import { calendarDaysBetween } from '@shared/browser/calendar-day';

/** Groups the already-sorted history by favourite, then by local calendar age relative to `today`. */
export function historyGroups(conversations: readonly ConversationDto[], today: number) {
  const groups = new Map<string, ConversationDto[]>();
  for (const conversation of conversations) {
    const age = calendarDaysBetween(new Date(conversation.updatedAt).getTime(), today);
    const label = conversation.isFavorite
      ? '收藏'
      : age <= 0
        ? '今天'
        : age === 1
          ? '昨天'
          : age < 7
            ? '過去 7 天'
            : age < 30
              ? '過去 30 天'
              : '更早';
    const rows = groups.get(label) ?? [];
    rows.push(conversation);
    groups.set(label, rows);
  }
  return [...groups].map(([label, conversations]) => ({ label, conversations }));
}
