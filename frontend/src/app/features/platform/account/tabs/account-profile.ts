import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { nameInitial } from '@shared/browser/format';
import { SettingGroup } from '@shared/ui/setting-row';

/** 帳號分頁的「個人資料」：目前只顯示目錄提供的姓名與帳號；自助維護的欄位之後加在這張卡片。 */
@Component({
  selector: 'nx-account-profile',
  imports: [SettingGroup],
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
        </div>
      </div>
    }
  </nx-setting-group>`,
})
export class AccountProfile {
  readonly session = inject(WorkspaceSession);
  readonly initial = nameInitial;
}
