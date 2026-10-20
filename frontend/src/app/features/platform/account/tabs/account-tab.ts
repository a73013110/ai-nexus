import { ChangeDetectionStrategy, Component } from '@angular/core';
import { AccountAccess } from './account-access';
import { AccountProfile } from './account-profile';
import { AccountSignIn } from './account-sign-in';

/**
 * 帳號分頁：個人資料、登入方式、角色與可用功能各是一張卡片元件，之後加入自助維護時只改對應的卡片。
 * 目前全部唯讀，所以這個分頁沒有儲存列。
 */
@Component({
  selector: 'nx-account-tab',
  imports: [AccountAccess, AccountProfile, AccountSignIn],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      gap: var(--section-gap);
    }
  `,
  template: `<nx-account-profile /><nx-account-sign-in /><nx-account-access />`,
})
export class AccountTab {}
