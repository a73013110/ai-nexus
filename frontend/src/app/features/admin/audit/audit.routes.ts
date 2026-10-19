import type { Routes } from '@angular/router';
import { catalogRoute } from '@core/auth/feature-guard';

export const auditRoutes: Routes = [
  catalogRoute('audit', {
    path: '',
    loadComponent: () => import('./activity-audit-page').then((module) => module.ActivityAuditPage),
  }),
];
