import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { safeMessage } from '../../../core/errors/safe-errors';
import { CompactDialog } from '../../../shared/ui/compact-dialog';
import { Icon } from '../../../shared/ui/icon';
import { Notice } from '../../../shared/ui/notice';
import { AdminApi } from '../admin-api';
import { AdminStore } from '../admin-store';
import { UserAccountFields, userAccountDraft, userAccountRequest } from './user-account-fields';
import { UserRoleFields } from './user-role-fields';

/**
 * Creating a user needs only the account and its roles; model policy, storage and activity exist
 * once the user does, so they stay in the inspector.
 */
@Component({
  selector: 'nx-admin-user-create-dialog',
  imports: [CompactDialog, Icon, Notice, UserAccountFields, UserRoleFields],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    .admin-user-create {
      --dialog-width: 42rem;
    }
    form {
      display: grid;
      gap: var(--p-space-3);
    }
    .dialog-actions {
      margin-top: 0;
    }
  `,
  template: `
    <dialog
      nxCompactDialog
      #dialog
      class="platform-dialog admin-user-create"
      aria-labelledby="user-create-title"
      (cancel)="saving() && $event.preventDefault()"
    >
      <div class="dialog-scroll">
        <div class="dialog-heading">
          <div>
            <h2 id="user-create-title">新增使用者</h2>
            <p>建立後可在使用者詳情調整 AI 模型與附件容量。</p>
          </div>
          <button
            type="button"
            class="icon-button"
            aria-label="關閉新增使用者"
            [disabled]="saving()"
            (click)="dialog.close()"
          >
            <nx-icon name="close" />
          </button>
        </div>
        @if (opened()) {
          @if (error()) {
            <nx-notice tone="danger" [message]="error()" />
          }
          <form (submit)="save($event)">
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
            <div class="dialog-actions">
              <button
                type="button"
                class="secondary-button"
                [disabled]="saving()"
                (click)="dialog.close()"
              >
                取消</button
              ><button type="submit" class="primary-button" [disabled]="saving()">
                {{ saving() ? '建立中…' : '建立使用者' }}
              </button>
            </div>
          </form>
        }
      </div>
    </dialog>
  `,
})
export class AdminUserCreateDialog {
  private readonly api = inject(AdminApi);
  private readonly store = inject(AdminStore);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  /** The form is built on first open and kept, so the closing dialog does not empty. */
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
