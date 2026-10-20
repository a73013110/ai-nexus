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
import { safeMessage } from '@core/errors/safe-errors';
import { CompactDialog } from '@shared/ui/compact-dialog';
import { DialogHeader } from '@shared/ui/dialog-header';
import { Field } from '@shared/ui/field';
import { Notice } from '@shared/ui/notice';
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
  imports: [CompactDialog, DialogHeader, Field, Notice, VerificationCodeField, ViewSwitch],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<dialog
    nxCompactDialog
    size="sm"
    #dialog
    class="platform-dialog"
    (close)="store.dialog() === 'reauthenticate' && store.close()"
    (cancel)="busy() ? $event.preventDefault() : null"
  >
    <form class="dialog-form" (submit)="submit($event)">
      <nx-dialog-header heading="確認是你本人" closeLabel="關閉確認身分" />
      <div class="dialog-body">
        <p class="form-note">變更信箱或登入方式前請再確認一次身分；確認後幾分鐘內不必重複輸入。</p>
        @if (error()) {
          <nx-notice tone="danger" [message]="error()" />
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
          <label
            >{{ passwordLabel()
            }}<input
              nxField
              type="password"
              autocomplete="current-password"
              required
              maxlength="1024"
              [value]="password()"
              [readOnly]="busy()"
              (input)="password.set($any($event.target).value)"
          /></label>
        } @else if (method() === 'code') {
          @if (challenge(); as current) {
            <nx-verification-code-field [challenge]="current" [(code)]="code" />
          } @else {
            <p class="form-note">驗證碼會寄到 {{ store.security()?.email }}，15 分鐘內有效。</p>
          }
          <button type="button" class="secondary-button" [disabled]="busy()" (click)="send()">
            {{ challenge() ? '重新寄送驗證碼' : '寄送驗證碼' }}
          </button>
        }
      </div>
      <footer class="dialog-foot">
        <button type="button" class="secondary-button" [disabled]="busy()" (click)="store.close()">
          取消
        </button>
        <button type="submit" class="primary-button" [disabled]="busy() || !ready()">
          {{ busy() ? '確認中…' : '確認' }}
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
  readonly busy = signal(false);
  readonly error = signal('');
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
      this.error.set('');
    });
  }

  async send() {
    this.busy.set(true);
    this.error.set('');
    try {
      this.challenge.set(await this.store.sendCode());
      this.code.set('');
    } catch (error) {
      this.error.set(safeMessage(error));
    } finally {
      this.busy.set(false);
    }
  }

  async submit(event: Event) {
    event.preventDefault();
    if (this.busy() || !this.ready()) return;
    this.busy.set(true);
    this.error.set('');
    try {
      const challenge = this.challenge();
      await this.store.reauthenticate(
        this.method() === 'password'
          ? { password: this.password() }
          : { challengeId: challenge!.id, code: this.code() },
      );
    } catch (error) {
      this.error.set(safeMessage(error));
    } finally {
      this.busy.set(false);
    }
  }
}
