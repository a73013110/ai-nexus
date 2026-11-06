import { queryField } from '@shared/ui/query-fields';
import type { QueryStateOptions } from '@shared/ui/query-state';
import type { SelectOption } from '@shared/ui/select';
import type { UserFilters } from '../identity-api';

/** 使用者頁的查詢條件；`status` 是查詢列上方的切換，切換即套用。 */
export interface UsersQuery {
  status: UserStatus;
  search: string;
  roles: string[];
}

export type UserStatus = '' | 'enabled' | 'disabled';
export const USER_STATUSES: readonly (SelectOption & { value: UserStatus })[] = [
  { value: '', label: '全部' },
  { value: 'enabled', label: '啟用' },
  { value: 'disabled', label: '停用' },
];

/** 與後端 `AdminUserFilter` 的上限相同；伺服器仍會以 `invalid_search` 拒絕。 */
const SEARCH_MAX = 120;
const MAX_ROLES = 50;
const ROLE_ID = /^\S{1,64}$/;

export function userFilters(query: UsersQuery): UserFilters {
  return {
    search: query.search.trim() || undefined,
    enabled: query.status ? query.status === 'enabled' : undefined,
    role: query.roles.length ? query.roles : undefined,
  };
}

/** 角色 chip 的名稱由頁面提供（存取目錄）；目錄還沒讀到或角色已刪除時顯示角色 ID。 */
export function usersQueryOptions(
  roleName: (id: string) => string | undefined,
): QueryStateOptions<UsersQuery> {
  return {
    fields: {
      // 查詢列上方的切換本身就看得到目前的值，不另做 chip。
      status: queryField.choice('status', USER_STATUSES),
      search: queryField.text('search', { max: SEARCH_MAX, chip: '搜尋' }),
      roles: queryField.list('role', {
        chip: '角色',
        label: roleName,
        pattern: ROLE_ID,
        max: MAX_ROLES,
      }),
    },
  };
}
