import type { Routes } from '@angular/router';
import { catalogRoute } from '@core/auth/feature-guard';

export const jobsRoutes: Routes = [
  catalogRoute('jobs', {
    path: '',
    loadComponent: () => import('./jobs-page').then((module) => module.JobsPage),
  }),
];
