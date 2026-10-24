import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { DialogSaveBar } from '@shared/ui/dialog-save-bar';
import { Field } from '@shared/ui/field';
import { Icon } from '@shared/ui/icon';
import { InfoPopover } from '@shared/ui/info-popover';
import { Notice } from '@shared/ui/notice';
import { PendingButton } from '@shared/ui/pending-button';
import { AccessControlStore } from '@features/admin/access-control/access-control-store';
import { StorageUsage } from '@features/work/files/file-library/storage-usage';
import { ModelPolicyEditor } from '@features/platform/inference/model-policies/model-policy-editor';
import { AttendanceUserCards } from '@features/office/attendance/cards/attendance-user-cards';
import { AdminUserStore, type UserSettingsTab } from './admin-user-store';
import { DirectoryBinding } from './directory-binding';
import { UserAccountFields, emailState } from './user-account-fields';
import { UserRoleFields } from './user-role-fields';

/**
 * 檢視器中可編輯的分頁。每一個都是獨立的表單，有自己的儲存、錯誤與未儲存狀態；
 * 欄位是可捲動的 `.form-sections`，`DialogSaveBar` 則固定在底部。
 * 帳號分頁另有 AD 綁定（立即送出，不經表單儲存）與出勤卡號（唯讀，有 `attendance.cards` 才顯示），由出勤模組的元件自己讀取。
 */
@Component({
  selector: 'nx-admin-user-settings',
  imports: [
    AttendanceUserCards,
    DialogSaveBar,
    DirectoryBinding,
    Field,
    Icon,
    InfoPopover,
    ModelPolicyEditor,
    NgTemplateOutlet,
    Notice,
    PendingButton,
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
  readonly admin = inject(AccessControlStore);
  readonly session = inject(WorkspaceSession);
  readonly tab = input.required<UserSettingsTab>();
  readonly emailState = emailState;
  /** 骨架列的寬度（%），長短交錯才不像一整塊色塊。 */
  protected readonly skeleton = [72, 90, 64, 84];
  submit(event: Event) {
    event.preventDefault();
    const tab = this.tab();
    if (tab === 'account') void this.store.saveAccount();
    if (tab === 'email') void this.store.saveEmail();
    if (tab === 'roles') void this.store.saveRoles();
    if (tab === 'models') void this.store.saveModels();
    if (tab === 'storage') void this.store.saveStorage();
  }
}
