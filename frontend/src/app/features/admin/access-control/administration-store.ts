import { Injectable, computed, inject, signal } from '@angular/core';
import { apiResource } from '@core/api/api-resource';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { ViewScope } from '@shared/browser/view-scope';
import { AdministrationApi } from './administration-api';
import { ToastService } from '@shared/ui/toast-service';

/** 單一管理頁的各管理分頁與編輯器共用的目錄與使用者列表。 */
@Injectable()
export class AdministrationStore {
  private readonly api = inject(AdministrationApi);
  private readonly session = inject(WorkspaceSession);
  private readonly scope = inject(ViewScope);
  /** 帳號會重新讀取：此頁面會改變它可做的事。 */
  private readonly catalogRead = apiResource({
    feature: 'admin',
    freshAccount: true,
    loader: () => this.api.catalog(),
  });
  readonly catalog = computed(() => this.catalogRead.value() ?? null);
  /** 每個選單群組，包括空的：編輯器用來排列，摘要則依群組分類。 */
  readonly navigationGroups = computed(() => this.catalog()?.navigationGroups ?? []);
  private readonly ready = computed(() => !!this.catalog());
  readonly search = signal('');
  private readonly query = signal('');
  private readonly typing = signal(false);
  readonly offset = signal(0);
  readonly usersRead = apiResource({
    params: () => (this.ready() ? { search: this.query(), offset: this.offset() } : undefined),
    loader: ({ search, offset }) => this.api.users(search, offset),
  });
  readonly users = computed(() => this.usersRead.value() ?? null);
  readonly loading = this.catalogRead.loading;
  readonly loadingUsers = computed(() => this.typing() || this.usersRead.refreshing());
  readonly actionError = signal('');
  /** 使用者列表沒有資料時讀取失敗由表格狀態顯示（附重試），頁首只提示已有資料時的失敗。 */
  readonly error = computed(
    () =>
      this.actionError() ||
      this.catalogRead.error() ||
      (this.users()?.users.length ? this.usersRead.error() : ''),
  );
  readonly toast = inject(ToastService);

  load() {
    this.actionError.set('');
    this.catalogRead.reload();
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
    this.toast.success(title, description);
    this.catalogRead.reload();
    this.usersRead.reload();
    void this.session.load(true);
  }
  /** 功能或選單群組已移動：重新讀取目錄與此帳號的導覽（側邊欄）。 */
  reordered() {
    this.catalogRead.reload();
    void this.session.load(true);
  }
  roleNames(ids: string[]) {
    return ids.map((id) => this.catalog()?.roles.find((item) => item.id === id)?.name || id);
  }
  groupNames(ids: string[]) {
    return ids.map((id) => this.catalog()?.groups.find((item) => item.id === id)?.name || id);
  }
}
