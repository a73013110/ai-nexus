import type { Routes } from '@angular/router';
import { catalogRoute } from '@core/auth/feature-guard';

export const dashboardRoutes: Routes = [
  catalogRoute('dashboard', {
    path: '',
    loadComponent: () => import('./dashboard-page').then((module) => module.DashboardPage),
  }),
];
