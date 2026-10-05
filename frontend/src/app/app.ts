import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { SettingsDialog } from './features/settings/settings-dialog';
import { IdentityBanner } from './shared/ui/identity-banner';

@Component({
  imports: [RouterOutlet, SettingsDialog, IdentityBanner],
  selector: 'app-root',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './app.html',
})
export class App {}
