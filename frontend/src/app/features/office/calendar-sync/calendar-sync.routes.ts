import type { Routes } from '@angular/router';
import { catalogRoute } from '@core/auth/feature-guard';

/** Google 日曆同步只有一頁；網址由功能目錄推得（`/office/calendar-sync`）。 */
export const calendarSyncRoutes: Routes = [
  catalogRoute('calendar-sync', {
    path: '',
    loadComponent: () => import('./calendar-sync-page').then((module) => module.CalendarSyncPage),
  }),
];
