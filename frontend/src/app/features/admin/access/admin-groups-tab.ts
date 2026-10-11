import { ChangeDetectionStrategy, Component, computed, inject, output } from '@angular/core';
import type { AdminGroupDto } from '../../../core/api/schema';
import { FeatureSummary } from '../feature-summary';
import { Icon } from '../../../shared/ui/icon';
import { AdminStore } from '../admin-store';

/** Feature groups grant features, models and storage. */
@Component({
  selector: 'nx-admin-groups-tab',
  imports: [Icon, FeatureSummary],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './access-tabs.scss',
  template: `
    <div class="page-toolbar">
      <p class="form-note">
        功能與模型授權取各群組聯集；模型 token 額度取授權群組最高值，無上限優先。個人政策另行套用。
      </p>
      <button class="primary-button" (click)="edit.emit(undefined)">
        <nx-icon name="plus" />新增群組
      </button>
    </div>
    <div class="resource-list">
      @for (group of store.catalog()?.groups ?? []; track group.id) {
        <div class="resource-row">
          <button class="resource-summary" (click)="edit.emit(group)">
            <strong>{{ group.name }}</strong
            ><small
              >{{ group.id }} · {{ group.enabled ? '啟用' : '停用' }} ·
              {{
                group.policy?.allowedModelIds == null
                  ? '全部模型'
                  : '授予 ' + group.policy.allowedModelIds.length + ' 個模型'
              }}</small
            >
          </button>
          <nx-feature-summary [features]="summaries()[group.id] || []" />
          <button
            class="icon-button"
            [attr.aria-label]="'編輯群組：' + group.name"
            (click)="edit.emit(group)"
          >
            <nx-icon name="edit" />
          </button>
        </div>
      }
    </div>
  `,
})
export class AdminGroupsTab {
  readonly store = inject(AdminStore);
  readonly edit = output<AdminGroupDto | undefined>();
  readonly summaries = computed(() => {
    const catalog = this.store.catalog();
    return Object.fromEntries(
      (catalog?.groups || []).map((group) => [
        group.id,
        (catalog?.features || []).filter((feature) => group.featureIds.includes(feature.id)),
      ]),
    );
  });
}
