import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { safeMessage } from '@core/errors/safe-errors';
import { CompactDialog } from '@shared/ui/compact-dialog';
import { DialogHeader } from '@shared/ui/dialog-header';
import { Field } from '@shared/ui/field';
import { Notice } from '@shared/ui/notice';
import { PendingButton } from '@shared/ui/pending-button';
import { AccountSecurityStore } from './account-security-store';
import { bindSecurityDialog } from './security-dialog';

/** 本地密碼的長度規則與伺服器相同；常見密碼、包含帳號名等由伺服器判斷（`password_weak`）。 */
export function localPasswordProblem(password: string, confirmation: string) {
  if (password.length < 12 || password.length > 128) return '密碼需為 12–128 個字元，可使用長句。';
  if (!password.trim()) return '密碼不能只有空白。';
  if (password !== confirmation) return '兩次輸入的密碼不一致。';
  return '';
}

/** 啟用本地登入，或變更本地帳號名與密碼。 */
@Component({
  selector: 'nx-local-login-dialog',
  imports: [CompactDialog, DialogHeader, Field, Notice, PendingButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<dialog
    nxCompactDialog
    size="sm"
    #dialog
    class="platform-dialog"
    [busy]="busy()"
    (close)="store.dialog() === 'local-login' && store.close()"
  >
    <form class="dialog-form" (submit)="submit($event)">
      <nx-dialog-header [heading]="heading()" closeLabel="關閉本地登入設定" />
      <div class="dialog-body">
        <p class="form-note">
          本地密碼與 AD 密碼分開管理，可在 AD 無法使用時登入；AD 使用者需定期（預設 30 天內）用 AD
          登入一次，本地密碼才會持續有效。儲存後其他裝置上的登入會登出。
        </p>
        @if (error()) {
          <nx-notice tone="danger" [message]="error()" />
        }
        <label
          >本地帳號<input
            nxField
            autocomplete="username"
            required
            maxlength="64"
            [value]="account()"
            [readOnly]="busy()"
            (input)="account.set($any($event.target).value)"
        /></label>
        <label
          >新密碼<input
            nxField
            type="password"
            autocomplete="new-password"
            required
            minlength="12"
            maxlength="128"
            [value]="password()"
            [readOnly]="busy()"
            (input)="password.set($any($event.target).value)"
        /></label>
        <label
          >再輸入一次<input
            nxField
            type="password"
            autocomplete="new-password"
            required
            maxlength="128"
            [value]="confirmation()"
            [readOnly]="busy()"
            (input)="confirmation.set($any($event.target).value)"
        /></label>
        <p class="form-note">12–128 個字元，建議用一句好記的長句；不可包含帳號名稱或常見密碼。</p>
      </div>
      <footer class="dialog-foot">
        <button
          type="button"
          class="secondary-button"
          [attr.aria-disabled]="busy() || null"
          (click)="busy() || store.close()"
        >
          取消
        </button>
        <button nxPending type="submit" class="primary-button" [pending]="busy()">
          {{ busy() ? '儲存中…' : '儲存' }}
        </button>
      </footer>
    </form>
  </dialog>`,
})
export class LocalLoginDialog {
  readonly store = inject(AccountSecurityStore);
  readonly heading = computed(() =>
    this.store.security()?.localLogin.enabled ? '變更本地登入' : '啟用本地登入',
  );
  readonly account = signal('');
  readonly password = signal('');
  readonly confirmation = signal('');
  readonly busy = signal(false);
  readonly error = signal('');
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');

  constructor() {
    bindSecurityDialog(this.store, 'local-login', this.dialog, () => {
      const security = this.store.security();
      // 沒有本地帳號時建議沿用 AD 帳號名（伺服器允許與自己的 AD 帳號相同）。
      this.account.set((security?.localLogin.account ?? security?.adAccount ?? '').toLowerCase());
      this.password.set('');
      this.confirmation.set('');
      this.error.set('');
    });
  }

  async submit(event: Event) {
    event.preventDefault();
    if (this.busy()) return;
    const problem = localPasswordProblem(this.password(), this.confirmation());
    if (problem) return this.error.set(problem);
    this.busy.set(true);
    this.error.set('');
    try {
      await this.store.setLocalLogin({ account: this.account().trim(), password: this.password() });
    } catch (error) {
      this.error.set(safeMessage(error));
    } finally {
      this.busy.set(false);
    }
  }
}
