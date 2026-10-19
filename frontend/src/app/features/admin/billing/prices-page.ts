import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { FeaturePage } from '@core/layout/feature-page';
import { PriceBook } from './prices/price-book';

/** 模型與工具價格：目前適用的價格與版本歷史；每次修改都新增下一個版本。 */
@Component({
  selector: 'nx-prices-page',
  imports: [FeaturePage, PriceBook],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<nx-feature-page
    [title]="session.featureName('billing.prices')"
    description="管理模型與工具的價格版本，作為用量費用的估算依據。"
  >
    <nx-price-book />
  </nx-feature-page>`,
})
export class PricesPage {
  readonly session = inject(WorkspaceSession);
}
