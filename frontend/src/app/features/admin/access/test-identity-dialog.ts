import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import type { AdminUserDto } from '../../../core/api/schema';
import { WorkspaceSession } from '../../../core/auth/workspace-session';
import { safeMessage } from '../../../core/errors/safe-errors';
import { CompactDialog } from '../../../shared/ui/compact-dialog';
import { Field } from '../../../shared/ui/field';
import { Icon } from '../../../shared/ui/icon';
import { Notice } from '../../../shared/ui/notice';

/** Switches to another user's identity for a short, audited test. */
@Component({
  selector: 'nx-test-identity-dialog',
  imports: [CompactDialog, Field, Icon, Notice],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <dialog
      nxCompactDialog
      #testDialog
      class="platform-dialog"
      aria-labelledby="test-identity-title"
      (close)="user.set(null)"
      (cancel)="busy() && $event.preventDefault()"
    >
      <div class="dialog-scroll">
        <div class="dialog-heading">
          <h2 id="test-identity-title">以 {{ user()?.displayName }} 的身分測試</h2>
          <button
            type="button"
            class="icon-button"
            aria-label="關閉身分測試"
            [disabled]="busy()"
            (click)="testDialog.close()"
          >
            <nx-icon name="close" />
          </button>
        </div>
        <p class="form-note">
          接下來會使用此使用者的實際權限與私人資料範圍。操作會生效並記錄管理者來源；15
          分鐘後返回原管理者，亦可隨時提前返回。
        </p>
        @if (error()) {
          <nx-notice tone="danger" [message]="error()" />
        }
        <form class="platform-form" (submit)="start($event)">
          <label
            >測試目的<input
              nxField
              aria-label="測試目的"
              required
              minlength="4"
              maxlength="240"
              [value]="reason()"
              [readOnly]="busy()"
              (input)="reason.set($any($event.target).value)"
          /></label>
          <div class="dialog-actions">
            <button
              type="button"
              class="secondary-button"
              [disabled]="busy()"
              (click)="testDialog.close()"
            >
              取消
            </button>
            <button type="submit" class="primary-button" [disabled]="busy()">
              {{ busy() ? '正在切換…' : '開始身分測試' }}
            </button>
          </div>
        </form>
      </div>
    </dialog>
  `,
})
export class TestIdentityDialog {
  private readonly session = inject(WorkspaceSession);
  readonly user = signal<AdminUserDto | null>(null);
  readonly reason = signal('驗證角色權限與功能操作');
  readonly busy = signal(false);
  readonly error = signal('');
  private readonly testDialog = viewChild.required<ElementRef<HTMLDialogElement>>('testDialog');
  open(user: AdminUserDto) {
    this.user.set(user);
    this.reason.set('驗證角色權限與功能操作');
    this.error.set('');
    this.testDialog().nativeElement.showModal();
  }
  async start(event: Event) {
    event.preventDefault();
    const user = this.user();
    if (!user || this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    try {
      await this.session.auth.testIdentity(user.id, this.reason());
    } catch (error) {
      this.error.set(safeMessage(error));
      this.busy.set(false);
    }
  }
}
