import type { Routes } from '@angular/router';
import { moduleRoute, unavailableRoute } from '@core/layout/module-route';

/**
 * 系統管理區的模組；網址第二段是模組 ID（`FeatureCatalog`），各模組的頁面在自己的 `<module>.routes.ts`。
 * 最後的 `**` 接住不存在的模組；模組內被 `featureGuard` 擋下的頁面由 `moduleRoute` 補上的 `**` 接住。
 */
export const adminRoutes: Routes = [
  moduleRoute('identity', () =>
    import('@features/admin/identity/identity.routes').then((module) => module.identityRoutes),
  ),
  moduleRoute('access-control', () =>
    import('@features/admin/access-control/access-control.routes').then(
      (module) => module.accessControlRoutes,
    ),
  ),
  moduleRoute('organization', () =>
    import('@features/admin/organization/organization.routes').then(
      (module) => module.organizationRoutes,
    ),
  ),
  moduleRoute('operations', () =>
    import('@features/admin/operations/operations.routes').then(
      (module) => module.operationsRoutes,
    ),
  ),
  moduleRoute('billing', () =>
    import('@features/admin/billing/billing.routes').then((module) => module.billingRoutes),
  ),
  moduleRoute('monitoring', () =>
    import('@features/admin/monitoring/monitoring.routes').then(
      (module) => module.monitoringRoutes,
    ),
  ),
  moduleRoute('audit', () =>
    import('@features/admin/audit/audit.routes').then((module) => module.auditRoutes),
  ),
  moduleRoute('diagnostics', () =>
    import('@features/admin/diagnostics/diagnostics.routes').then(
      (module) => module.diagnosticsRoutes,
    ),
  ),
  moduleRoute('mail', () =>
    import('@features/admin/mail/mail.routes').then((module) => module.mailRoutes),
  ),
  moduleRoute('releases', () =>
    import('@features/admin/releases/releases.routes').then((module) => module.releasesRoutes),
  ),
  moduleRoute('quality', () =>
    import('@features/admin/quality/quality.routes').then((module) => module.qualityRoutes),
  ),
  moduleRoute('design', () =>
    import('@features/admin/design/design.routes').then((module) => module.designRoutes),
  ),
  unavailableRoute,
];
