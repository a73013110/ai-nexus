import { Routes } from '@angular/router';
import { authenticated } from '@core/auth/auth-guard';
import { optionalId } from '@core/layout/optional-id';
import { repositoryRoute } from '@features/work/repositories/repository-route';
import { pendingChanges } from '@shared/browser/pending-changes';
import { WORKSPACE_HOME } from '@core/layout/workspace-home';

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
        path: 'files',
        loadComponent: () =>
          import('@features/work/files/files-page').then((module) => module.FilesPage),
      },
      {
        path: 'dashboard',
        loadComponent: () =>
          import('@features/work/dashboard/dashboard-page').then((match) => match.DashboardPage),
      },
      {
        matcher: repositoryRoute,
        loadComponent: () =>
          import('@features/work/repositories/repositories-page').then(
            (match2) => match2.RepositoriesPage,
          ),
      },
      {
        path: 'design',
        loadComponent: () =>
          import('@features/admin/design/design-page').then((match3) => match3.DesignPage),
      },
      {
        path: 'integrations',
        loadComponent: () =>
          import('@features/admin/integrations/integrations-page').then(
            (match4) => match4.IntegrationsPage,
          ),
      },
      {
        path: 'quality/retrieval',
        loadComponent: () =>
          import('@features/admin/quality/retrieval-evaluation').then(
            (match5) => match5.RetrievalEvaluation,
          ),
      },
      {
        matcher: optionalId('quality'),
        loadComponent: () =>
          import('@features/admin/quality/quality-page').then((match6) => match6.QualityPage),
      },
      {
        matcher: optionalId('shared'),
        loadComponent: () =>
          import('@features/work/sharing/shared-page').then((match7) => match7.SharedPage),
      },
      {
        matcher: optionalId('projects'),
        loadComponent: () =>
          import('@features/work/projects/projects-page').then((match8) => match8.ProjectsPage),
      },
      {
        matcher: optionalId('artifacts'),
        canDeactivate: [pendingChanges],
        loadComponent: () =>
          import('@features/work/artifacts/artifacts-page').then((module) => module.ArtifactsPage),
      },
      {
        matcher: optionalId('knowledge'),
        loadComponent: () =>
          import('@features/work/knowledge/knowledge-page').then((module) => module.KnowledgePage),
      },
      {
        path: 'tasks',
        loadComponent: () =>
          import('@features/work/jobs/jobs-page').then((module) => module.JobsPage),
      },
      {
        path: 'reader/share/:shareId/:id',
        loadComponent: () =>
          import('@features/work/knowledge/documents/document-reader').then(
            (module) => module.DocumentReader,
          ),
      },
      {
        path: 'reader/attachment/:id',
        loadComponent: () =>
          import('@features/work/knowledge/documents/document-reader').then(
            (module) => module.DocumentReader,
          ),
      },
      {
        path: 'reader/:id',
        loadComponent: () =>
          import('@features/work/knowledge/documents/document-reader').then(
            (module) => module.DocumentReader,
          ),
      },
      {
        path: 'admin/operations',
        loadComponent: () =>
          import('@features/admin/operations/operations-page').then(
            (match9) => match9.OperationsPage,
          ),
      },
      {
        path: 'admin/monitoring',
        loadComponent: () =>
          import('@features/admin/monitoring/monitoring-page').then(
            (match10) => match10.MonitoringPage,
          ),
      },
      {
        path: 'admin/logs',
        loadComponent: () =>
          import('@features/admin/diagnostics/system-logs-page').then(
            (match11) => match11.SystemLogsPage,
          ),
      },
      {
        path: 'admin/audit',
        loadComponent: () =>
          import('@features/admin/audit/activity-audit-page').then(
            (match12) => match12.ActivityAuditPage,
          ),
      },
      {
        path: 'admin',
        loadComponent: () =>
          import('@features/admin/access-control/administration-page').then(
            (module) => module.AdministrationPage,
          ),
      },
      {
        path: 'settings',
        loadComponent: () =>
          import('@features/platform/account/settings-entry').then(
            (module) => module.SettingsEntry,
          ),
      },
      {
        // /chat 與 /chat/:id 共用同一條路由，因此開始新對話時工作區不會改變。
        matcher: optionalId('chat'),
        loadComponent: () =>
          import('@features/work/chat/chat-workspace').then((module) => module.ChatWorkspace),
      },
    ],
  },
  { path: '**', redirectTo: WORKSPACE_HOME },
];
