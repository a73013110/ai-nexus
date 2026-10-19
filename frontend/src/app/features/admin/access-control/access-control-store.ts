import { Injectable, computed, inject, signal } from '@angular/core';
import { apiResource } from '@core/api/api-resource';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { ToastService } from '@shared/ui/toast-service';
import { AccessControlApi } from './access-control-api';

/**
 * 頁面提供（`providers`）的存取目錄：角色、功能群組、功能與選單分群。存取控制三頁與使用者頁各自提供一份，
 * 頁面內的表格與編輯器共用它。
 */
@Injectable()
export class AccessControlStore {
  private readonly api = inject(AccessControlApi);
  private readonly session = inject(WorkspaceSession);
  /** 帳號會重新讀取：這些頁面會改變它可做的事。授權與後端的 `AccessCatalog` policy 相同。 */
  private readonly catalogRead = apiResource({
    feature: [
      'identity.users',
      'access-control.roles',
      'access-control.groups',
      'access-control.features',
    ],
    freshAccount: true,
    loader: () => this.api.catalog(),
  });
  readonly catalog = computed(() => this.catalogRead.value() ?? null);
  /** 每個選單群組，包括空的：編輯器用來排列，摘要則依群組分類。 */
  readonly navigationGroups = computed(() => this.catalog()?.navigationGroups ?? []);
  readonly loading = this.catalogRead.loading;
  readonly actionError = signal('');
  readonly error = computed(() => this.actionError() || this.catalogRead.error());
  readonly toast = inject(ToastService);

  load() {
    this.actionError.set('');
    this.catalogRead.reload();
  }
  /** 授權已改變：重新讀取目錄與此帳號的授權。 */
  saved(title: string, description?: string) {
    this.toast.success(title, description);
    this.catalogRead.reload();
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
