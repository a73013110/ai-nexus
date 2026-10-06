import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { WorkspaceSidebar } from './workspace-sidebar';
import { WorkspaceLayout } from '../../core/preferences/workspace-layout';

@Component({
  selector: 'nx-feature-page',
  imports: [WorkspaceSidebar],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: ':host { display: block; min-width: 0; }',
  template: `<a class="skip-link" href="#feature-content">跳到主要內容</a>
    <div class="feature-layout">
      <aside nxWorkspaceSidebar class="feature-sidebar" aria-label="工作區導覽"></aside>
      <main
        class="feature-main"
        id="feature-content"
        tabindex="-1"
        [attr.inert]="layout.overlay() ? '' : null"
      >
        <div class="feature-content">
          <header class="feature-header">
            <div>
              <span class="panel-eyebrow">{{ eyebrow() }}</span>
              <h1>{{ title() }}</h1>
              <p>{{ description() }}</p>
            </div>
            <ng-content select="[page-actions]" />
          </header>
          <ng-content />
        </div>
      </main>
    </div>`,
})
export class FeaturePage {
  readonly layout = inject(WorkspaceLayout);
  readonly title = input.required<string>();
  readonly description = input('');
  readonly eyebrow = input('AI NEXUS · 工作區');
}
