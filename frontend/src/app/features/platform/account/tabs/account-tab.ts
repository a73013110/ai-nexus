import { ChangeDetectionStrategy, Component } from '@angular/core';
import { AccountSecurityStore } from '../security/account-security-store';
import { EmailDialog } from '../security/email-dialog';
import { LocalLoginDialog } from '../security/local-login-dialog';
import { ReauthenticationDialog } from '../security/reauthentication-dialog';
import { AccountAccess } from './account-access';
import { AccountProfile } from './account-profile';
import { AccountSignIn } from './account-sign-in';

/**
 * 帳號分頁：個人資料（含通知信箱）、登入方式（含本地登入）、角色與可用功能各是一張卡片。
 * 信箱與本地登入的變更各自經對話框立即生效（不經分頁的儲存列），共用 `AccountSecurityStore`。
 */
@Component({
  selector: 'nx-account-tab',
  imports: [
    AccountAccess,
    AccountProfile,
    AccountSignIn,
    EmailDialog,
    LocalLoginDialog,
    ReauthenticationDialog,
  ],
  providers: [AccountSecurityStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      gap: var(--section-gap);
    }
  `,
  template: `<nx-account-profile /><nx-account-sign-in /><nx-account-access />
    <nx-reauthentication-dialog /><nx-email-dialog /><nx-local-login-dialog />`,
})
export class AccountTab {}
