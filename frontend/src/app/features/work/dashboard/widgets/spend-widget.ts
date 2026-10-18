import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { TrendChart } from '@shared/ui/trend-chart';
import { chargeKind } from '@features/admin/billing/billing-api';
import { DashboardStore } from '../dashboard-store';

/** 單一幣別與收費類型下，每天的已知花費；類型與幣別不會相加。 */
@Component({
  selector: 'nx-spend-widget',
  imports: [TrendChart],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: grid;
      gap: var(--p-space-3);
    }
    p {
      margin: 0;
      color: var(--secondary);
      font-size: var(--p-font-xs);
    }
  `,
  template: `<nx-trend-chart [points]="points()" label="每日已知費用" [currency]="currency()" />
    <p>{{ note() }}</p>`,
})
export class SpendWidget {
  readonly store = inject(DashboardStore);
  readonly points = computed(() =>
    this.store.spendDaily().map((item) => ({ label: item.date, value: item.value })),
  );
  readonly currency = computed(() => this.store.total()?.currency ?? '');
  readonly note = computed(() => {
    const total = this.store.total();
    return `${total ? chargeKind(total.kind) : '尚無價格紀錄'} · 未知費用不納入金額，不同幣別與費用類型不相加。`;
  });
}
