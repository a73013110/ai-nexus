import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { WORKSPACE_HOME } from './workspace-home';
import { BrandWordmark } from '../../shared/ui/brand-wordmark';

@Component({
  selector: 'nx-workspace-brand',
  imports: [RouterLink, BrandWordmark],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './workspace-brand.scss',
  template: `<a class="brand" [routerLink]="home" aria-label="AI Nexus 總覽">
    <nx-brand-wordmark [compact]="compact()" aria-hidden="true" />
  </a>`,
})
export class WorkspaceBrand {
  readonly compact = input(false);
  readonly home = WORKSPACE_HOME;
}
