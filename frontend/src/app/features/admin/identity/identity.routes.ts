import type { Routes } from '@angular/router';
import { catalogRoute } from '@core/auth/feature-guard';

export const identityRoutes: Routes = [
  catalogRoute('identity.users', {
    path: 'users',
    loadComponent: () => import('./users-page').then((module) => module.UsersPage),
  }),
  catalogRoute('identity.api-clients', {
    path: 'api-clients',
    loadComponent: () =>
      import('./api-clients/api-clients-page').then((module) => module.ApiClientsPage),
  }),
];
