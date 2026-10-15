import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import type { NavigationGroupDto } from '@core/api/schema';
import {
  navigationSections,
  navigationTree,
  type NavigationEntry,
} from '@core/layout/navigation-tree';

/**
 * Feature names in menu order, under the path of their menu group. Without `groups` every feature
 * sits in one unlabelled list.
 */
@Component({
  selector: 'nx-feature-summary',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './feature-summary.scss',
  host: { class: 'feature-summary' },
  template: `@for (section of sections(); track section.id) {
      <div class="feature-summary-group" [attr.aria-label]="section.path || null">
        @if (section.path) {
          <span class="feature-category-label">{{ section.path }}</span>
        }
        <div class="resource-badges">
          @for (feature of section.features; track feature.id) {
            <span>{{ feature.name }}</span>
          }
        </div>
      </div>
    } @empty {
      <span class="form-note">尚無功能授權</span>
    }`,
})
export class FeatureSummary {
  readonly features = input.required<readonly (NavigationEntry & { name: string })[]>();
  readonly groups = input<readonly NavigationGroupDto[]>([]);
  readonly sections = computed(() =>
    navigationSections(navigationTree(this.features(), this.groups())),
  );
}
