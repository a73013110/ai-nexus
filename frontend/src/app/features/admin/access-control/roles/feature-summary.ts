import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { featureSections } from '@core/api/catalog';
import type { NavigationSectionDto } from '@core/api/schema';

/**
 * 依側欄的位置列出功能名稱（一個分群一列，分群路徑在左欄對齊），組內依選單順序；
 * 分組規則見 `featureSections`。
 */
@Component({
  selector: 'nx-feature-summary',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './feature-summary.scss',
  host: { class: 'feature-summary' },
  template: `@for (section of sections(); track section.id) {
      <div class="feature-summary-group" role="group" [attr.aria-label]="section.name">
        <span class="feature-category-label" aria-hidden="true">{{ section.name }}</span>
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
  readonly features = input.required<readonly { id: string; name: string }[]>();
  /** 功能在完整選單上的位置：管理或稽核目錄的 `navigationSections`。 */
  readonly navigation = input.required<readonly NavigationSectionDto[]>();
  readonly sections = computed(() => featureSections(this.features(), this.navigation()));
}
