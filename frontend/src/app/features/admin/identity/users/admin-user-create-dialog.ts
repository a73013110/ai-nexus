import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { action } from '@shared/browser/action';
import { CompactDialog } from '@shared/ui/compact-dialog';
import { DialogHeader } from '@shared/ui/dialog-header';
import { Notice } from '@shared/ui/notice';
import { IdentityApi } from '../identity-api';
import { AdminUsersStore } from '../admin-users-store';
import { UserAccountFields, userAccountDraft, userAccountRequest } from './user-account-fields';
import { UserRoleFields } from './user-role-fields';
import { DialogActions } from '@shared/ui/dialog-actions';

/**
 * 建立使用者只需要帳號與其角色；模型政策、儲存空間與活動要等使用者建立後才有，
 * 所以它們留在檢視器中。
 */
@Component({
  selector: 'nx-admin-user-create-dialog',
  imports: [DialogActions, CompactDialog, DialogHeader, Notice, UserAccountFields, UserRoleFields],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <dialog nxCompactDialog size="md" #dialog class="platform-dialog" [busy]="saving.pending()">
      <form class="dialog-form" (submit)="save($event)">
        <nx-dialog-header
          heading="新增使用者"
          detail="建立後可在使用者詳情調整 AI 模型與附件容量。"
          closeLabel="關閉新增使用者"
        />
        @if (opened()) {
          <div class="dialog-body">
            @if (saving.error()) {
              <nx-notice tone="danger" [message]="saving.error()" />
            }
            <div class="form-sections">
              <nx-user-account-fields
                [value]="account()"
                [disabled]="saving.pending()"
                (valueChange)="account.set($event)"
              />
              <nx-user-role-fields
                [roleIds]="roleIds()"
                [disabled]="saving.pending()"
                (roleIdsChange)="roleIds.set($event)"
              />
            </div>
          </div>
          <nx-dialog-actions
            label="建立使用者"
            pendingLabel="建立中…"
            [pending]="saving.pending()"
            (dismiss)="dialog.close()"
          />
        }
      </form>
    </dialog>
  `,
})
export class AdminUserCreateDialog {
  private readonly api = inject(IdentityApi);
  private readonly store = inject(AdminUsersStore);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  /** 表單在首次開啟時建立並保留，讓關閉中的對話框不會被清空。 */
  readonly opened = signal(false);
  readonly account = signal(userAccountDraft());
  readonly roleIds = signal(['member']);
  readonly saving = action();
  open() {
    this.account.set(userAccountDraft());
    this.roleIds.set(['member']);
    this.saving.error.set('');
    this.opened.set(true);
    this.dialog().nativeElement.showModal();
  }
  async save(event: Event) {
    event.preventDefault();
    const outcome = await this.saving.run(() =>
      this.api.createUser(userAccountRequest(this.account(), this.roleIds())),
    );
    if (!outcome.ok) return;
    this.dialog().nativeElement.close();
    this.store.saved('已建立使用者', '從列表開啟使用者可調整 AI 模型與附件容量。');
  }
}
