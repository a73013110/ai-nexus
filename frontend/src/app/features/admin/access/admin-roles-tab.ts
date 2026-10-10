import { ChangeDetectionStrategy, Component, inject, output } from '@angular/core';
import type { AdminRoleDto } from '../../../core/api/schema';
import { Icon } from '../../../shared/ui/icon';
import { AdminStore } from '../admin-store';

/** Roles bundle feature groups; users receive roles. */
@Component({
  selector: 'nx-admin-roles-tab',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './access-tabs.scss',
  template: `
    <div class="page-toolbar">
      <p class="form-note">使用者取得角色，角色加入功能群組。</p>
      <button class="primary-button" (click)="edit.emit(undefined)">
        <nx-icon name="plus" />新增角色
      </button>
    </div>
    <div class="resource-list">
      @for (role of store.catalog()?.roles ?? []; track role.id) {
        <div class="resource-row">
          <button class="resource-summary" (click)="edit.emit(role)">
            <strong>{{ role.name }}</strong
            ><small
              >{{ role.id }} · {{ role.userCount }} 位使用者 ·
              {{ role.enabled ? '啟用' : '停用' }}</small
            >
          </button>
          <div class="resource-badges">
            @for (group of store.groupNames(role.groupIds); track group) {
              <span>{{ group }}</span>
            }
          </div>
          <button
            class="icon-button"
            [attr.aria-label]="'編輯角色：' + role.name"
            (click)="edit.emit(role)"
          >
            <nx-icon name="edit" />
          </button>
        </div>
      }
    </div>
  `,
})
export class AdminRolesTab {
  readonly store = inject(AdminStore);
  readonly edit = output<AdminRoleDto | undefined>();
}
