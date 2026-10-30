import { NgTemplateOutlet } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  viewChild,
} from '@angular/core';
import { formatNumber } from '@shared/browser/format';
import { ConfirmDialog } from '@shared/ui/confirm-dialog';
import { DialogSaveBar } from '@shared/ui/dialog-save-bar';
import { Field } from '@shared/ui/field';
import { Icon } from '@shared/ui/icon';
import { InfoPopover } from '@shared/ui/info-popover';
import { Notice } from '@shared/ui/notice';
import { PendingButton } from '@shared/ui/pending-button';
import { StatGrid, StatTile } from '@shared/ui/stat-tile';
import { AccessControlStore } from '@features/admin/access-control/access-control-store';
import { UserUnits } from '@features/admin/organization/user-units';
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
 * 帳號分頁裡的 AD 綁定與帳號狀態是立即生效的動作（各自確認，不經表單儲存）；
 * 出勤卡號唯讀，有 `attendance.cards` 才顯示，由出勤模組的元件自己讀取。
 */
@Component({
  selector: 'nx-admin-user-settings',
  imports: [
    AttendanceUserCards,
    ConfirmDialog,
    DialogSaveBar,
    DirectoryBinding,
    Field,
    Icon,
    InfoPopover,
    ModelPolicyEditor,
    NgTemplateOutlet,
    Notice,
    PendingButton,
    StatGrid,
    StatTile,
    StorageUsage,
    UserAccountFields,
    UserRoleFields,
    UserUnits,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './admin-user-settings.scss',
  templateUrl: './admin-user-settings.html',
})
export class AdminUserSettings {
  readonly store = inject(AdminUserStore);
  readonly admin = inject(AccessControlStore);
  readonly tab = input.required<UserSettingsTab>();
  private readonly confirmation = viewChild(ConfirmDialog);
  readonly emailState = emailState;
  readonly usage = computed(() => this.store.overview()?.usage ?? null);
  /** 還沒有詳情且正在讀：數字畫骨架；讀取失敗才顯示「—」。 */
  readonly pending = computed(() => !this.store.overview() && this.store.overviewRefreshing());
  readonly tokens = computed(() => {
    const usage = this.usage();
    return usage ? usage.inputTokens + usage.outputTokens : null;
  });
  readonly requestsNote = computed(() => {
    const usage = this.usage();
    return usage ? `${usage.completed} 完成 · ${usage.failed} 失敗` : '';
  });
  readonly tokensNote = computed(() => {
    const usage = this.usage();
    return usage
      ? `輸入 ${formatNumber(usage.inputTokens)} · 輸出 ${formatNumber(usage.outputTokens)}`
      : '';
  });
  readonly statusNote = computed(() => {
    if (this.store.dirty().account) return '請先儲存或還原帳號變更。';
    return this.store.current()?.enabled
      ? '停用後無法登入，資料與稽核保留。'
      : '啟用後可以再次登入。';
  });
  readonly number = (value: number | null | undefined) =>
    value === null || value === undefined ? null : formatNumber(value);
  /** 骨架列的寬度（%），長短交錯才不像一整塊色塊。 */
  protected readonly skeleton = [72, 90, 64, 84];

  submit(event: Event) {
    event.preventDefault();
    const tab = this.tab();
    if (tab === 'account') void this.store.saveAccount();
    if (tab === 'roles') void this.store.saveRoles();
    if (tab === 'models') void this.store.saveModels();
    if (tab === 'usage') void this.store.saveStorage();
  }

  /** 停用要確認（會讓既有工作階段失效）；啟用直接送出。 */
  async toggleEnabled() {
    const user = this.store.current();
    if (!user || this.store.locked() || this.store.dirty().account) return;
    if (user.enabled) {
      const confirmed = await this.confirmation()?.ask({
        title: `停用使用者：${user.displayName}`,
        message:
          '停用後無法登入，既有工作階段立即失效；角色、對話與稽核紀錄都保留，之後可以再啟用。',
        confirm: '停用',
        danger: true,
      });
      if (!confirmed) return;
    }
    await this.store.setEnabled(!user.enabled);
  }
}
