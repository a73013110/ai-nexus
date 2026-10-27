import type { Routes } from '@angular/router';
import { catalogRoute } from '@core/auth/feature-guard';

/** 活動稽核與功能使用量；`usage` 要排在空路徑前。 */
export const auditRoutes: Routes = [
  catalogRoute('audit.usage', {
    path: 'usage',
    loadComponent: () => import('./feature-usage-page').then((module) => module.FeatureUsagePage),
  }),
  catalogRoute('audit', {
    path: '',
    loadComponent: () => import('./activity-audit-page').then((module) => module.ActivityAuditPage),
  }),
];
