import { Notice } from '../../shared/ui/notice';
import { ViewSwitch } from '../../shared/ui/view-switch';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { FeaturePage } from '../../core/layout/feature-page';
import { ActivatedRoute, Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { WorkspaceSession } from '../../core/auth/workspace-session';
import type { AdminUserDto } from '../../core/api/schema';
import { ViewScope } from '../../shared/browser/view-scope';
import { ActionMenu, type MenuAction } from '../../shared/ui/action-menu';
import { PriceBook } from '../billing/price-book';
import { AdminStore } from './admin-store';
import { AdminUserInspector } from './admin-user-inspector';
import { RetrievalAdmin } from './retrieval-admin';
import { PlatformUsage } from './platform-usage';
import { AccessEditorDialog } from './access/access-editor-dialog';
import { AdminUsersTab } from './access/admin-users-tab';
import { AdminRolesTab } from './access/admin-roles-tab';
import { AdminGroupsTab } from './access/admin-groups-tab';
import { AdminFeaturesTab } from './access/admin-features-tab';

/** Shell of the management workspace: navigation, shared catalog and the editors. */
@Component({
  selector: 'nx-admin-page',
  imports: [
    Notice,
    ViewSwitch,
    FeaturePage,
    ActionMenu,
    PriceBook,
    AdminUserInspector,
    RetrievalAdmin,
    PlatformUsage,
    AccessEditorDialog,
    AdminUsersTab,
    AdminRolesTab,
    AdminGroupsTab,
    AdminFeaturesTab,
  ],
  providers: [AdminStore, ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './admin-page.scss',
  templateUrl: './admin-page.html',
})
export class AdminPage {
  readonly session = inject(WorkspaceSession);
  readonly store = inject(AdminStore);
  private readonly router = inject(Router);
  private readonly prices = viewChild.required(PriceBook);
  readonly inspected = signal<AdminUserDto | null>(null);
  readonly tab = signal('users');
  readonly tabs = [
    { id: 'users', name: '使用者' },
    { id: 'roles', name: '角色' },
    { id: 'groups', name: '功能群組與模型' },
    { id: 'features', name: '功能' },
    { id: 'usage', name: '平台用量' },
    { id: 'retrieval', name: '知識檢索' },
  ];
  readonly navigationGroups = [
    { label: '帳號與授權', ids: ['users', 'roles', 'groups', 'features'] },
    { label: '用量與檢索', ids: ['usage', 'retrieval'] },
  ].map((group) => ({
    label: group.label,
    options: this.tabs
      .filter((tab) => group.ids.includes(tab.id))
      .map((tab) => ({ value: tab.id, label: tab.name })),
  }));
  readonly managementTools = computed<MenuAction[]>(() => [
    ...(this.session.has('audit')
      ? [{ id: 'audit', label: this.session.featureName('audit'), icon: 'audit' }]
      : []),
    ...(this.session.has('monitoring')
      ? [{ id: 'monitoring', label: this.session.featureName('monitoring'), icon: 'activity' }]
      : []),
    ...(this.session.has('logs.query')
      ? [{ id: 'logs', label: this.session.featureName('logs.query'), icon: 'logs' }]
      : []),
    ...(this.session.has('admin')
      ? [
          { id: 'dashboard', label: '用量與費用總覽', icon: 'chart' },
          { id: 'prices', label: '模型與工具價格', icon: 'money' },
          { id: 'design', label: '介面元件', icon: 'sliders' },
        ]
      : []),
  ]);
  managementAction(action: string) {
    if (!this.managementTools().some((item) => item.id === action)) return;
    if (action === 'prices') this.prices().open();
    else
      void this.router.navigateByUrl(
        (
          {
            audit: '/admin/audit',
            monitoring: '/admin/monitoring',
            logs: '/admin/logs',
            dashboard: '/dashboard?scope=platform',
            design: '/design',
          } as Record<string, string>
        )[action],
      );
  }
  constructor() {
    inject(ActivatedRoute)
      .queryParamMap.pipe(takeUntilDestroyed())
      .subscribe((params) => {
        const tab = params.get('tab') || 'users';
        if (this.tabs.some((item) => item.id === tab)) this.tab.set(tab);
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
