import type { Routes } from '@angular/router';
import { catalogRoute } from '@core/auth/feature-guard';

/** 出勤四頁；網址由功能目錄推得，管理頁（卡號、匯入、假日）在選單放「系統管理 → 人事與出勤」。 */
export const attendanceRoutes: Routes = [
  catalogRoute('attendance.records', {
    path: 'records',
    loadComponent: () =>
      import('./records/attendance-records-page').then((module) => module.AttendanceRecordsPage),
  }),
  catalogRoute('attendance.cards', {
    path: 'cards',
    loadComponent: () =>
      import('./cards/attendance-cards-page').then((module) => module.AttendanceCardsPage),
  }),
  catalogRoute('attendance.imports', {
    path: 'imports',
    loadComponent: () =>
      import('./imports/attendance-imports-page').then((module) => module.AttendanceImportsPage),
  }),
  catalogRoute('attendance.calendar', {
    path: 'calendar',
    loadComponent: () =>
      import('./calendar/attendance-calendar-page').then((module) => module.AttendanceCalendarPage),
  }),
];
