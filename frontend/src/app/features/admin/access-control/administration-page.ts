import { Notice } from '@shared/ui/notice';
import { ViewSwitch } from '@shared/ui/view-switch';
import { ViewMotion } from '@shared/ui/view-motion';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FeatureAccessNotice } from '@core/layout/feature-access-notice';
import { FeaturePage } from '@core/layout/feature-page';
import { ActivatedRoute, Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { WorkspaceSession } from '@core/auth/workspace-session';
import type { AdminUserDto } from '@core/api/schema';
import { ViewScope } from '@shared/browser/view-scope';
import { AdministrationStore } from './administration-store';
import { AdminUserInspector } from '@features/admin/identity/users/admin-user-inspector';
import { AccessEditorDialog } from './roles/access-editor-dialog';
import { AdminUsersTab } from '@features/admin/identity/users/admin-users-tab';
import { AdminRolesTab } from './roles/admin-roles-tab';
import { AdminGroupsTab } from './roles/admin-groups-tab';
import { AdminFeaturesTab } from './roles/admin-features-tab';

/** 權限管理：在同一個共用目錄上管理使用者、角色、功能群組與功能。 */
@Component({
  selector: 'nx-administration-page',
  imports: [
    Notice,
    ViewSwitch,
    ViewMotion,
    FeaturePage,
    FeatureAccessNotice,
    AdminUserInspector,
    AccessEditorDialog,
    AdminUsersTab,
    AdminRolesTab,
    AdminGroupsTab,
    AdminFeaturesTab,
  ],
  providers: [AdministrationStore, ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './administration-page.html',
})
export class AdministrationPage {
  readonly session = inject(WorkspaceSession);
  readonly store = inject(AdministrationStore);
  private readonly router = inject(Router);
  readonly inspected = signal<AdminUserDto | null>(null);
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
  readonly tab = signal('users');
  readonly tabs = [
    { value: 'users', label: '使用者' },
    { value: 'roles', label: '角色' },
    { value: 'groups', label: '功能群組與模型' },
    { value: 'features', label: '功能' },
  ];
  constructor() {
    inject(ActivatedRoute)
      .queryParamMap.pipe(takeUntilDestroyed())
      .subscribe((params) => {
        const tab = params.get('tab') || 'users';
        if (this.tabs.some((item) => item.value === tab)) this.tab.set(tab);
      });
  }
  selectTab(id: string) {
    this.tab.set(id);
    this.store.actionError.set('');
    void this.router.navigate([], {
      queryParams: { tab: id, category: null, traceId: null, search: null },
      queryParamsHandling: 'merge',
    });
  }
}
