import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  effect,
  inject,
  Injector,
} from '@angular/core';
import { WorkspaceSession } from './core/auth/workspace-session';
import { UnhandledIssue } from './core/errors/unhandled-issue';
import { RouterOutlet } from '@angular/router';
import { SettingsDialog } from './features/settings/settings-dialog';
import { IdentityBanner } from './core/auth/identity-banner';
import { ReaderDialog } from './features/knowledge/reader-dialog';
import { NotificationCenter } from './features/notifications/notification-center';
import { trackKeyboardInset } from './core/layout/keyboard-inset';
import { bindSheetSwipe } from './shared/ui/sheet-swipe';

@Component({
  imports: [
    UnhandledIssue,
    RouterOutlet,
    SettingsDialog,
    IdentityBanner,
    ReaderDialog,
    NotificationCenter,
  ],
  // eslint-disable-next-line @angular-eslint/component-selector -- index.html bootstrap element
  selector: 'app-root',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './app.html',
})
export class App {
  constructor() {
    const session = inject(WorkspaceSession);
    const injector = inject(Injector);
    const destroyRef = inject(DestroyRef);
    destroyRef.onDestroy(trackKeyboardInset());
    destroyRef.onDestroy(bindSheetSwipe());
    let started = false;
    effect(() => {
      if (session.me() && !started) {
        started = true;
        void import('./core/monitoring/browser-presence')
          .then(({ BrowserPresence }) => injector.get(BrowserPresence))
          .catch(() => {
            started = false;
          });
      }
    });
  }
}
