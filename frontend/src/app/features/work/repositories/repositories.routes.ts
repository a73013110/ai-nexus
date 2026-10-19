import type { Routes } from '@angular/router';
import { catalogRoute } from '@core/auth/feature-guard';
import { repositoryRoute } from './repository-route';

export const repositoriesRoutes: Routes = [
  catalogRoute('repositories', {
    matcher: repositoryRoute,
    loadComponent: () => import('./repositories-page').then((module) => module.RepositoriesPage),
  }),
];
