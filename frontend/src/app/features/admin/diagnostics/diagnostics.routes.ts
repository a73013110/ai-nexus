import type { Routes } from '@angular/router';
import { catalogRoute } from '@core/auth/feature-guard';

export const diagnosticsRoutes: Routes = [
  catalogRoute('diagnostics.logs', {
    path: 'logs',
    loadComponent: () => import('./system-logs-page').then((module) => module.SystemLogsPage),
  }),
];
