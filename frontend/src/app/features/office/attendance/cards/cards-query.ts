import type { ParamMap, Params } from '@angular/router';
import type { QueryChip, QueryStateOptions } from '@shared/ui/query-state';
import { CARD_KINDS, cardKindLabel } from '../attendance-presentation';

/** 卡號對應頁的條件；`view` 是上方的集合切換（卡片／未歸戶卡號／不列入出勤），切換即套用。 */
export interface CardsQuery {
  view: CardsView;
  search: string;
  kind: string;
}

export type CardsView = 'cards' | 'unattributed' | 'exclusions';
const VIEWS: readonly CardsView[] = ['cards', 'unattributed', 'exclusions'];

/** 與後端 `ListCards` 的搜尋上限相同。 */
export const CARD_SEARCH_MAX = 100;

export const emptyCardsQuery = (): CardsQuery => ({ view: 'cards', search: '', kind: '' });

export const cardsQueryOptions: QueryStateOptions<CardsQuery> = {
  defaults: emptyCardsQuery,
  toParams: (query): Params => ({
    view: query.view !== 'cards' ? query.view : undefined,
    search: query.search.trim() || undefined,
    kind: query.kind || undefined,
  }),
  fromParams: (params: ParamMap, defaults) => {
    const kind = params.get('kind') ?? '';
    return {
      view: VIEWS.find((view) => view === params.get('view')) ?? defaults.view,
      search: (params.get('search') ?? '').slice(0, CARD_SEARCH_MAX),
      kind: CARD_KINDS.some((item) => item.value === kind) ? kind : '',
    };
  },
  chips: (query) => {
    const list: QueryChip<CardsQuery>[] = [];
    if (query.search.trim())
      list.push({ id: 'search', label: '搜尋：' + query.search.trim(), clear: { search: '' } });
    if (query.kind)
      list.push({ id: 'kind', label: '種類：' + cardKindLabel(query.kind), clear: { kind: '' } });
    return list;
  },
};
