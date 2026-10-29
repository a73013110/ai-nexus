import type { Routes } from '@angular/router';
import { catalogRoute } from '@core/auth/feature-guard';

export const releasesRoutes: Routes = [
  catalogRoute('releases', {
    path: '',
    loadComponent: () => import('./releases-page').then((module) => module.ReleasesPage),
  }),
];
