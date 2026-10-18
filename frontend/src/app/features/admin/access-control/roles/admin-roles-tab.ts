import { ChangeDetectionStrategy, Component, computed, inject, output } from '@angular/core';
import type { AdminRoleDto } from '@core/api/schema';
import { DataTable, DataTableRow } from '@shared/ui/data-table';
import { Icon } from '@shared/ui/icon';
import { AdministrationStore } from '../administration-store';
import { AccessStatus } from './access-status';

/** 角色組合功能群組；使用者則領取角色。 */
@Component({
  selector: 'nx-admin-roles-tab',
  imports: [Icon, DataTable, DataTableRow, AccessStatus],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: '../access-tabs.scss',
  template: `
    <div class="page-toolbar">
      <p class="form-note">使用者取得角色，角色加入功能群組。</p>
      <button class="primary-button" (click)="edit.emit(undefined)">
        <nx-icon name="plus" />新增角色
      </button>
    </div>
    <nx-data-table class="admin-access-table admin-roles-table" label="角色列表" [stacked]="true">
      <div table-toolbar class="ui-table-toolbar">
        <h2>
          角色 <span class="ui-count-label">{{ roles().length }} 個</span>
        </h2>
      </div>
      <table>
        <colgroup>
          <col class="admin-name-column" />
          <col class="admin-status-column" />
          <col class="admin-count-column" />
          <col />
        </colgroup>
        <thead>
          <tr>
            <th scope="col">角色</th>
            <th scope="col">狀態</th>
            <th scope="col" class="ui-table-numeric">使用者</th>
            <th scope="col">功能群組</th>
          </tr>
        </thead>
        <tbody>
          @for (role of roles(); track role.id) {
            <tr nxTableRow (rowActivate)="edit.emit(role)">
              <td>
                <button
                  type="button"
                  class="ui-table-link admin-table-name"
                  data-row-action
                  aria-haspopup="dialog"
                  [attr.aria-label]="'編輯角色：' + role.name"
                  (click)="edit.emit(role)"
                >
                  <span class="ui-truncate" [title]="role.name">{{ role.name }}</span></button
                ><small class="ui-truncate" [title]="role.id">{{ role.id }}</small>
              </td>
              <td><nx-access-status [enabled]="role.enabled" /></td>
              <td class="ui-table-numeric" data-label="使用者">{{ role.userCount }} 位</td>
              <td data-label="功能群組">
                @if (role.groupIds.length) {
                  <div class="resource-badges">
                    @for (group of store.groupNames(role.groupIds); track group) {
                      <span>{{ group }}</span>
                    }
                  </div>
                } @else {
                  <span class="form-note">尚未加入功能群組</span>
                }
              </td>
            </tr>
          }
        </tbody>
      </table>
    </nx-data-table>
  `,
})
export class AdminRolesTab {
  readonly store = inject(AdministrationStore);
  readonly edit = output<AdminRoleDto | undefined>();
  readonly roles = computed(() => this.store.catalog()?.roles ?? []);
}
