import { ChangeDetectionStrategy, Component, computed, inject, output } from '@angular/core';
import type { AdminGroupDto } from '../../../core/api/schema';
import { DataTable, DataTableRow } from '../../../shared/ui/data-table';
import { Icon } from '../../../shared/ui/icon';
import { AdminStore } from '../admin-store';
import { FeatureSummary } from '../feature-summary';
import { AccessStatus } from './access-status';

/** Feature groups grant features, models and storage. */
@Component({
  selector: 'nx-admin-groups-tab',
  imports: [Icon, DataTable, DataTableRow, AccessStatus, FeatureSummary],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './access-tabs.scss',
  template: `
    <div class="page-toolbar">
      <p class="form-note">
        功能與模型授權取各群組聯集；模型 token 額度取授權群組最高值，無上限優先。個人政策另行套用。
      </p>
      <button class="primary-button" (click)="edit.emit(undefined)">
        <nx-icon name="plus" />新增功能群組
      </button>
    </div>
    <nx-data-table
      class="admin-access-table admin-groups-table"
      label="功能群組列表"
      [stacked]="true"
    >
      <div table-toolbar class="ui-table-toolbar">
        <h2>
          功能群組 <span class="ui-count-label">{{ groups().length }} 個</span>
        </h2>
        <span class="form-note">選取群組編輯功能、模型與容量</span>
      </div>
      <table>
        <colgroup>
          <col class="admin-name-column" />
          <col class="admin-status-column" />
          <col class="admin-model-column" />
          <col />
          <col class="admin-row-action-column" />
        </colgroup>
        <thead>
          <tr>
            <th scope="col">功能群組</th>
            <th scope="col">狀態</th>
            <th scope="col">AI 模型</th>
            <th scope="col">功能授權</th>
            <th scope="col"><span class="sr-only">編輯</span></th>
          </tr>
        </thead>
        <tbody>
          @for (group of groups(); track group.id) {
            <tr nxTableRow (rowActivate)="edit.emit(group)">
              <td>
                <strong>{{ group.name }}</strong
                ><small class="ui-truncate" [title]="group.id">{{ group.id }}</small>
              </td>
              <td><nx-access-status [enabled]="group.enabled" /></td>
              <td data-label="AI 模型">
                {{
                  group.policy?.allowedModelIds == null
                    ? '全部模型'
                    : '授予 ' + group.policy.allowedModelIds.length + ' 個模型'
                }}
              </td>
              <td data-label="功能授權">
                <nx-feature-summary [features]="summaries()[group.id] || []" />
              </td>
              <td data-stack="end">
                <button
                  class="icon-button"
                  data-row-action
                  [attr.aria-label]="'編輯功能群組：' + group.name"
                  (click)="edit.emit(group)"
                >
                  <nx-icon name="edit" />
                </button>
              </td>
            </tr>
          }
        </tbody>
      </table>
    </nx-data-table>
  `,
})
export class AdminGroupsTab {
  readonly store = inject(AdminStore);
  readonly edit = output<AdminGroupDto | undefined>();
  readonly groups = computed(() => this.store.catalog()?.groups ?? []);
  readonly summaries = computed(() => {
    const features = this.store.catalog()?.features || [];
    return Object.fromEntries(
      this.groups().map((group) => [
        group.id,
        features.filter((feature) => group.featureIds.includes(feature.id)),
      ]),
    );
  });
}
