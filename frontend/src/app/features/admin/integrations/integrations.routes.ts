import type { Routes } from '@angular/router';
import { catalogRoute } from '@core/auth/feature-guard';

export const integrationsRoutes: Routes = [
  catalogRoute('integrations', {
    path: '',
    loadComponent: () => import('./integrations-page').then((module) => module.IntegrationsPage),
  }),
];
