import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import type { FeatureDto } from '../../core/api/schema';
import { groupFeatures } from '../../core/layout/feature-groups';

@Component({
  selector: 'nx-feature-summary',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'feature-summary' },
  template: `@for (group of groups(); track group.id) {
      <div class="feature-summary-group" [attr.aria-label]="group.name">
        <span class="feature-category-label">{{ group.name }}</span>
        <div class="resource-badges">
          @for (feature of group.features; track feature.id) {
            <span>{{ feature.name }}</span>
          }
        </div>
      </div>
    } @empty {
      <span class="form-note">尚無功能授權</span>
    }`,
})
export class FeatureSummary {
  readonly features = input.required<readonly FeatureDto[]>();
  readonly groups = computed(() => groupFeatures(this.features()));
}
