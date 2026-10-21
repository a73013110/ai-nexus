import { ChangeDetectionStrategy, Component, computed, inject, viewChild } from '@angular/core';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { FeaturePage } from '@core/layout/feature-page';
import { PriceBook } from './prices/price-book';
import { pricesTour } from './prices-tour';

/** 模型與工具價格：目前適用的價格與版本歷史；每次修改都新增下一個版本。 */
@Component({
  selector: 'nx-prices-page',
  imports: [FeaturePage, PriceBook],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<nx-feature-page
    [title]="session.featureName('billing.prices')"
    description="管理模型與工具的價格版本，作為用量費用的估算依據。"
    [tour]="tour()"
  >
    <nx-price-book />
  </nx-feature-page>`,
})
export class PricesPage {
  readonly session = inject(WorkspaceSession);
  /** 分頁狀態在價格表內；導覽依它決定介紹哪一個表格。 */
  private readonly book = viewChild(PriceBook);
  readonly tour = computed(() =>
    pricesTour({ view: this.book()?.view() ?? 'current', testing: this.session.testing() }),
  );
}
