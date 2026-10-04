import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'nx-workspace-brand',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<a class="brand" routerLink="/chat" aria-label="AI Nexus 新對話">
    <span class="brand-symbol"
      ><svg viewBox="0 0 32 32" fill="none" aria-hidden="true">
        <path d="M9 24V8l14 16V8M5 8h4M23 24h4" stroke="currentColor" stroke-width="1.8" /></svg
    ></span>
    <span>AI <strong>Nexus</strong><small>公司工作台</small></span>
  </a>`,
})
export class WorkspaceBrand {}
