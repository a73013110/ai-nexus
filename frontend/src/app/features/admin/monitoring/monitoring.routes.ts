import type { Routes } from '@angular/router';
import { catalogRoute } from '@core/auth/feature-guard';

export const monitoringRoutes: Routes = [
  catalogRoute('monitoring', {
    path: '',
    loadComponent: () => import('./monitoring-page').then((module) => module.MonitoringPage),
  }),
];
