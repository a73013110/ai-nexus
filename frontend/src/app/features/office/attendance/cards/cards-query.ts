import type { ParamMap, Params } from '@angular/router';
import type { QueryChip, QueryStateOptions } from '@shared/ui/query-state';

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

export const emptyCardsQuery = (): CardsQuery => ({ status: '', search: '' });

export const cardsQueryOptions: QueryStateOptions<CardsQuery> = {
  defaults: emptyCardsQuery,
  toParams: (query): Params => ({
    status: query.status || undefined,
    search: query.search.trim() || undefined,
  }),
  fromParams: (params: ParamMap, defaults) => ({
    status:
      CARD_STATUSES.find((item) => item.value === params.get('status'))?.value ?? defaults.status,
    search: (params.get('search') ?? '').slice(0, CARD_SEARCH_MAX),
  }),
  // 狀態切換本身就看得到目前的值，不另做條件標籤。
  chips: (query) => {
    const list: QueryChip<CardsQuery>[] = [];
    if (query.search.trim())
      list.push({ id: 'search', label: '搜尋：' + query.search.trim(), clear: { search: '' } });
    return list;
  },
};
