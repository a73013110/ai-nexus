import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import type { VerificationCodeDto } from '@core/api/schema';
import { safeMessage } from '@core/errors/safe-errors';
import { CompactDialog } from '@shared/ui/compact-dialog';
import { DialogHeader } from '@shared/ui/dialog-header';
import { Field } from '@shared/ui/field';
import { Notice } from '@shared/ui/notice';
import { PendingButton } from '@shared/ui/pending-button';
import { AccountSecurityStore } from './account-security-store';
import { bindSecurityDialog } from './security-dialog';
import { VerificationCodeField } from './verification-code-field';

/**
 * 設定、變更或驗證通知信箱：先把驗證碼寄到新地址，輸入後才生效。地址綁在驗證碼上，
 * 確認前改了輸入框也不影響這組驗證碼要驗的地址。
 */
@Component({
  selector: 'nx-email-dialog',
  imports: [CompactDialog, DialogHeader, Field, Notice, PendingButton, VerificationCodeField],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<dialog
    nxCompactDialog
    size="sm"
    #dialog
    class="platform-dialog"
    [busy]="busy()"
    (close)="store.dialog() === 'email' && store.close()"
  >
    <form class="dialog-form" (submit)="submit($event)">
      <nx-dialog-header heading="通知信箱" closeLabel="關閉通知信箱" />
      <div class="dialog-body">
        <p class="form-note">
          驗證碼與帳號安全通知會寄到這裡。新地址要輸入寄到該信箱的驗證碼才會生效；變更後其他裝置上的登入會登出。
        </p>
        @if (error()) {
          <nx-notice tone="danger" [message]="error()" />
        }
        <label
          >信箱<input
            nxField
            type="email"
            autocomplete="email"
            required
            maxlength="254"
            [value]="email()"
            [readOnly]="busy() || !!challenge()"
            (input)="email.set($any($event.target).value)"
        /></label>
        @if (challenge(); as current) {
          <nx-verification-code-field [challenge]="current" [(code)]="code" />
          <div class="dialog-inline-actions">
            <button
              nxPending
              type="button"
              class="secondary-button"
              [pending]="working() === 'resend'"
              [disabled]="working() === 'confirm'"
              (click)="send('resend')"
            >
              {{ working() === 'resend' ? '寄送中…' : '重新寄送' }}
            </button>
            <button type="button" class="secondary-button" [disabled]="busy()" (click)="edit()">
              改用其他地址
            </button>
          </div>
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
        <!-- 自己送出中不停用（焦點留在這顆）；重新寄送進行中才停用。 -->
        <button
          nxPending
          type="submit"
          class="primary-button"
          [pending]="working() === 'send' || working() === 'confirm'"
          [disabled]="working() === 'resend' || (!busy() && !ready())"
        >
          {{
            challenge()
              ? working() === 'confirm'
                ? '驗證中…'
                : '驗證並儲存'
              : working() === 'send'
                ? '寄送中…'
                : '寄送驗證碼'
          }}
        </button>
      </footer>
    </form>
  </dialog>`,
  styles: `
    .dialog-inline-actions {
      display: flex;
      flex-wrap: wrap;
      gap: var(--p-space-2);
    }
  `,
})
export class EmailDialog {
  readonly store = inject(AccountSecurityStore);
  readonly email = signal('');
  readonly code = signal('');
  readonly challenge = signal<VerificationCodeDto | null>(null);
  /** 進行中的動作：第一次寄送、重新寄送或驗證；只讓按下的那顆轉。 */
  readonly working = signal<'' | 'send' | 'resend' | 'confirm'>('');
  readonly busy = computed(() => this.working() !== '');
  readonly error = signal('');
  readonly ready = computed(() =>
    this.challenge() ? this.code().length === 6 : this.email().trim().length > 0,
  );
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');

  constructor() {
    bindSecurityDialog(this.store, 'email', this.dialog, () => {
      const security = this.store.security();
      // 目前的信箱還沒驗證時預先帶入，送出就是「驗證目前信箱」。
      this.email.set(security?.email && !security.emailVerifiedAt ? security.email : '');
      this.code.set('');
      this.challenge.set(null);
      this.error.set('');
    });
  }

  edit() {
    this.challenge.set(null);
    this.code.set('');
    this.error.set('');
  }

  async send(action: 'send' | 'resend' = 'send') {
    if (this.busy()) return;
    await this.busyWhile(action, async () => {
      const challenge = await this.store.requestEmail(this.email().trim());
      if (challenge) {
        this.challenge.set(challenge);
        this.code.set('');
      }
    });
  }

  async submit(event: Event) {
    event.preventDefault();
    if (this.busy() || !this.ready()) return;
    const challenge = this.challenge();
    if (!challenge) return this.send();
    await this.busyWhile('confirm', () => this.store.confirmEmail(challenge.id, this.code()));
  }

  private async busyWhile(action: 'send' | 'resend' | 'confirm', work: () => Promise<void>) {
    this.working.set(action);
    this.error.set('');
    try {
      await work();
    } catch (error) {
      this.error.set(safeMessage(error));
    } finally {
      this.working.set('');
    }
  }
}
