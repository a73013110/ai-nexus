import {
  ApplicationConfig,
  ErrorHandler,
  provideAppInitializer,
  inject,
  DestroyRef,
  provideZonelessChangeDetection,
} from '@angular/core';
import { ClientIssues } from './core/api/client-issues';
import { provideRouter } from '@angular/router';
import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    { provide: ErrorHandler, useExisting: ClientIssues },
    provideAppInitializer(() => {
      const issues = inject(ClientIssues);
      const error = (event: ErrorEvent) => void issues.report(event.error, 'exception');
      const rejection = (event: PromiseRejectionEvent) => void issues.report(event.reason, 'rejection');
      window.addEventListener('error', error); window.addEventListener('unhandledrejection', rejection);
      inject(DestroyRef).onDestroy(() => { window.removeEventListener('error', error); window.removeEventListener('unhandledrejection', rejection); });
    }),
    provideZonelessChangeDetection(),
    provideRouter(routes),
  ],
};
