import { ChangeDetectionStrategy, Component, computed, inject, viewChild } from '@angular/core';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { FeaturePage } from '@core/layout/feature-page';
import { PlatformUsage } from './platform-usage';
import { usageTour } from './usage-tour';

/** 平台用量：最近 30 天的使用量、模型與使用來源，以及模型與搜尋服務的狀態。 */
@Component({
  selector: 'nx-usage-page',
  imports: [FeaturePage, PlatformUsage],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<nx-feature-page
    [title]="session.featureName('operations.usage')"
    description="檢查平台用量，以及模型與搜尋服務的狀態。"
    [tour]="tour()"
  >
    <nx-platform-usage />
  </nx-feature-page>`,
})
export class UsagePage {
  readonly session = inject(WorkspaceSession);
  private readonly usage = viewChild(PlatformUsage);
  readonly tour = computed(() =>
    usageTour({
      empty: this.usage()?.stats()?.requests === 0,
      dashboard: this.session.has('dashboard') ? this.session.featureName('dashboard') : null,
      prices: this.session.has('billing.prices')
        ? this.session.featureName('billing.prices')
        : null,
    }),
  );
}
