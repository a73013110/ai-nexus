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
        [attr.title]="feature.name"
      >
        <nx-icon [name]="icons[feature.id] || 'document'" /><span>{{ feature.name }}</span>
      </a>
    }
    <a routerLink="/settings" routerLinkActive="current"
      ><nx-icon name="sliders" /><span>個人設定</span></a
    >
  </nav>`,
})
export class WorkspaceNavigation {
  readonly session = inject(WorkspaceSession);
  readonly compact = input(false);
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
