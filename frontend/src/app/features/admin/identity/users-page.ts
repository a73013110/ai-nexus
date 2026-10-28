import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import type { AdminUserDto } from '@core/api/schema';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { FeaturePage } from '@core/layout/feature-page';
import { ViewScope } from '@shared/browser/view-scope';
import { Notice } from '@shared/ui/notice';
import { PendingButton } from '@shared/ui/pending-button';
import { AccessControlStore } from '@features/admin/access-control/access-control-store';
import { AdminUsersStore } from './admin-users-store';
import { usersTour } from './identity-tour';
import { AdminUserInspector } from './users/admin-user-inspector';
import { AdminUsersTab } from './users/admin-users-tab';

/** 使用者管理：公司身分、角色指派、個人政策與稽核式檢視；角色名稱與功能預覽取自存取目錄。 */
@Component({
  selector: 'nx-users-page',
  imports: [Notice, PendingButton, FeaturePage, AdminUserInspector, AdminUsersTab],
  providers: [AccessControlStore, AdminUsersStore, ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<nx-feature-page
      [title]="session.featureName('identity.users')"
      description="管理公司身分、角色指派與個人的 AI 使用政策。"
      layoutMode="data"
      [tour]="tour()"
    >
      <!-- 目錄與列表的首次載入都由表格的骨架表示；目錄讀取失敗時只留提示與重試。 -->
      @if (store.error()) {
        <nx-notice [message]="store.error()"
          ><button
            notice-actions
            nxPending
            type="button"
            [pending]="store.access.refreshing() || store.table.refreshing()"
            (click)="store.load()"
          >
            {{ store.access.refreshing() || store.table.refreshing() ? '重新載入中…' : '重新載入' }}
          </button>
        </nx-notice>
      }
      @if (store.access.catalog() || !store.access.error()) {
        <nx-admin-users-tab
          class="ui-table-fill"
          [selectedId]="inspected()?.id ?? null"
          (inspect)="inspected.set($event)"
        />
      }
    </nx-feature-page>
    <nx-admin-user-inspector
      [user]="inspected()"
      [index]="inspectedIndex()"
      [count]="pageUsers().length"
      (move)="inspectAdjacent($event)"
      (closed)="inspected.set(null)"
    />`,
})
export class UsersPage {
  readonly session = inject(WorkspaceSession);
  readonly store = inject(AdminUsersStore);
  readonly inspected = signal<AdminUserDto | null>(null);
  readonly tour = computed(() =>
    usersTour({
      directoryImport: this.session.has('identity.users.import'),
      directorySync: this.session.has('identity.users.sync'),
    }),
  );
  /** 使用者列表目前這一頁；管理對話框的上下筆只在這一頁內移動。 */
  readonly pageUsers = computed(() => this.store.users()?.users ?? []);
  readonly inspectedIndex = computed(() => {
    const id = this.inspected()?.id;
    return id ? this.pageUsers().findIndex((user) => user.id === id) : -1;
  });
  inspectAdjacent(direction: -1 | 1) {
    const next = this.pageUsers()[this.inspectedIndex() + direction];
    if (next) this.inspected.set(next);
  }
}
