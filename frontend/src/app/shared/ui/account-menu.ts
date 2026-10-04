import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { WorkspaceSession } from '../../core/auth/workspace-session';
import { SettingsOverlay } from '../../core/preferences/settings-overlay';
import { ActionMenu } from './action-menu';
import { Icon } from './icon';

@Component({
  selector: 'nx-account-menu',
  imports: [ActionMenu, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<nx-action-menu
      [profile]="true"
      label="登入者選單"
      [items]="items()"
      (action)="act($event)"
    >
      <span class="avatar">{{ session.me()?.displayName?.slice(0, 1) || 'N' }}</span>
      <span class="profile-name"
        ><strong>{{ session.me()?.displayName || '公司帳號' }}</strong
        ><small>{{ session.me()?.account }}</small></span
      >
      <nx-icon name="more" />
    </nx-action-menu>
    @if (error()) {
      <p class="form-note" role="alert">{{ error() }}</p>
    }`,
})
export class AccountMenu {
  readonly session = inject(WorkspaceSession);
  private readonly settings = inject(SettingsOverlay);
  readonly error = signal('');
  readonly items = computed(() => [
    { id: 'settings', label: '設定', icon: 'settings' },
    ...(this.session.auth.session()?.mode === 'Ldap'
      ? [{ id: 'logout', label: '登出工作台', icon: 'logout' }]
      : []),
  ]);
  async act(id: string) {
    if (id === 'settings') this.settings.open();
    if (id === 'logout') {
      try {
        await this.session.auth.logout();
      } catch {
        this.error.set('登出未完成，請再試一次。');
      }
    }
  }
}
