import type { Routes } from '@angular/router';
import { catalogRoute } from '@core/auth/feature-guard';

export const identityRoutes: Routes = [
  catalogRoute('identity.users', {
    path: 'users',
    loadComponent: () => import('./users-page').then((module) => module.UsersPage),
  }),
];
