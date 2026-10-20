import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { isLoginMethod, LOGIN_METHODS } from '@features/admin/identity/login-methods';
import { SettingGroup, SettingRow } from '@shared/ui/setting-row';

/**
 * 帳號分頁的「登入方式」：目前這次工作階段用哪種方式登入。登出與切換登入方式在登入者選單，
 * 這裡不重複；本地密碼等自助設定之後加在這張卡片。
 */
@Component({
  selector: 'nx-account-sign-in',
  imports: [SettingGroup, SettingRow],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    .sign-in-method {
      color: var(--secondary);
      font-size: var(--text-caption);
      white-space: nowrap;
    }
  `,
  template: `<nx-setting-group heading="登入方式">
    <nx-setting-row label="目前的登入方式" [description]="current().description">
      <span class="sign-in-method">{{ current().label }}</span>
    </nx-setting-row>
    @if (session.testing()) {
      <nx-setting-row
        label="測試身分"
        description="你正以其他使用者的身分檢視工作區；要返回管理者請使用登入者選單。"
      />
    }
  </nx-setting-group>`,
})
export class AccountSignIn {
  readonly session = inject(WorkspaceSession);
  /** 舊伺服器不回傳 `method`；Windows 模式只有 Windows 身分，其餘預設為 AD。 */
  readonly current = computed(() => {
    const login = this.session.auth.session();
    const method = login?.method ?? (login?.mode === 'Windows' ? 'windows' : 'ad');
    return isLoginMethod(method)
      ? LOGIN_METHODS[method]
      : { label: method, description: '由伺服器提供的登入方式。' };
  });
}
