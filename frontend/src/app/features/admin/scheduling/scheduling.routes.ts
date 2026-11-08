import type { Routes } from '@angular/router';
import { catalogRoute } from '@core/auth/feature-guard';

export const schedulingRoutes: Routes = [
  catalogRoute('scheduling', {
    path: '',
    loadComponent: () => import('./scheduling-page').then((module) => module.SchedulingPage),
  }),
];
