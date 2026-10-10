import { ChangeDetectionStrategy, Component, computed, inject, output } from '@angular/core';
import type { AdminFeatureDto } from '../../../core/api/schema';
import { FEATURE_ICONS, groupFeatures } from '../../../core/layout/feature-groups';
import { DataTable, DataTableRow } from '../../../shared/ui/data-table';
import { StatusBadge } from '../../../shared/ui/status-badge';
import { Icon } from '../../../shared/ui/icon';
import { AdminStore } from '../admin-store';

/** Features are registered by modules; only their name, order and switch are editable. */
@Component({
  selector: 'nx-admin-features-tab',
  imports: [Icon, DataTable, DataTableRow, StatusBadge],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './access-tabs.scss',
  template: `
    <p class="form-note">
      功能由模組註冊，路由與 API 授權由程式定義。可調整顯示名稱、順序或停用功能。
    </p>
    <nx-data-table class="admin-features-table" label="功能配置列表">
      <div table-toolbar class="ui-table-toolbar">
        <h2>
          功能配置 <span class="ui-count-label">{{ features().length }} 項</span>
        </h2>
        <span class="form-note">選取功能進行編輯</span>
      </div>
      <table>
        <colgroup>
          <col />
          <col />
          <col class="admin-auth-column" />
          <col class="admin-order-column" />
          <col class="admin-row-action-column" />
        </colgroup>
        <thead>
          <tr>
            <th scope="col">功能</th>
            <th scope="col">路由</th>
            <th scope="col">狀態</th>
            <th scope="col">順序</th>
            <th scope="col"><span class="sr-only">編輯</span></th>
          </tr>
        </thead>
        @for (section of sections(); track section.id) {
          <tbody>
            <tr class="admin-feature-section">
              <th scope="rowgroup" colspan="5">
                {{ section.name }}
                <span class="ui-count-label">{{ section.features.length }} 項</span>
              </th>
            </tr>
            @for (feature of section.features; track feature.id) {
              <tr nxTableRow (rowActivate)="edit.emit(feature)">
                <td>
                  <span class="admin-table-name"
                    ><nx-icon [name]="featureIcons[feature.id] || 'document'" /><strong>{{
                      feature.name
                    }}</strong></span
                  >
                </td>
                <td>
                  <span class="ui-truncate" [title]="feature.route">{{ feature.route }}</span>
                </td>
                <td>
                  <nx-status-badge [tone]="feature.enabled ? 'neutral' : 'warning'">{{
                    feature.enabled ? '啟用' : '停用'
                  }}</nx-status-badge>
                </td>
                <td>{{ feature.sortOrder }}</td>
                <td>
                  <button
                    class="icon-button"
                    data-row-action
                    [attr.aria-label]="'編輯功能：' + feature.name"
                    (click)="edit.emit(feature)"
                  >
                    <nx-icon name="edit" />
                  </button>
                </td>
              </tr>
            }
          </tbody>
        }
      </table>
    </nx-data-table>
  `,
})
export class AdminFeaturesTab {
  readonly store = inject(AdminStore);
  readonly edit = output<AdminFeatureDto | undefined>();
  readonly featureIcons = FEATURE_ICONS;
  readonly features = computed(() => this.store.catalog()?.features ?? []);
  readonly sections = computed(() => groupFeatures(this.features()));
}
