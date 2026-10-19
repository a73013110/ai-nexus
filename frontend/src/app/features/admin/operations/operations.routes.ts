import type { Routes } from '@angular/router';
import { catalogRoute } from '@core/auth/feature-guard';

export const operationsRoutes: Routes = [
  catalogRoute('operations.usage', {
    path: 'usage',
    loadComponent: () => import('./usage-page').then((module) => module.UsagePage),
  }),
];
