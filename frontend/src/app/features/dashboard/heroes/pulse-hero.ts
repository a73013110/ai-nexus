import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { NumberTicker } from '../../../shared/ui/number-ticker';
import { money } from '../../billing/billing-api';
import { DashboardStore } from '../dashboard-store';
import { compactNumber, metricLabel } from '../dashboard-format';

/**
 * Editorial: the period's call count set huge, a heartbeat line of the days, the other headline
 * figures in a row, and every section's lead figure running past in a ticker.
 */
@Component({
  selector: 'nx-pulse-hero',
  imports: [NumberTicker],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './pulse-hero.scss',
  template: `<div class="pulse-head">
      <span class="pulse-eyebrow"
        >{{ store.scopeLabel() }} / {{ store.appliedFrom() }} → {{ store.appliedThrough() }}</span
      >
      <span class="pulse-live" [attr.data-state]="store.activeWork() ? 'active' : 'idle'"
        ><i aria-hidden="true"></i
        >{{ store.activeWork() ? 'LIVE · ' + store.activeWork() + ' 件處理中' : 'IDLE' }}</span
      >
    </div>
    <h2 class="pulse-figure">
      <nx-number-ticker [value]="store.data()?.spend?.requests ?? 0" />
      <span>次模型與工具呼叫</span>
    </h2>
    <svg class="pulse-line" viewBox="0 0 1000 120" preserveAspectRatio="none" aria-hidden="true">
      <path class="pulse-base" [attr.d]="path()" />
      <path class="pulse-trace" pathLength="1" [attr.d]="path()" />
    </svg>
    <dl class="pulse-stats">
      @for (stat of stats(); track stat.label) {
        <div [style.animation-delay.ms]="200 + $index * 90">
          <dt>{{ stat.label }}</dt>
          <dd>{{ stat.value }}</dd>
        </div>
      }
    </dl>
    @if (ticker().length) {
      <div class="pulse-ticker" aria-hidden="true">
        <div>
          @for (copy of [0, 1]; track copy) {
            @for (item of ticker(); track item.key) {
              <span
                ><b>{{ item.label }}</b> {{ item.value }}</span
              >
            }
          }
        </div>
      </div>
    }`,
})
export class PulseHero {
  readonly store = inject(DashboardStore);
  /** A heartbeat: each day is a spike proportional to its calls on a flat baseline. */
  readonly path = computed(() => {
    const days = this.store.callsDaily();
    if (!days.length) return 'M0 90 L1000 90';
    const max = Math.max(1, ...days.map((x) => x.value));
    const step = 1000 / days.length;
    let d = 'M0 90';
    days.forEach((day, i) => {
      const x = i * step,
        h = (day.value / max) * 80;
      d += ` L${x + step * 0.3} 90 L${x + step * 0.42} ${90 - h} L${x + step * 0.55} ${90 + h * 0.25} L${x + step * 0.66} 90`;
    });
    return d + ' L1000 90';
  });
  readonly stats = computed(() => {
    const total = this.store.total(),
      tokens = this.store.tokenTotals();
    return [
      {
        label: '已知費用',
        value: total?.knownCalls ? money(total.amount, total.currency) : '—',
      },
      { label: 'Token', value: compactNumber(tokens.input + tokens.output) },
      { label: '期間提問', value: compactNumber(this.store.metric('chat', 'questions') ?? 0) },
      { label: '處理中工作', value: String(this.store.activeWork()) },
    ];
  });
  readonly ticker = computed(() =>
    this.store.sections().flatMap((section) =>
      section.metrics.slice(0, 2).map((metric) => ({
        key: section.id + metric.key,
        label: section.title + ' ' + metric.label,
        value: metricLabel(metric),
      })),
    ),
  );
}
