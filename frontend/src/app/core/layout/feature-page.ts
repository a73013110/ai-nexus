import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Icon } from '../../shared/ui/icon';
import { ProductTourButton } from '../../shared/ui/product-tour-button';
import type { ProductTourDefinition } from '../../shared/ui/product-tour';
import { WorkspaceMenuButton } from './workspace-menu-button';

@Component({
  selector: 'nx-feature-page',
  imports: [WorkspaceMenuButton, ProductTourButton, RouterLink, Icon],
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
          @if (back(); as link) {
            <a
              class="icon-button feature-back"
              [routerLink]="link.path"
              [queryParams]="link.query"
              [attr.aria-label]="link.label"
              [title]="link.label"
              ><nx-icon name="left"
            /></a>
          }
          <div class="feature-title">
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
  /** A detail page's way back to its collection, shown before the title. */
  readonly back = input<{
    path: string;
    label: string;
    query?: Record<string, string | boolean>;
  } | null>(null);
  readonly density = input<'comfortable' | 'compact'>('compact');
  readonly wide = input(true);
  readonly layoutMode = input<'flow' | 'data'>('flow');
  /** The page's guided tour; its trigger sits at the far right of the header. */
  readonly tour = input<ProductTourDefinition | null>(null);
}
