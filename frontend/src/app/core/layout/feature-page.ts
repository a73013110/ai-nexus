import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { ProductTourButton } from '../../shared/ui/product-tour-button';
import type { ProductTourDefinition } from '../../shared/ui/product-tour';
import { WorkspaceMenuButton } from './workspace-menu-button';

@Component({
  selector: 'nx-feature-page',
  imports: [WorkspaceMenuButton, ProductTourButton],
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
          <div class="feature-title">
            @if (eyebrow()) {
              <span class="panel-eyebrow">{{ eyebrow() }}</span>
            }
            <h1>{{ title() }}</h1>
            @if (description()) {
              <p>{{ description() }}</p>
            }
          </div>
          <ng-content select="[page-actions]" />
          @if (tour(); as definition) {
            <!-- Every page keeps its tour at the far right of its header. -->
            <nx-product-tour-button class="feature-tour" [tour]="definition" [compact]="true" />
          }
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
  /** The page's guided tour; its trigger sits at the far right of the header. */
  readonly tour = input<ProductTourDefinition | null>(null);
}
