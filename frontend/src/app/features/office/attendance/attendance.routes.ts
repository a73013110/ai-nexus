import type { Routes } from '@angular/router';
import { catalogRoute } from '@core/auth/feature-guard';

/** 出勤四頁；網址由功能目錄推得，選單四頁都放「公司事務」。 */
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
