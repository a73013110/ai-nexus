import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { DonutChart } from '@shared/ui/donut-chart';
import { ViewSwitch } from '@shared/ui/view-switch';
import { DashboardStore } from '../dashboard-store';
import { compactNumber, figureMoney, formatModelDisplayName } from '@shared/browser/format';

const SHOWN = 6;

/** How the period's spend or tokens divide between models and tools. */
@Component({
  selector: 'nx-mix-widget',
  imports: [DonutChart, ViewSwitch],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: grid;
      gap: var(--p-space-3);
      container-type: inline-size;
      --view-switch-radius: var(--p-radius-pill);
      --view-switch-track: var(--tile-inset);
      --donut-value-size: clamp(0.8125rem, 5.5cqi, 1.125rem);
    }
  `,
  template: `<nx-view-switch
      label="分布依據"
      appearance="segment"
      [options]="views"
      [value]="view()"
      (valueChange)="view.set($event)"
    />
    @if (view() === 'spend') {
      <nx-donut-chart
        label="模型與工具已知費用分布"
        [segments]="spend()"
        [format]="spendFormat()"
        totalLabel="已知費用"
        empty="價格設定完成後，新的呼叫會出現在這裡。"
      />
    } @else {
      <nx-donut-chart
        label="各模型 Token 分布"
        [segments]="tokens()"
        [format]="compact"
        totalLabel="Token"
      />
    }`,
})
export class MixWidget {
  readonly store = inject(DashboardStore);
  readonly views = [
    { value: 'spend', label: '費用' },
    { value: 'tokens', label: 'Token' },
  ];
  readonly view = signal('spend');
  readonly compact = compactNumber;
  readonly spendFormat = computed(() => {
    const currency = this.store.total()?.currency ?? '';
    return (value: number) => (currency ? figureMoney(value, currency) : compactNumber(value));
  });
  readonly spend = computed(() =>
    top(this.store.modelRows().map((x) => ({ key: x.label, label: x.label, value: x.amount }))),
  );
  readonly tokens = computed(() => {
    const models = new Map<string, { key: string; label: string; value: number }>();
    for (const row of this.store.data()?.tokens.daily ?? []) {
      const model = models.get(row.modelId) ?? {
        key: row.modelId,
        label: formatModelDisplayName(row),
        value: 0,
      };
      model.value += row.inputTokens + row.outputTokens;
      models.set(row.modelId, model);
    }
    return top([...models.values()].sort((a, b) => b.value - a.value));
  });
}

/** The largest parts, with the rest folded into one. */
function top(parts: { key: string; label: string; value: number }[]) {
  const sorted = [...parts].sort((a, b) => b.value - a.value);
  const rest = sorted.slice(SHOWN).reduce((sum, x) => sum + x.value, 0);
  return rest > 0
    ? [...sorted.slice(0, SHOWN), { key: '__other', label: '其他', value: rest }]
    : sorted;
}
