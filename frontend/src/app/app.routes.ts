import { Routes } from '@angular/router';
import { authenticated } from './core/auth/auth-guard';
import { pendingChanges } from './shared/browser/pending-changes';
import { WORKSPACE_HOME } from './core/workspace-home';
import { redirectLegacyAdminAudit } from './core/legacy-admin-route';

export const routes: Routes = [
  {
    path: 'files',
    canActivate: [authenticated],
    loadComponent: () => import('./features/files/files-page').then((module) => module.FilesPage),
  },
  {
    path: 'dashboard',
    canActivate: [authenticated],
    loadComponent: () => import('./features/dashboard/dashboard-page').then((m) => m.DashboardPage),
  },
  {
    path: 'repositories',
    canActivate: [authenticated],
    loadComponent: () =>
      import('./features/repositories/repositories-page').then((m) => m.RepositoriesPage),
  },
  {
    path: 'design',
    canActivate: [authenticated],
    loadComponent: () => import('./features/design/design-page').then((m) => m.DesignPage),
  },
  {
    path: 'integrations',
    canActivate: [authenticated],
    loadComponent: () =>
      import('./features/integrations/integrations-page').then((m) => m.IntegrationsPage),
  },
  {
    path: 'quality/:id',
    canActivate: [authenticated],
    loadComponent: () => import('./features/quality/quality-page').then((m) => m.QualityPage),
  },
  {
    path: 'quality',
    canActivate: [authenticated],
    loadComponent: () => import('./features/quality/quality-page').then((m) => m.QualityPage),
  },
  {
    path: 'shared/:id',
    canActivate: [authenticated],
    loadComponent: () => import('./features/sharing/shared-page').then((m) => m.SharedPage),
  },
  {
    path: 'shared',
    canActivate: [authenticated],
    loadComponent: () => import('./features/sharing/shared-page').then((m) => m.SharedPage),
  },
  {
    path: 'projects/:id',
    canActivate: [authenticated],
    loadComponent: () => import('./features/projects/projects-page').then((m) => m.ProjectsPage),
  },
  {
    path: 'projects',
    canActivate: [authenticated],
    loadComponent: () => import('./features/projects/projects-page').then((m) => m.ProjectsPage),
  },
  {
    path: 'artifacts/:id',
    canActivate: [authenticated],
    canDeactivate: [pendingChanges],
    loadComponent: () =>
      import('./features/artifacts/artifacts-page').then((module) => module.ArtifactsPage),
  },
  {
    path: 'artifacts',
    canActivate: [authenticated],
    canDeactivate: [pendingChanges],
    loadComponent: () =>
      import('./features/artifacts/artifacts-page').then((module) => module.ArtifactsPage),
  },
  {
    path: 'knowledge',
    canActivate: [authenticated],
    loadComponent: () =>
      import('./features/knowledge/knowledge-page').then((module) => module.KnowledgePage),
  },
  {
    path: 'tasks',
    canActivate: [authenticated],
    loadComponent: () => import('./features/tasks/tasks-page').then((module) => module.TasksPage),
  },
  {
    path: 'reader/share/:shareId/:id',
    canActivate: [authenticated],
    loadComponent: () =>
      import('./features/knowledge/document-reader').then((module) => module.DocumentReader),
  },
  {
    path: 'reader/attachment/:id',
    canActivate: [authenticated],
    loadComponent: () =>
      import('./features/knowledge/document-reader').then((module) => module.DocumentReader),
  },
  {
    path: 'reader/:id',
    canActivate: [authenticated],
    loadComponent: () =>
      import('./features/knowledge/document-reader').then((module) => module.DocumentReader),
  },
  {
    path: 'admin/monitoring',
    canActivate: [authenticated],
    loadComponent: () =>
      import('./features/admin/monitoring/monitoring-page').then((m) => m.MonitoringPage),
  },
  {
    path: 'admin/logs',
    canActivate: [authenticated],
    loadComponent: () =>
      import('./features/admin/logs/system-logs-page').then((m) => m.SystemLogsPage),
  },
  {
    path: 'admin/audit',
    canActivate: [authenticated],
    loadComponent: () =>
      import('./features/audit/activity-audit-page').then((m) => m.ActivityAuditPage),
  },
  {
    path: 'admin',
    canActivate: [authenticated, redirectLegacyAdminAudit],
    loadComponent: () => import('./features/admin/admin-page').then((module) => module.AdminPage),
  },
  {
    path: 'settings',
    canActivate: [authenticated],
    loadComponent: () =>
      import('./features/settings/settings-entry').then((module) => module.SettingsEntry),
  },
  {
    path: 'login',
    loadComponent: () =>
      import('./features/identity/login-page').then((module) => module.LoginPage),
  },
  {
    path: 'chat',
    canActivate: [authenticated],
    loadComponent: () =>
      import('./features/chat/chat-workspace').then((module) => module.ChatWorkspace),
  },
  {
    path: 'chat/:id',
    canActivate: [authenticated],
    loadComponent: () =>
      import('./features/chat/chat-workspace').then((module) => module.ChatWorkspace),
  },
  { path: '', pathMatch: 'full', redirectTo: WORKSPACE_HOME },
  { path: '**', redirectTo: WORKSPACE_HOME },
];
