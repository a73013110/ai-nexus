import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { Icon } from '../../../shared/ui/icon';
import { NumberTicker } from '../../../shared/ui/number-ticker';
import { Sparkline } from '../../../shared/ui/sparkline';
import { chargeKind, money } from '../../billing/billing-api';
import { DashboardStore } from '../dashboard-store';
import { metricFormatter } from '../dashboard-format';

/** The headline figures for the period and the work in progress now, each with its trend. */
@Component({
  selector: 'nx-overview-widget',
  imports: [Icon, NumberTicker, Sparkline],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './overview-widget.scss',
  template: `<div class="kpis">
      @for (kpi of kpis(); track kpi.key) {
        <article class="kpi" [attr.data-key]="kpi.key" [style.--kpi-color]="kpi.color">
          <span class="kpi-label"><nx-icon [name]="kpi.icon" />{{ kpi.label }}</span>
          <strong class="kpi-value"
            ><nx-number-ticker [value]="kpi.value" [format]="kpi.format" />
            @if (kpi.unit) {
              <em>{{ kpi.unit }}</em>
            }
            @if (kpi.live) {
              <i class="kpi-live" aria-hidden="true"></i>
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
    return [
      {
        key: 'spend',
        icon: 'money',
        label: '區間已知費用',
        value: total?.knownCalls ? total.amount : null,
        format: (value: number) => (total ? money(value, total.currency) : count(value)),
        unit: '',
        hint:
          (total ? chargeKind(total.kind) : '請由管理員設定模型與工具價格') +
          (unknown ? ` · 各類型共 ${unknown} 次費用未知` : ''),
        trend: this.store.spendDaily().map((x) => x.value),
        color: 'var(--series-1)',
        live: false,
      },
      {
        key: 'calls',
        icon: 'chart',
        label: '模型與工具呼叫',
        value: data.spend.requests,
        format: count,
        unit: '次',
        hint: `${data.spend.pendingCalls} 次待結算 · 含搜尋、OCR、索引與評測`,
        trend: this.store.callsDaily().map((x) => x.value),
        color: 'var(--series-2)',
        live: false,
      },
      {
        key: 'tokens',
        icon: 'database',
        label: 'Token 使用量',
        value: tokens.input + tokens.output,
        format: count,
        unit: '',
        hint: `輸入 ${count(tokens.input)} · 輸出 ${count(tokens.output)}`,
        trend: this.store.tokensDaily().map((x) => x.value),
        color: 'var(--series-5)',
        live: false,
      },
      {
        key: 'active',
        icon: 'tasks',
        label: '處理中工作',
        value: this.store.activeWork(),
        format: count,
        unit: '件',
        hint: `${generating} 件 AI 回覆生成 · ${jobs} 件背景任務`,
        trend: null,
        color: 'var(--series-3)',
        live: this.store.activeWork() > 0,
      },
    ];
  });
}
