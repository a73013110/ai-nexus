import type { Routes } from '@angular/router';
import { catalogRoute } from '@core/auth/feature-guard';

export const mailRoutes: Routes = [
  catalogRoute('mail.messages', {
    path: 'messages',
    loadComponent: () =>
      import('./messages/mail-messages-page').then((module) => module.MailMessagesPage),
  }),
];
