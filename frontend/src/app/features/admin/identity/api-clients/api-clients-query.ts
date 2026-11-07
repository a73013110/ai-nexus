import type { ApiClientDto } from '@core/api/schema';
import { queryField } from '@shared/ui/query-fields';
import type { QueryStateOptions } from '@shared/ui/query-state';

/** 外部系統頁的條件；`status` 是查詢列前方的狀態切換，切換即套用。清單只有個位數，在前端篩選。 */
export interface ApiClientsQuery {
  status: ApiClientStatus;
  search: string;
}

export type ApiClientStatus = '' | 'enabled' | 'disabled';
export const API_CLIENT_STATUSES: readonly { value: ApiClientStatus; label: string }[] = [
  { value: '', label: '全部' },
  { value: 'enabled', label: '啟用' },
  { value: 'disabled', label: '停用' },
];

export const API_CLIENT_SEARCH_MAX = 100;

export const apiClientsQueryOptions: QueryStateOptions<ApiClientsQuery> = {
  fields: {
    // 狀態切換本身就看得到目前的值，不另做條件標籤。
    status: queryField.choice('status', API_CLIENT_STATUSES),
    search: queryField.text('search', { max: API_CLIENT_SEARCH_MAX, chip: '搜尋' }),
  },
};

/** 搜尋比對名稱、聯絡人與金鑰末四碼：對方回報末四碼時能反查是哪個系統。末四碼分大小寫，其餘不分。 */
export function matchesApiClient(client: ApiClientDto, query: ApiClientsQuery): boolean {
  if (query.status === 'enabled' && !client.enabled) return false;
  if (query.status === 'disabled' && client.enabled) return false;
  const search = query.search.trim();
  if (!search) return true;
  const text = search.toLocaleLowerCase();
  return (
    client.name.toLocaleLowerCase().includes(text) ||
    (client.contact ?? '').toLocaleLowerCase().includes(text) ||
    client.keyHint === search
  );
}
