import { Notice } from '../../shared/ui/notice';
import { ViewSwitch } from '../../shared/ui/view-switch';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FeaturePage } from '../../core/layout/feature-page';
import { ActivatedRoute, Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { WorkspaceSession } from '../../core/auth/workspace-session';
import type { AdminUserDto } from '../../core/api/schema';
import { ViewScope } from '../../shared/browser/view-scope';
import { ActionMenu, type MenuAction } from '../../shared/ui/action-menu';
import { AdminStore } from './admin-store';
import { AdminUserInspector } from './admin-user-inspector';
import { AccessEditorDialog } from './access/access-editor-dialog';
import { AdminUsersTab } from './access/admin-users-tab';
import { AdminRolesTab } from './access/admin-roles-tab';
import { AdminGroupsTab } from './access/admin-groups-tab';
import { AdminFeaturesTab } from './access/admin-features-tab';

/** Access management: users, roles, feature groups and features over one shared catalog. */
@Component({
  selector: 'nx-admin-page',
  imports: [
    Notice,
    ViewSwitch,
    FeaturePage,
    ActionMenu,
    AdminUserInspector,
    AccessEditorDialog,
    AdminUsersTab,
    AdminRolesTab,
    AdminGroupsTab,
    AdminFeaturesTab,
  ],
  providers: [AdminStore, ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './admin-page.html',
})
export class AdminPage {
  readonly session = inject(WorkspaceSession);
  readonly store = inject(AdminStore);
  private readonly router = inject(Router);
  readonly inspected = signal<AdminUserDto | null>(null);
  readonly tab = signal('users');
  readonly tabs = [
    { value: 'users', label: '使用者' },
    { value: 'roles', label: '角色' },
    { value: 'groups', label: '功能群組與模型' },
    { value: 'features', label: '功能' },
  ];
  /** Page tools beyond access management itself; the component workbench shares its grant. */
  readonly pageTools = computed<MenuAction[]>(() =>
    this.session.has('admin') ? [{ id: 'design', label: '介面元件', icon: 'sliders' }] : [],
  );
  pageAction(action: string) {
    if (action === 'design') void this.router.navigateByUrl('/design');
  }
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
