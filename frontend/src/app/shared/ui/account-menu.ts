import { IssueCode } from './issue-code';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { WorkspaceSession } from '../../core/auth/workspace-session';
import { SettingsOverlay } from '../../core/preferences/settings-overlay';
import { ActionMenu } from './action-menu';
import { Icon } from './icon';

@Component({
  selector: 'nx-account-menu',
  imports: [IssueCode, ActionMenu, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'ui-density-compact' },
  template: `<nx-action-menu
      [profile]="true"
      label="登入者選單"
      [items]="items()"
      (action)="act($event)"
    >
      <span class="avatar">{{ session.me()?.displayName?.slice(0, 1) || 'N' }}</span>
      <span class="profile-name"
        ><strong>{{ session.me()?.displayName || '帳號' }}</strong
        ><small>{{ session.me()?.account }}</small></span
      >
      <nx-icon name="more" />
    </nx-action-menu>
    @if (error()) {
      <p class="form-note" role="alert">{{ error() }}<nx-issue-code [message]="error()" /></p>
    }`,
})
export class AccountMenu {
  readonly session = inject(WorkspaceSession);
  private readonly settings = inject(SettingsOverlay);
  readonly error = signal('');
  readonly items = computed(() => [
    { id: 'settings', label: '設定', icon: 'settings' },
    ...(this.session.auth.session()?.testing
      ? [{ id: 'restore', label: '返回管理者', icon: 'shield' }]
      : []),
    ...(!this.session.auth.session()?.testing &&
    (this.session.auth.session()?.methods?.length ?? 0) > 1
      ? [{ id: 'login', label: '切換登入方式', icon: 'lock' }]
      : []),
    ...(this.session.auth.session()?.mode === 'Ldap' ||
    this.session.auth.session()?.method === 'local' ||
    this.session.auth.session()?.testing
      ? [{ id: 'logout', label: '登出工作區', icon: 'logout' }]
      : []),
  ]);
  async act(id: string) {
    if (id === 'settings') this.settings.open();
    if (id === 'login') window.location.assign('/login?method=local');
    if (id === 'restore') {
      try {
        await this.session.auth.endTestIdentity();
      } catch {
        this.error.set('返回管理者未完成，請再試一次。');
      }
    }
    if (id === 'logout') {
      try {
        await this.session.auth.logout();
      } catch {
        this.error.set('登出未完成，請再試一次。');
      }
    }
  }
}
