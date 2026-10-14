import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { NumberTicker } from '@shared/ui/number-ticker';
import { Sparkline } from '@shared/ui/sparkline';
import { chargeKind, money } from '@features/billing/billing-api';
import { DashboardStore } from '../dashboard-store';
import { compactNumber, figureMoney, metricFormatter } from '@shared/browser/format';

/** The headline figures for the period and the work in progress now, each with its trend. */
@Component({
  selector: 'nx-overview-widget',
  imports: [NumberTicker, Sparkline],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './overview-widget.scss',
  template: `<div class="kpis">
      @for (kpi of kpis(); track kpi.key) {
        <article class="kpi" [attr.data-key]="kpi.key">
          <span class="kpi-label"
            >{{ kpi.label }}
            @if (kpi.live) {
              <i class="kpi-live" aria-hidden="true"></i>
            }
          </span>
          <strong class="kpi-value" [attr.title]="kpi.exact"
            ><nx-number-ticker [value]="kpi.value" [format]="kpi.format" />
            @if (kpi.unit) {
              <em>{{ kpi.unit }}</em>
            }
          </strong>
          <small>{{ kpi.hint }}</small>
          @if (kpi.trend) {
            <nx-sparkline [values]="kpi.trend" />
          }
        </article>
      }
    </div>
    <p class="kpi-note">
      統計範圍：{{ store.scopeLabel() }}。費用、呼叫與 Token 依查詢期間；工作狀態與數量是目前狀況。
    </p>`,
})
export class OverviewWidget {
  readonly store = inject(DashboardStore);
  readonly kpis = computed(() => {
    const data = this.store.data();
    if (!data) return [];
    const total = this.store.total(),
      tokens = this.store.tokenTotals(),
      unknown = this.store.unknownCalls();
    const generating = this.store.metric('chat', 'generating') ?? 0,
      jobs = this.store.metric('tasks', 'active') ?? 0;
    const count = metricFormatter('count');
    // Headline counts shorten past a hundred thousand so they fit the narrowest tile.
    const figure = (value: number) =>
      Math.abs(value) >= 100_000 ? compactNumber(value) : count(value);
    return [
      {
        key: 'spend',
        label: '區間已知費用',
        value: total?.knownCalls ? total.amount : null,
        format: (value: number) => (total ? figureMoney(value, total.currency) : figure(value)),
        exact: total?.knownCalls ? money(total.amount, total.currency) : null,
        unit: '',
        hint:
          (total ? chargeKind(total.kind) : '期間沒有已知費用') +
          (unknown ? ` · 各類型共 ${unknown} 次費用未知` : ''),
        trend: this.store.spendDaily().map((x) => x.value),
        live: false,
      },
      {
        key: 'calls',
        label: '模型與工具呼叫',
        value: data.spend.requests,
        format: figure,
        exact: count(data.spend.requests),
        unit: '次',
        hint: `${data.spend.pendingCalls} 次待結算 · 含搜尋、OCR、索引與評測`,
        trend: this.store.callsDaily().map((x) => x.value),
        live: false,
      },
      {
        key: 'tokens',
        label: 'Token 使用量',
        value: tokens.input + tokens.output,
        format: figure,
        exact: count(tokens.input + tokens.output),
        unit: '',
        hint: `輸入 ${count(tokens.input)} · 輸出 ${count(tokens.output)}`,
        trend: this.store.tokensDaily().map((x) => x.value),
        live: false,
      },
      {
        key: 'active',
        label: '處理中工作',
        value: this.store.activeWork(),
        format: figure,
        exact: count(this.store.activeWork()),
        unit: '件',
        hint: `${generating} 件 AI 回覆生成 · ${jobs} 件背景任務`,
        trend: null,
        live: this.store.activeWork() > 0,
      },
    ];
  });
}
