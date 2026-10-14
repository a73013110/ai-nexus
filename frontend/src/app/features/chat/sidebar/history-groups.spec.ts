import { describe, expect, it } from 'vitest';
import type { ConversationDto } from '@core/api/schema';
import { historyGroups } from './history-groups';

const today = new Date(2026, 9, 11).getTime();
const row = (id: string, daysAgo: number, isFavorite = false) =>
  ({
    id,
    isFavorite,
    updatedAt: new Date(2026, 9, 11 - daysAgo, 23, 30).toISOString(),
  }) as ConversationDto;

describe('history groups', () => {
  it('buckets by local calendar day and keeps favourites first', () => {
    const groups = historyGroups(
      [
        row('pin', 90, true),
        row('a', 0),
        row('b', 1),
        row('c', 6),
        row('d', 7),
        row('e', 29),
        row('f', 30),
      ],
      today,
    );
    expect(groups.map((group) => [group.label, group.conversations.map((x) => x.id)])).toEqual([
      ['收藏', ['pin']],
      ['今天', ['a']],
      ['昨天', ['b']],
      ['過去 7 天', ['c']],
      ['過去 30 天', ['d', 'e']],
      ['更早', ['f']],
    ]);
  });
  it('treats a clock slightly ahead of the viewer as today', () => {
    expect(historyGroups([row('future', -1)], today)[0].label).toBe('今天');
  });
});
