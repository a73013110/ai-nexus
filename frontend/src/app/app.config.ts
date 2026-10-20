import {
  ApplicationConfig,
  ErrorHandler,
  provideAppInitializer,
  inject,
  DestroyRef,
  provideZonelessChangeDetection,
} from '@angular/core';
import { ClientIssues } from '@core/errors/client-issues';
import { provideRouter } from '@angular/router';
import { routes } from './app.routes';
import { withMorphTransitions } from '@core/layout/view-morph';
import { provideNavigationBadges } from '@core/layout/navigation-badges';
import { NotificationStore } from '@features/platform/notifications/notification-store';

export const appConfig: ApplicationConfig = {
  providers: [
    { provide: ErrorHandler, useExisting: ClientIssues },
    provideAppInitializer(() => {
      const issues = inject(ClientIssues);
      // 擴充功能與跨來源的腳本不屬於我們要回報的範圍：它們不帶錯誤物件，或來自另一個來源。
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
    provideRouter(routes, withMorphTransitions()),
    // 側欄入口的徽章；新來源在這裡加一行（見 NavigationBadges）。
    provideNavigationBadges(() => inject(NotificationStore).featureBadges),
  ],
};
