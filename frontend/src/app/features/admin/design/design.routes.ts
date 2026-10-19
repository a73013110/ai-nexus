import type { Routes } from '@angular/router';
import { catalogRoute } from '@core/auth/feature-guard';

export const designRoutes: Routes = [
  catalogRoute('design', {
    path: '',
    loadComponent: () => import('./design-page').then((module) => module.DesignPage),
  }),
];
