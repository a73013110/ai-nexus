import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { WorkspaceNavigation } from './workspace-navigation';
import { WorkspaceBrand } from './workspace-brand';
import { AccountMenu } from './account-menu';

@Component({
  selector: 'nx-feature-page',
  imports: [WorkspaceNavigation, WorkspaceBrand, AccountMenu],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<a class="skip-link" href="#feature-content">跳到主要內容</a>
    <div class="feature-layout">
      <aside class="feature-sidebar">
        <nx-workspace-brand /><nx-workspace-navigation />
        <nx-account-menu />
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
  readonly title = input.required<string>();
  readonly description = input('');
  readonly eyebrow = input('AI NEXUS · 工作空間');
}
