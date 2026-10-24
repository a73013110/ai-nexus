import { inject } from '@angular/core';
import { Router, Routes } from '@angular/router';
import { authenticated } from '@core/auth/auth-guard';
import { WORKSPACE_HOME } from '@core/layout/workspace-home';

/**
 * 根路由只放登入、個人設定、首頁轉址與各區（ADR 0016）。區路由是同層的 `<區>.routes.ts`（區資料夾只放模組），
 * 頁面路由在各模組的 `<module>.routes.ts`；網址與目錄的對應由 `app.routes.spec.ts` 雙向檢查。
 */
export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () =>
      import('@features/admin/identity/login-page').then((module) => module.LoginPage),
  },
  { path: '', pathMatch: 'full', redirectTo: WORKSPACE_HOME },
  {
    // 每個工作區頁面都需要已登入的工作階段；守衛在此宣告一次，
    // 殼層則讓側邊欄跨頁面保持存活。
    path: '',
    canActivateChild: [authenticated],
    loadComponent: () =>
      import('@core/layout/workspace-shell').then((module) => module.WorkspaceShell),
    children: [
      {
        path: 'work',
        loadChildren: () => import('./work.routes').then((module) => module.workRoutes),
      },
      {
        path: 'office',
        loadChildren: () => import('./office.routes').then((module) => module.officeRoutes),
      },
      {
        // 區 staff 已改名為 office（ADR 0016）；舊書籤與通知連結轉到同一頁，保留其餘路徑段與查詢參數。
        path: 'staff',
        children: [
          {
            path: '**',
            redirectTo: ({ url, queryParams }) =>
              inject(Router).createUrlTree(['/office', ...url.map((segment) => segment.path)], {
                queryParams,
              }),
          },
        ],
      },
      {
        path: 'admin',
        loadChildren: () => import('./admin.routes').then((module) => module.adminRoutes),
      },
      {
        path: 'settings',
        loadComponent: () =>
          import('@features/platform/account/settings-entry').then(
            (module) => module.SettingsEntry,
          ),
      },
      {
        path: '**',
        loadComponent: () =>
          import('@core/layout/unavailable-page').then((module) => module.UnavailablePage),
      },
    ],
  },
];
