import type { Routes } from '@angular/router';
import { catalogRoute } from '@core/auth/feature-guard';
import { repositoryRoute } from './repository-route';

/**
 * 儲存庫權限（`/work/repositories/access`）放在前面：只有一段網址，儲存庫的路由不接（`repositoryRoute`），
 * 其餘網址都由同一條儲存庫路由處理。
 */
export const repositoriesRoutes: Routes = [
  catalogRoute('repositories.access', {
    path: 'access',
    loadComponent: () =>
      import('./access/repository-access-page').then((module) => module.RepositoryAccessPage),
  }),
  catalogRoute('repositories', {
    matcher: repositoryRoute,
    loadComponent: () => import('./repositories-page').then((module) => module.RepositoriesPage),
  }),
];
