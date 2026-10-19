import type { Routes } from '@angular/router';
import { catalogRoute } from '@core/auth/feature-guard';

export const organizationRoutes: Routes = [
  catalogRoute('organization', {
    path: '',
    loadComponent: () => import('./organization-page').then((module) => module.OrganizationPage),
  }),
];
