import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { WorkspaceNavigation } from './workspace-navigation';
import { WorkspaceSession } from '../../core/auth/workspace-session';
import { Icon } from './icon';

@Component({
  selector: 'nx-feature-page',
  imports: [RouterLink, WorkspaceNavigation, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<a class="skip-link" href="#feature-content">跳到主要內容</a>
    <div class="feature-layout">
      <aside class="feature-sidebar">
        <a class="feature-brand" routerLink="/chat">AI <strong>Nexus</strong></a
        ><nx-workspace-navigation />
        <a class="feature-account" routerLink="/settings"
          ><span class="avatar">{{ session.me()?.displayName?.slice(0, 1) || 'N' }}</span
          ><span
            ><strong>{{ session.me()?.displayName }}</strong
            ><small>個人設定</small></span
          ><nx-icon name="sliders"
        /></a>
      </aside>
      <main class="feature-main" id="feature-content">
        <header class="feature-header">
          <div>
            <span class="panel-eyebrow">{{ eyebrow() }}</span>
            <h1>{{ title() }}</h1>
            <p>{{ description() }}</p>
          </div>
          <ng-content select="[page-actions]" />
        </header>
        <ng-content />
      </main>
    </div>`,
})
export class FeaturePage {
  readonly session = inject(WorkspaceSession);
  readonly title = input.required<string>();
  readonly description = input('');
  readonly eyebrow = input('AI NEXUS · 工作空間');
}
