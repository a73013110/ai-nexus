import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { catalogSections } from '@core/api/catalog';

/**
 * 依功能目錄的區列出功能名稱（一區一列，區名在左欄對齊），依目錄宣告順序。授權與選單位置無關，
 * 所以不依側欄分群；不在目錄中的 ID（舊資料）放在最後一組沒有標題的清單。
 */
@Component({
  selector: 'nx-feature-summary',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './feature-summary.scss',
  host: { class: 'feature-summary' },
  template: `@for (section of sections(); track section.id) {
      <div class="feature-summary-group" role="group" [attr.aria-label]="section.name || '其他'">
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
  readonly sections = computed(() => catalogSections(this.features()));
}
