import { ChangeDetectionStrategy, Component, effect, inject, Injector } from '@angular/core';
import { WorkspaceSession } from './core/auth/workspace-session';
import { UnhandledIssue } from './shared/ui/unhandled-issue';
import { RouterOutlet } from '@angular/router';
import { SettingsDialog } from './features/settings/settings-dialog';
import { IdentityBanner } from './shared/ui/identity-banner';
import { ReaderDialog } from './features/knowledge/reader-dialog';
import { NotificationCenter } from './shared/ui/notification-center';

@Component({
  imports: [
    UnhandledIssue,
    RouterOutlet,
    SettingsDialog,
    IdentityBanner,
    ReaderDialog,
    NotificationCenter,
  ],
  selector: 'app-root',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './app.html',
})
export class App {
  constructor() {
    const session = inject(WorkspaceSession);
    const injector = inject(Injector);
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
