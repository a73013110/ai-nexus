import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  effect,
  inject,
  Injector,
} from '@angular/core';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { UnhandledIssue } from '@core/errors/unhandled-issue';
import { RouterOutlet } from '@angular/router';
import { SettingsDialog } from '@features/platform/account/settings-dialog';
import { TestIdentityIndicator } from '@core/auth/test-identity-indicator';
import { IdentityVeil } from '@core/auth/identity-veil';
import { ReaderDialog } from '@features/work/knowledge/documents/reader-dialog';
import { NotificationCenter } from '@features/platform/notifications/notification-center';
import { NotificationStore } from '@features/platform/notifications/notification-store';
import { trackKeyboardInset } from '@core/layout/keyboard-inset';
import { RouteProgress } from '@core/layout/route-progress';
import { bindSheetSwipe } from '@shared/ui/sheet-swipe';

@Component({
  imports: [
    UnhandledIssue,
    RouterOutlet,
    SettingsDialog,
    TestIdentityIndicator,
    ReaderDialog,
    NotificationCenter,
    RouteProgress,
  ],
  // eslint-disable-next-line @angular-eslint/component-selector -- index.html bootstrap element
  selector: 'app-root',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './app.html',
})
export class App {
  private readonly session = inject(WorkspaceSession);
  readonly testing = this.session.testing;
  readonly notifications = inject(NotificationStore);
  constructor() {
    const injector = inject(Injector);
    const destroyRef = inject(DestroyRef);
    destroyRef.onDestroy(trackKeyboardInset());
    destroyRef.onDestroy(bindSheetSwipe());
    inject(IdentityVeil).revealAfterFirstNavigation();
    let started = false;
    effect(() => {
      if (this.session.me() && !started) {
        started = true;
        void import('@core/monitoring/browser-presence')
          .then(({ BrowserPresence }) => injector.get(BrowserPresence))
          .catch(() => {
            started = false;
          });
      }
    });
  }
}
