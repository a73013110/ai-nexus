import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { safeMessage } from '@core/errors/safe-errors';
import { CompactDialog } from '@shared/ui/compact-dialog';
import { DialogHeader } from '@shared/ui/dialog-header';
import { Notice } from '@shared/ui/notice';
import { IdentityApi } from '../identity-api';
import { AdminUsersStore } from '../admin-users-store';
import { UserAccountFields, userAccountDraft, userAccountRequest } from './user-account-fields';
import { UserRoleFields } from './user-role-fields';

/**
 * 建立使用者只需要帳號與其角色；模型政策、儲存空間與活動要等使用者建立後才有，
 * 所以它們留在檢視器中。
 */
@Component({
  selector: 'nx-admin-user-create-dialog',
  imports: [CompactDialog, DialogHeader, Notice, UserAccountFields, UserRoleFields],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <dialog
      nxCompactDialog
      size="md"
      #dialog
      class="platform-dialog"
      (cancel)="saving() ? $event.preventDefault() : null"
    >
      <form class="dialog-form" (submit)="save($event)">
        <nx-dialog-header
          heading="新增使用者"
          detail="建立後可在使用者詳情調整 AI 模型與附件容量。"
          closeLabel="關閉新增使用者"
        />
        @if (opened()) {
          <div class="dialog-body">
            @if (error()) {
              <nx-notice tone="danger" [message]="error()" />
            }
            <div class="form-sections">
              <nx-user-account-fields
                [value]="account()"
                [disabled]="saving()"
                (valueChange)="account.set($event)"
              />
              <nx-user-role-fields
                [roleIds]="roleIds()"
                [enabled]="account().enabled"
                [disabled]="saving()"
                (roleIdsChange)="roleIds.set($event)"
              />
            </div>
          </div>
          <footer class="dialog-foot">
            <button
              type="button"
              class="secondary-button"
              [disabled]="saving()"
              (click)="dialog.close()"
            >
              取消
            </button>
            <button type="submit" class="primary-button" [disabled]="saving()">
              {{ saving() ? '建立中…' : '建立使用者' }}
            </button>
          </footer>
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
  readonly saving = signal(false);
  readonly error = signal('');
  open() {
    this.account.set(userAccountDraft());
    this.roleIds.set(['member']);
    this.error.set('');
    this.opened.set(true);
    this.dialog().nativeElement.showModal();
  }
  async save(event: Event) {
    event.preventDefault();
    if (this.saving()) return;
    this.saving.set(true);
    this.error.set('');
    try {
      await this.api.createUser(userAccountRequest(this.account(), this.roleIds()));
      this.dialog().nativeElement.close();
      this.store.saved('已建立使用者', '從列表開啟使用者可調整 AI 模型與附件容量。');
    } catch (error) {
      this.error.set(safeMessage(error));
    } finally {
      this.saving.set(false);
    }
  }
}
