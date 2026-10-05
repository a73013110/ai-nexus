import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { SettingsDialog } from './features/settings/settings-dialog';
import { IdentityBanner } from './shared/ui/identity-banner';
import { ReaderDialog } from './features/knowledge/reader-dialog';

@Component({
  imports: [RouterOutlet, SettingsDialog, IdentityBanner, ReaderDialog],
  selector: 'app-root',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './app.html',
})
export class App {}
