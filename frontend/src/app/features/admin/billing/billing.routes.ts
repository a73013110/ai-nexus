import type { Routes } from '@angular/router';
import { catalogRoute } from '@core/auth/feature-guard';

export const billingRoutes: Routes = [
  catalogRoute('billing.prices', {
    path: 'prices',
    loadComponent: () => import('./prices-page').then((module) => module.PricesPage),
  }),
];
