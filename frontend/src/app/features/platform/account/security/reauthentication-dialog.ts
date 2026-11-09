import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  inject,
  linkedSignal,
  signal,
  viewChild,
} from '@angular/core';
import type { VerificationCodeDto } from '@core/api/schema';
import { action } from '@shared/browser/action';
import { CompactDialog } from '@shared/ui/compact-dialog';
import { DialogHeader } from '@shared/ui/dialog-header';
import { Field } from '@shared/ui/field';
import { Notice } from '@shared/ui/notice';
import { PasswordField, PasswordInput } from '@shared/ui/password-field';
import { PendingButton } from '@shared/ui/pending-button';
import { ViewSwitch } from '@shared/ui/view-switch';
import { AccountSecurityStore } from './account-security-store';
import { bindSecurityDialog } from './security-dialog';
import { VerificationCodeField } from './verification-code-field';

const METHODS = [
  { value: 'password', label: '目前的密碼' },
  { value: 'code', label: '信箱驗證碼' },
];

/** 步升驗證：變更信箱或登入方式前，以目前登入方式的密碼或寄到已驗證信箱的驗證碼確認本人。 */
@Component({
  selector: 'nx-reauthentication-dialog',
  imports: [
    CompactDialog,
    DialogHeader,
    Field,
    Notice,
    PasswordField,
    PasswordInput,
    PendingButton,
    VerificationCodeField,
    ViewSwitch,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<dialog
    nxCompactDialog
    size="sm"
    #dialog
    class="platform-dialog"
    [busy]="busy()"
    (close)="store.dialog() === 'reauthenticate' && store.close()"
  >
    <form class="dialog-form" (submit)="submit($event)">
      <nx-dialog-header heading="確認是你本人" closeLabel="關閉確認身分" />
      <div class="dialog-body">
        <p class="form-note">變更信箱或登入方式前請再確認一次身分；確認後幾分鐘內不必重複輸入。</p>
        @if (working.error()) {
          <nx-notice tone="danger" [message]="working.error()" />
        }
        @if (options().length === 0) {
          <nx-notice
            tone="warning"
            message="目前沒有可用的確認方式（沒有可驗證的密碼或已驗證信箱），請聯絡系統管理員。"
          />
        } @else if (options().length > 1) {
          <nx-view-switch
            label="確認方式"
            appearance="segment"
            [options]="options()"
            [value]="method()"
            (valueChange)="method.set($event)"
          />
        }
        @if (method() === 'password') {
          <div class="ui-field">
            <label for="reauthentication-password">{{ passwordLabel() }}</label>
            <nx-password-field [disabled]="busy()">
              <input
                id="reauthentication-password"
                nxField
                nxPasswordInput
                required
                maxlength="1024"
                [value]="password()"
                [readOnly]="busy()"
                (input)="password.set($any($event.target).value)"
              />
            </nx-password-field>
          </div>
        } @else if (method() === 'code') {
          @if (challenge(); as current) {
            <nx-verification-code-field [challenge]="current" [(code)]="code" />
          } @else {
            <p class="form-note">驗證碼會寄到 {{ store.security()?.email }}，15 分鐘內有效。</p>
          }
          <button
            nxPending
            type="button"
            class="secondary-button"
            [pending]="working.pendingFor('send')"
            [disabled]="working.pendingFor('confirm')"
            (click)="send()"
          >
            {{
              working.pendingFor('send') ? '寄送中…' : challenge() ? '重新寄送驗證碼' : '寄送驗證碼'
            }}
          </button>
        }
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
        <button
          nxPending
          type="submit"
          class="primary-button"
          [pending]="working.pendingFor('confirm')"
          [disabled]="working.pendingFor('send') || (!busy() && !ready())"
        >
          {{ working.pendingFor('confirm') ? '確認中…' : '確認' }}
        </button>
      </footer>
    </form>
  </dialog>`,
})
export class ReauthenticationDialog {
  readonly store = inject(AccountSecurityStore);
  readonly options = computed(() =>
    METHODS.filter((method) => this.store.security()?.reauthMethods.includes(method.value)),
  );
  readonly method = linkedSignal(() => this.options()[0]?.value ?? '');
  readonly passwordLabel = computed(() =>
    this.store.security()?.currentMethod === 'local' ? '本地密碼' : 'AD 密碼',
  );
  readonly password = signal('');
  readonly code = signal('');
  readonly challenge = signal<VerificationCodeDto | null>(null);
  /** 寄送驗證碼（`send`）或確認身分（`confirm`）：只讓按下的那顆轉。 */
  readonly working = action();
  readonly busy = this.working.pending;
  readonly ready = computed(() =>
    this.method() === 'password'
      ? this.password().length > 0
      : !!this.challenge() && this.code().length === 6,
  );
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');

  constructor() {
    bindSecurityDialog(this.store, 'reauthenticate', this.dialog, () => {
      this.password.set('');
      this.code.set('');
      this.challenge.set(null);
      this.working.error.set('');
    });
  }

  async send() {
    const outcome = await this.working.run(() => this.store.sendCode(), { key: 'send' });
    if (!outcome.ok) return;
    this.challenge.set(outcome.value);
    this.code.set('');
  }

  async submit(event: Event) {
    event.preventDefault();
    if (this.busy() || !this.ready()) return;
    const challenge = this.challenge();
    await this.working.run(
      () =>
        this.store.reauthenticate(
          this.method() === 'password'
            ? { password: this.password() }
            : { challengeId: challenge!.id, code: this.code() },
        ),
      { key: 'confirm' },
    );
  }
}
