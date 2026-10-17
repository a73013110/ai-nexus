import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { DialogSaveBar } from '@shared/ui/dialog-save-bar';
import { Field } from '@shared/ui/field';
import { InfoPopover } from '@shared/ui/info-popover';
import { Notice } from '@shared/ui/notice';
import { AdministrationStore } from '../administration-store';
import { StorageUsage } from '@features/attachments/file-library/storage-usage';
import { ModelPolicyEditor } from '../model-policies/model-policy-editor';
import { AdminUserStore, type UserSettingsTab } from './admin-user-store';
import { UserAccountFields } from './user-account-fields';
import { UserRoleFields } from './user-role-fields';

/**
 * 檢視器中可編輯的分頁。每一個都是獨立的表單，有自己的儲存、錯誤與未儲存狀態；
 * 欄位是可捲動的 `.form-sections`，`DialogSaveBar` 則固定在底部。
 */
@Component({
  selector: 'nx-admin-user-settings',
  imports: [
    DialogSaveBar,
    Field,
    InfoPopover,
    ModelPolicyEditor,
    NgTemplateOutlet,
    Notice,
    StorageUsage,
    UserAccountFields,
    UserRoleFields,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './admin-user-settings.scss',
  templateUrl: './admin-user-settings.html',
})
export class AdminUserSettings {
  readonly store = inject(AdminUserStore);
  readonly admin = inject(AdministrationStore);
  readonly session = inject(WorkspaceSession);
  readonly tab = input.required<UserSettingsTab>();
  submit(event: Event) {
    event.preventDefault();
    const tab = this.tab();
    if (tab === 'account') void this.store.saveAccount();
    if (tab === 'roles') void this.store.saveRoles();
    if (tab === 'models') void this.store.saveModels();
    if (tab === 'storage') void this.store.saveStorage();
  }
}
