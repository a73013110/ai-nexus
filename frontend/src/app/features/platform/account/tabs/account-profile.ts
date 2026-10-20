import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { nameInitial } from '@shared/browser/format';
import { Notice } from '@shared/ui/notice';
import { SettingGroup, SettingRow } from '@shared/ui/setting-row';
import { StatusBadge } from '@shared/ui/status-badge';
import { AccountSecurityStore, emailBadge } from '../security/account-security-store';

/**
 * 帳號分頁的「個人資料」：目錄提供的姓名與帳號（唯讀，姓名自改等於可冒名），以及通知信箱。
 * AD 提供的信箱由目錄維護；其他情況可自行設定，以寄到新地址的驗證碼確認。
 */
@Component({
  selector: 'nx-account-profile',
  imports: [Notice, SettingGroup, SettingRow, StatusBadge],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    .account-identity {
      display: flex;
      align-items: center;
      gap: var(--p-space-4);
      min-width: 0;
      padding-top: var(--p-space-3);
      border-top: 1px solid var(--line);
    }
    .avatar {
      width: 3rem;
      height: 3rem;
      font-size: var(--text-ui);
    }
    .account-identity div {
      display: grid;
      gap: var(--p-space-1);
      min-width: 0;
    }
    .account-identity strong {
      font-size: var(--text-ui);
      font-weight: 600;
    }
    .account-identity span:not(.avatar) {
      color: var(--secondary);
      font-size: var(--text-caption);
      overflow-wrap: anywhere;
    }
  `,
  template: `<nx-setting-group
    heading="個人資料"
    description="姓名與帳號由公司目錄提供；需要更正時請聯絡資訊管理人員。"
  >
    @if (session.me(); as me) {
      <div class="account-identity">
        <span class="avatar" aria-hidden="true">{{ initial(me.displayName) }}</span>
        <div>
          <strong>{{ me.displayName }}</strong>
          <span>{{ me.account }}</span>
          @if (store.security()?.adAccount; as adAccount) {
            <span>AD 帳號：{{ adAccount }}（登入時只需輸入這個名稱）</span>
          }
        </div>
      </div>
    }
    @if (store.error()) {
      <nx-notice tone="danger" [message]="store.error()" />
    }
    @if (store.security(); as security) {
      <nx-setting-row label="通知信箱" [description]="emailDescription()">
        <nx-status-badge [tone]="badge()!.tone">{{ badge()!.label }}</nx-status-badge>
        @if (store.editable() && security.emailEditable) {
          <button
            type="button"
            class="secondary-button"
            [disabled]="!security.mailAvailable"
            (click)="store.open('email')"
          >
            {{ !security.email ? '設定' : security.emailVerifiedAt ? '變更' : '驗證' }}
          </button>
        }
      </nx-setting-row>
    }
  </nx-setting-group>`,
})
export class AccountProfile {
  readonly session = inject(WorkspaceSession);
  readonly store = inject(AccountSecurityStore);
  readonly initial = nameInitial;
  readonly badge = computed(() => {
    const security = this.store.security();
    return security ? emailBadge(security) : null;
  });
  readonly emailDescription = computed(() => {
    const security = this.store.security();
    if (!security) return '';
    const address = security.email ?? '尚未設定；驗證碼與帳號安全通知會寄到這裡。';
    if (security.emailSource === 'directory')
      return `${address}（由公司 AD 提供，需更正請聯絡資訊管理人員）`;
    if (!security.mailAvailable) return `${address}（系統尚未設定寄信，暫時無法驗證）`;
    return address;
  });
}
