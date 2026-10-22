import type { Routes } from '@angular/router';
import { catalogRoute } from '@core/auth/feature-guard';

const placeholder = () =>
  import('./attendance-placeholder-page').then((module) => module.AttendancePlaceholderPage);

/** 四個頁面目前都是佔位頁，之後各自換成真頁面。網址由功能目錄推得。 */
export const attendanceRoutes: Routes = [
  catalogRoute('attendance.records', { path: 'records', loadComponent: placeholder }),
  catalogRoute('attendance.cards', { path: 'cards', loadComponent: placeholder }),
  catalogRoute('attendance.imports', { path: 'imports', loadComponent: placeholder }),
  catalogRoute('attendance.calendar', { path: 'calendar', loadComponent: placeholder }),
];
