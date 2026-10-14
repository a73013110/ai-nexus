import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { TokenUsageChart } from '@features/billing/token-usage-chart';
import { DashboardStore } from '../dashboard-store';

@Component({
  selector: 'nx-tokens-widget',
  imports: [TokenUsageChart],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: block;
    }
  `,
  template: `<nx-token-usage-chart [usage]="store.data()?.tokens" />`,
})
export class TokensWidget {
  readonly store = inject(DashboardStore);
}
