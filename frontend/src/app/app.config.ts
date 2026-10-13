import {
  ApplicationConfig,
  ErrorHandler,
  provideAppInitializer,
  inject,
  DestroyRef,
  provideZonelessChangeDetection,
} from '@angular/core';
import { ClientIssues } from './core/errors/client-issues';
import { provideRouter } from '@angular/router';
import { routes } from './app.routes';
import { withNexusViewTransitions } from './core/layout/view-morph';

export const appConfig: ApplicationConfig = {
  providers: [
    { provide: ErrorHandler, useExisting: ClientIssues },
    provideAppInitializer(() => {
      const issues = inject(ClientIssues);
      // Extensions and cross-origin scripts are not ours to report: they carry no error object or another origin.
      const error = (event: ErrorEvent) => {
        if (
          !event.error ||
          (event.filename && new URL(event.filename, location.href).origin !== location.origin)
        )
          return;
        void issues.report(event.error, 'exception');
      };
      const rejection = (event: PromiseRejectionEvent) =>
        void issues.report(event.reason, 'rejection');
      window.addEventListener('error', error);
      window.addEventListener('unhandledrejection', rejection);
      inject(DestroyRef).onDestroy(() => {
        window.removeEventListener('error', error);
        window.removeEventListener('unhandledrejection', rejection);
      });
    }),
    provideZonelessChangeDetection(),
    provideRouter(routes, withNexusViewTransitions()),
  ],
};
