import type { ManagedRepositoryDto } from '@core/api/schema';
import { queryField } from '@shared/ui/query-fields';
import type { QueryStateOptions } from '@shared/ui/query-state';
import {
  REPOSITORY_STATES,
  type RepositoryState,
  matchesState,
} from './repository-access-presentation';

/**
 * 儲存庫權限頁的條件；`view` 是查詢列前方的檢視切換，切換即套用。清單一次讀完、在前端篩選：
 * 儲存庫是 Gitea 各組織的目錄，數量在千筆以內，換頁、排序與篩選都不必回後端。
 */
export interface RepositoryAccessQuery {
  view: RepositoryView;
  search: string;
  owner: string;
  state: RepositoryState;
}

export type RepositoryView = 'all' | 'mine' | 'stewarded';
export const REPOSITORY_VIEWS: readonly { value: RepositoryView; label: string }[] = [
  { value: 'all', label: '全部' },
  { value: 'mine', label: '我的權限' },
  { value: 'stewarded', label: '我負責的' },
];

export const REPOSITORY_SEARCH_MAX = 100;

export const repositoryAccessQueryOptions: QueryStateOptions<RepositoryAccessQuery> = {
  fields: {
    // 檢視切換本身就看得到目前的值，不另做條件標籤。
    view: queryField.choice('view', REPOSITORY_VIEWS),
    search: queryField.text('search', { max: REPOSITORY_SEARCH_MAX, chip: '搜尋' }),
    // 組織由清單讀出，連結帶入時選項可能還沒載入，所以只取文字。
    owner: queryField.text('owner', { max: REPOSITORY_SEARCH_MAX, chip: '組織' }),
    state: queryField.choice('state', REPOSITORY_STATES, { chip: '狀態' }),
  },
};

/** 搜尋比對「組織/名稱」與說明，不分大小寫；「我的權限」含平台授權、Gitea 直接加入與審核中的申請。 */
export function matchesRepository(item: ManagedRepositoryDto, query: RepositoryAccessQuery) {
  if (query.view === 'mine' && !item.myGrant && !item.myDirectPermission && !item.pendingInstanceId)
    return false;
  if (query.view === 'stewarded' && item.manager !== 'Steward') return false;
  if (query.owner && item.owner !== query.owner) return false;
  if (!matchesState(item, query.state)) return false;
  const needle = query.search.trim().toLocaleLowerCase();
  return (
    !needle ||
    `${item.owner}/${item.name} ${item.description ?? ''}`.toLocaleLowerCase().includes(needle)
  );
}
