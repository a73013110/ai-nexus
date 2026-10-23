import { ChangeDetectionStrategy, Component, computed, inject, viewChild } from '@angular/core';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { isLoginMethod, LOGIN_METHODS } from '@features/admin/identity/login-methods';
import { formatDate } from '@shared/browser/format';
import { ConfirmDialog } from '@shared/ui/confirm-dialog';
import { PendingButton } from '@shared/ui/pending-button';
import { SettingGroup, SettingRow } from '@shared/ui/setting-row';
import { StatusBadge } from '@shared/ui/status-badge';
import { AccountSecurityStore } from '../security/account-security-store';

/**
 * 帳號分頁的「登入方式」：目前這次工作階段用哪種方式登入，以及本地登入（帳號名＋密碼）的啟用、變更與停用。
 * 登出與切換登入方式在登入者選單，這裡不重複。
 */
@Component({
  selector: 'nx-account-sign-in',
  imports: [ConfirmDialog, PendingButton, SettingGroup, SettingRow, StatusBadge],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    .sign-in-method {
      color: var(--secondary);
      font-size: var(--text-caption);
      white-space: nowrap;
    }
    .row-skeleton {
      display: grid;
      gap: var(--p-space-2);
      padding-block: var(--p-space-3);
    }
    .row-skeleton .skeleton {
      height: 0.875rem;
    }
    .row-skeleton .skeleton + .skeleton {
      width: 60%;
    }
  `,
  template: `<nx-setting-group heading="登入方式">
      <nx-setting-row label="目前的登入方式" [description]="current().description">
        <span class="sign-in-method">{{ current().label }}</span>
      </nx-setting-row>
      @if (store.security(); as security) {
        <nx-setting-row label="本地登入" [description]="localDescription()">
          <nx-status-badge [tone]="security.localLogin.enabled ? 'success' : 'neutral'">{{
            security.localLogin.enabled ? '已啟用' : '未啟用'
          }}</nx-status-badge>
          @if (store.editable() && security.localLogin.allowed) {
            <button type="button" class="secondary-button" (click)="store.open('local-login')">
              {{ security.localLogin.enabled ? '變更' : '啟用' }}
            </button>
          }
          @if (store.editable() && security.localLogin.enabled && !security.localLogin.required) {
            <button
              nxPending
              type="button"
              class="secondary-button"
              [pending]="store.disabling()"
              (click)="disable()"
            >
              {{ store.disabling() ? '停用中…' : '停用' }}
            </button>
          }
        </nx-setting-row>
      } @else if (store.loading()) {
        <div class="row-skeleton" role="status">
          <span class="skeleton"></span><span class="skeleton"></span>
          <span class="sr-only">正在載入本地登入狀態…</span>
        </div>
      }
      @if (session.testing()) {
        <nx-setting-row
          label="測試身分"
          description="你正以其他使用者的身分檢視工作區；要返回管理者請使用登入者選單。"
        />
      }
    </nx-setting-group>
    <nx-confirm-dialog />`,
})
export class AccountSignIn {
  readonly session = inject(WorkspaceSession);
  readonly store = inject(AccountSecurityStore);
  private readonly confirm = viewChild.required(ConfirmDialog);
  readonly localDescription = computed(() => {
    const login = this.store.security()?.localLogin;
    if (!login) return '';
    if (!login.enabled)
      return login.allowed
        ? '可另外設定本地帳號與密碼，在 AD 無法使用時登入。'
        : '系統未開放自行設定本地登入。';
    const changed = login.passwordChangedAt
      ? `，密碼最後變更 ${formatDate(login.passwordChangedAt)}`
      : '';
    const only = login.required ? '；這是此帳號唯一的登入方式，不能停用' : '';
    return `本地帳號 ${login.account}${changed}${only}。`;
  });

  async disable() {
    const confirmed = await this.confirm().ask({
      title: '停用本地登入',
      message: '停用後本地密碼會被清除，之後只能用 AD 登入；其他裝置上的登入會登出。',
      confirm: '停用本地登入',
      danger: true,
    });
    if (confirmed) this.store.disableLocalLogin();
  }

  /** 舊伺服器不回傳 `method`；Windows 模式只有 Windows 身分，其餘預設為 AD。 */
  readonly current = computed(() => {
    const login = this.session.auth.session();
    const method = login?.method ?? (login?.mode === 'Windows' ? 'windows' : 'ad');
    return isLoginMethod(method)
      ? LOGIN_METHODS[method]
      : { label: method, description: '由伺服器提供的登入方式。' };
  });
}
