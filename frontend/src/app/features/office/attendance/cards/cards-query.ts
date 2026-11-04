import { queryField } from '@shared/ui/query-fields';
import type { QueryStateOptions } from '@shared/ui/query-state';

/** 卡號對應頁的條件；`status` 是查詢列前方的狀態切換，切換即套用。 */
export interface CardsQuery {
  status: CardStatus;
  search: string;
}

/** 與後端 `CardStatuses` 相同；空字串為全部。 */
export type CardStatus = '' | 'held' | 'unheld' | 'hidden';
export const CARD_STATUSES: readonly { value: CardStatus; label: string }[] = [
  { value: '', label: '全部' },
  { value: 'held', label: '持有中' },
  { value: 'unheld', label: '無人持有' },
  { value: 'hidden', label: '隱藏' },
];

/** 與後端 `ListCards` 的搜尋上限相同。 */
export const CARD_SEARCH_MAX = 100;

export const cardsQueryOptions: QueryStateOptions<CardsQuery> = {
  fields: {
    // 狀態切換本身就看得到目前的值，不另做條件標籤。
    status: queryField.choice('status', CARD_STATUSES),
    search: queryField.text('search', { max: CARD_SEARCH_MAX, chip: '搜尋' }),
  },
};
