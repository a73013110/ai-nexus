import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { WorkspaceMenuButton } from './workspace-menu-button';

@Component({
  selector: 'nx-feature-page',
  imports: [WorkspaceMenuButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './feature-page.scss',
  host: {
    '[class.feature-layout-compact]': "density() === 'compact'",
    '[class.feature-layout-wide]': 'wide()',
    '[class.feature-layout-data]': "layoutMode() === 'data'",
  },
  template: `<a class="skip-link" href="#feature-content">跳到主要內容</a>
    <main class="feature-main" id="feature-content" tabindex="-1">
      <div class="feature-content" [class.ui-density-compact]="density() === 'compact'">
        <header class="feature-header">
          <nx-workspace-menu-button />
          <div>
            @if (eyebrow()) {
              <span class="panel-eyebrow">{{ eyebrow() }}</span>
            }
            <h1>{{ title() }}</h1>
            @if (description()) {
              <p>{{ description() }}</p>
            }
          </div>
          <ng-content select="[page-actions]" />
        </header>
        <ng-content />
      </div>
    </main>`,
})
export class FeaturePage {
  readonly title = input.required<string>();
  readonly description = input('');
  readonly eyebrow = input('AI NEXUS / 工作區');
  readonly density = input<'comfortable' | 'compact'>('compact');
  readonly wide = input(true);
  readonly layoutMode = input<'flow' | 'data'>('flow');
}
