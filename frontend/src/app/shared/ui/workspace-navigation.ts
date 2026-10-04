import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { WorkspaceSession } from '../../core/auth/workspace-session';
import { Icon } from './icon';

@Component({
  selector: 'nx-workspace-navigation',
  imports: [RouterLink, RouterLinkActive, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<nav class="workspace-navigation" aria-label="工作台功能" [class.compact]="compact()">
    @for (feature of session.me()?.access?.features || []; track feature.id) {
      <a
        [routerLink]="feature.route"
        routerLinkActive="current"
        [routerLinkActiveOptions]="{ exact: false }"
        ariaCurrentWhenActive="page"
        [attr.title]="feature.name"
        [attr.aria-label]="feature.name"
      >
        <nx-icon [name]="icons[feature.id] || 'document'" /><span>{{
          compact() ? shortNames[feature.id] || feature.name : feature.name
        }}</span>
      </a>
    }
    <a
      routerLink="/settings"
      routerLinkActive="current"
      ariaCurrentWhenActive="page"
      aria-label="個人設定"
      title="個人設定"
      ><nx-icon name="sliders" /><span>{{ compact() ? '設定' : '個人設定' }}</span></a
    >
  </nav>`,
})
export class WorkspaceNavigation {
  readonly session = inject(WorkspaceSession);
  readonly compact = input(false);
  readonly shortNames: Record<string, string> = {
    chat: '對話',
    projects: '專案',
    knowledge: '知識',
    artifacts: '成果',
    tasks: '任務',
    quality: '評測',
    admin: '管理',
    integrations: '來源',
    shared: '分享',
  };
  readonly icons: Record<string, string> = {
    chat: 'lines',
    projects: 'archive',
    knowledge: 'library',
    artifacts: 'document',
    tasks: 'repeat',
    quality: 'check',
    admin: 'lock',
    integrations: 'command',
    shared: 'copy',
  };
}
