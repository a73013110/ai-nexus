import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { FeaturePage } from '@core/layout/feature-page';
import { PlatformUsage } from './platform-usage';

/** 平台用量：最近 30 天的使用量、模型與使用來源，以及模型與搜尋服務的狀態。 */
@Component({
  selector: 'nx-usage-page',
  imports: [FeaturePage, PlatformUsage],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<nx-feature-page
    [title]="session.featureName('operations.usage')"
    description="檢查平台用量，以及模型與搜尋服務的狀態。"
  >
    <nx-platform-usage />
  </nx-feature-page>`,
})
export class UsagePage {
  readonly session = inject(WorkspaceSession);
}
