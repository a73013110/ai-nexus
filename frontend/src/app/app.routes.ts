import { Routes } from '@angular/router';
import { pendingChanges } from './shared/browser/pending-changes';

export const routes: Routes = [
  {
    path: 'quality/:id',
    loadComponent: () => import('./features/quality/quality-page').then((m) => m.QualityPage),
  },
  {
    path: 'quality',
    loadComponent: () => import('./features/quality/quality-page').then((m) => m.QualityPage),
  },
  {
    path: 'shared/:id',
    loadComponent: () => import('./features/sharing/shared-page').then((m) => m.SharedPage),
  },
  {
    path: 'shared',
    loadComponent: () => import('./features/sharing/shared-page').then((m) => m.SharedPage),
  },
  {
    path: 'projects/:id',
    loadComponent: () => import('./features/projects/projects-page').then((m) => m.ProjectsPage),
  },
  {
    path: 'projects',
    loadComponent: () => import('./features/projects/projects-page').then((m) => m.ProjectsPage),
  },
  {
    path: 'artifacts/:id',
    canDeactivate: [pendingChanges],
    loadComponent: () =>
      import('./features/artifacts/artifacts-page').then((module) => module.ArtifactsPage),
  },
  {
    path: 'artifacts',
    canDeactivate: [pendingChanges],
    loadComponent: () =>
      import('./features/artifacts/artifacts-page').then((module) => module.ArtifactsPage),
  },
  {
    path: 'knowledge',
    loadComponent: () =>
      import('./features/knowledge/knowledge-page').then((module) => module.KnowledgePage),
  },
  {
    path: 'tasks',
    loadComponent: () => import('./features/tasks/tasks-page').then((module) => module.TasksPage),
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
    path: 'admin',
    loadComponent: () => import('./features/admin/admin-page').then((module) => module.AdminPage),
  },
  {
    path: 'settings',
    loadComponent: () =>
      import('./features/settings/settings-page').then((module) => module.SettingsPage),
  },
  {
    path: 'login',
    loadComponent: () =>
      import('./features/identity/login-page').then((module) => module.LoginPage),
  },
  {
    path: 'chat',
    loadComponent: () =>
      import('./features/chat/chat-workspace').then((module) => module.ChatWorkspace),
  },
  {
    path: 'chat/:id',
    loadComponent: () =>
      import('./features/chat/chat-workspace').then((module) => module.ChatWorkspace),
  },
  { path: '', pathMatch: 'full', redirectTo: 'chat' },
  { path: '**', redirectTo: 'chat' },
];
