import type { Routes } from '@angular/router';
import { moduleRoute, unavailableRoute } from '@core/layout/module-route';

/**
 * 員工服務區的模組；網址第二段是模組 ID（`FeatureCatalog`），各模組的頁面在自己的 `<module>.routes.ts`。
 * 最後的 `**` 接住不存在的模組。
 */
export const staffRoutes: Routes = [
  moduleRoute('attendance', () =>
    import('@features/staff/attendance/attendance.routes').then(
      (module) => module.attendanceRoutes,
    ),
  ),
  unavailableRoute,
];
