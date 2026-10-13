import { Routes } from '@angular/router';
import { authenticated } from './core/auth/auth-guard';
import { optionalId } from './core/layout/optional-id';
import { repositoryRoute } from './features/repositories/repository-route';
import { pendingChanges } from './shared/browser/pending-changes';
import { WORKSPACE_HOME } from './core/layout/workspace-home';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () =>
      import('./features/identity/login-page').then((module) => module.LoginPage),
  },
  { path: '', pathMatch: 'full', redirectTo: WORKSPACE_HOME },
  {
    // Every workspace page requires a signed-in session; the guard is declared once here, and the
    // shell keeps the sidebar alive across pages.
    path: '',
    canActivateChild: [authenticated],
    loadComponent: () =>
      import('./core/layout/workspace-shell').then((module) => module.WorkspaceShell),
    children: [
      {
        path: 'files',
        loadComponent: () =>
          import('./features/files/files-page').then((module) => module.FilesPage),
      },
      {
        path: 'dashboard',
        loadComponent: () =>
          import('./features/dashboard/dashboard-page').then((m) => m.DashboardPage),
      },
      {
        matcher: repositoryRoute,
        loadComponent: () =>
          import('./features/repositories/repositories-page').then((m) => m.RepositoriesPage),
      },
      {
        path: 'design',
        loadComponent: () => import('./features/design/design-page').then((m) => m.DesignPage),
      },
      {
        path: 'integrations',
        loadComponent: () =>
          import('./features/integrations/integrations-page').then((m) => m.IntegrationsPage),
      },
      {
        path: 'quality/retrieval',
        loadComponent: () =>
          import('./features/quality/retrieval-evaluation').then((m) => m.RetrievalEvaluation),
      },
      {
        matcher: optionalId('quality'),
        loadComponent: () => import('./features/quality/quality-page').then((m) => m.QualityPage),
      },
      {
        matcher: optionalId('shared'),
        loadComponent: () => import('./features/sharing/shared-page').then((m) => m.SharedPage),
      },
      {
        matcher: optionalId('projects'),
        loadComponent: () =>
          import('./features/projects/projects-page').then((m) => m.ProjectsPage),
      },
      {
        matcher: optionalId('artifacts'),
        canDeactivate: [pendingChanges],
        loadComponent: () =>
          import('./features/artifacts/artifacts-page').then((module) => module.ArtifactsPage),
      },
      {
        matcher: optionalId('knowledge'),
        loadComponent: () =>
          import('./features/knowledge/knowledge-page').then((module) => module.KnowledgePage),
      },
      {
        path: 'tasks',
        loadComponent: () =>
          import('./features/tasks/tasks-page').then((module) => module.TasksPage),
      },
      {
        path: 'reader/share/:shareId/:id',
        loadComponent: () =>
          import('./features/knowledge/document-reader').then((module) => module.DocumentReader),
      },
      {
        path: 'reader/attachment/:id',
        loadComponent: () =>
          import('./features/knowledge/document-reader').then((module) => module.DocumentReader),
      },
      {
        path: 'reader/:id',
        loadComponent: () =>
          import('./features/knowledge/document-reader').then((module) => module.DocumentReader),
      },
      {
        path: 'admin/operations',
        loadComponent: () =>
          import('./features/admin/operations/operations-page').then((m) => m.OperationsPage),
      },
      {
        path: 'admin/monitoring',
        loadComponent: () =>
          import('./features/admin/monitoring/monitoring-page').then((m) => m.MonitoringPage),
      },
      {
        path: 'admin/logs',
        loadComponent: () =>
          import('./features/admin/logs/system-logs-page').then((m) => m.SystemLogsPage),
      },
      {
        path: 'admin/audit',
        loadComponent: () =>
          import('./features/audit/activity-audit-page').then((m) => m.ActivityAuditPage),
      },
      {
        path: 'admin',
        loadComponent: () =>
          import('./features/admin/admin-page').then((module) => module.AdminPage),
      },
      {
        path: 'settings',
        loadComponent: () =>
          import('./features/settings/settings-entry').then((module) => module.SettingsEntry),
      },
      {
        // One route for /chat and /chat/:id, so starting a conversation keeps the same workspace.
        matcher: optionalId('chat'),
        loadComponent: () =>
          import('./features/chat/chat-workspace').then((module) => module.ChatWorkspace),
      },
    ],
  },
  { path: '**', redirectTo: WORKSPACE_HOME },
];
