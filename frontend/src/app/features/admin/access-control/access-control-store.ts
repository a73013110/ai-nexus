import { Injectable, computed, inject, signal } from '@angular/core';
import { apiResource } from '@core/api/api-resource';
import { WorkspaceSession } from '@core/auth/workspace-session';
import type { ChoiceOption } from '@shared/ui/choice-groups';
import { ToastService } from '@shared/ui/toast-service';
import { AccessControlApi } from './access-control-api';

/**
 * 頁面提供（`providers`）的存取目錄：角色、功能群組與功能。存取控制各頁與使用者頁各自提供一份，
 * 頁面內的表格與編輯器共用它；選單頁用它取得功能名稱與可見群組的選項。
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
      'access-control.navigation',
    ],
    freshAccount: true,
    loader: () => this.api.catalog(),
  });
  readonly catalog = computed(() => this.catalogRead.value() ?? null);
  /** 首次載入（表格畫骨架）。 */
  readonly loading = this.catalogRead.loading;
  /** 重讀中（含寫入後與重試）：表格淡化、重試鈕轉圈。 */
  readonly refreshing = this.catalogRead.refreshing;
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
  /** 選單已改變：只重新讀取此帳號的導覽（側邊欄），目錄不受影響。 */
  navigationChanged() {
    void this.session.load(true);
  }
  roleNames(ids: string[]) {
    return ids.map((id) => this.catalog()?.roles.find((item) => item.id === id)?.name || id);
  }
  groupNames(ids: string[]) {
    return ids.map((id) => this.catalog()?.groups.find((item) => item.id === id)?.name || id);
  }
}

/** 角色、功能群組或功能做為勾選項：停用的仍列出並標示，識別碼放在第二行。 */
export function accessChoice(item: { id: string; name: string; enabled: boolean }): ChoiceOption {
  return { id: item.id, label: item.name + (item.enabled ? '' : '（停用）'), description: item.id };
}
