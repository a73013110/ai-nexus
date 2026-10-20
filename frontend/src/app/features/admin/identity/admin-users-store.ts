import { Injectable, computed, inject, signal } from '@angular/core';
import { apiResource } from '@core/api/api-resource';
import { ViewScope } from '@shared/browser/view-scope';
import { AccessControlStore } from '@features/admin/access-control/access-control-store';
import { IdentityApi } from './identity-api';

/**
 * 使用者頁提供的使用者列表（搜尋與分頁）。角色與群組名稱來自同頁提供的 `AccessControlStore`，
 * 所以列表在目錄讀到後才讀，名稱不會先顯示成 ID。
 */
@Injectable()
export class AdminUsersStore {
  private readonly api = inject(IdentityApi);
  private readonly scope = inject(ViewScope);
  readonly access = inject(AccessControlStore);
  private readonly ready = computed(() => !!this.access.catalog());
  readonly search = signal('');
  private readonly query = signal('');
  private readonly typing = signal(false);
  readonly offset = signal(0);
  /** 伺服器固定每頁 100 筆（`ListAdminUsers`），分頁只能照它換算。 */
  readonly pageSize = 100;
  readonly usersRead = apiResource({
    feature: 'identity.users',
    params: () => (this.ready() ? { search: this.query(), offset: this.offset() } : undefined),
    loader: ({ search, offset }) => this.api.users(search, offset),
  });
  readonly users = computed(() => this.usersRead.value() ?? null);
  readonly loading = this.access.loading;
  readonly loadingUsers = computed(() => this.typing() || this.usersRead.refreshing());
  readonly actionError = this.access.actionError;
  readonly toast = this.access.toast;
  /** 使用者列表沒有資料時讀取失敗由表格狀態顯示（附重試），頁首只提示已有資料時的失敗。 */
  readonly error = computed(
    () => this.access.error() || (this.users()?.users.length ? this.usersRead.error() : ''),
  );

  load() {
    this.access.load();
    this.usersRead.reload();
  }
  find(value: string) {
    this.search.set(value);
    this.typing.set(true);
    this.scope.later(
      () => {
        this.typing.set(false);
        this.offset.set(0);
        this.query.set(value);
      },
      250,
      'admin-users-search',
    );
  }
  /** 在 `offset` 顯示使用者頁面；若已顯示，則重新讀取。 */
  readUsers(offset = this.offset()) {
    if (offset === this.offset()) this.usersRead.reload();
    else this.offset.set(offset);
  }
  /** 授權已改變：重新讀取目錄、顯示中的使用者與此帳號的授權。 */
  saved(title: string, description?: string) {
    this.access.saved(title, description);
    this.usersRead.reload();
  }
  roleNames(ids: string[]) {
    return this.access.roleNames(ids);
  }
}
