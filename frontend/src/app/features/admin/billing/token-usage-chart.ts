import { ChangeDetectionStrategy, Component, computed, input, signal } from '@angular/core';
import type { TokenDayDto, TokenUsageDto } from '@core/api/schema';
import { formatModelDisplayName, formatNumber } from '@shared/browser/format';
import { DataTable, DataTableRow } from '@shared/ui/data-table';
import { Select } from '@shared/ui/select';
import { TrendChart } from '@shared/ui/trend-chart';

interface ModelUsage {
  id: string;
  modelDisplayName?: string | null;
  input: number;
  output: number;
  requests: number;
  known: number;
}
const DAY = 86_400_000;
/** 圖表可切換的統計類型；第一個是預設與後備。 */
const METRICS = [
  {
    value: 'total',
    label: '合計',
    tokens: (row: TokenDayDto) => row.inputTokens + row.outputTokens,
  },
  { value: 'input', label: '輸入', tokens: (row: TokenDayDto) => row.inputTokens },
  { value: 'output', label: '輸出', tokens: (row: TokenDayDto) => row.outputTokens },
];
const metricOf = (value: string) => METRICS.find((metric) => metric.value === value) ?? METRICS[0];

@Component({
  selector: 'nx-token-usage-chart',
  imports: [DataTable, DataTableRow, Select, TrendChart],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `@if (usage(); as data) {
      <div class="token-controls">
        <nx-select
          label="Token 統計模型"
          [options]="choices()"
          [value]="selectedModel()"
          (valueChange)="model.set($event)"
        />
        <nx-select
          label="Token 統計類型"
          [options]="metrics"
          [value]="metric()"
          (valueChange)="metric.set($event)"
        />
      </div>
      <nx-trend-chart [points]="points()" [label]="'每日 ' + metricLabel() + ' Tokens'" />
      <div class="token-totals">
        <span
          >輸入 <strong>{{ number(totals().input) }}</strong></span
        ><span
          >輸出 <strong>{{ number(totals().output) }}</strong></span
        ><span
          >合計 <strong>{{ number(totals().input + totals().output) }}</strong></span
        >
      </div>
      <p class="form-note">
        {{ totals().known }} / {{ totals().requests }} 次模型呼叫有完整用量回報。缺少的 Token
        不視為零；合計納入已回報的數量。UTC{{ offsetLabel() }} · 包含聊天與背景模型處理。
      </p>
      @if (models().length) {
        <details class="token-models">
          <summary>依模型檢視 Token 分布（{{ models().length }}）</summary>
          <nx-data-table class="token-table" label="各模型 Token 使用量" [stacked]="true">
            <table>
              <thead>
                <tr>
                  <th scope="col">模型</th>
                  <th scope="col" class="ui-table-numeric">輸入</th>
                  <th scope="col" class="ui-table-numeric">輸出</th>
                  <th scope="col" class="ui-table-numeric">合計</th>
                  <th scope="col" class="ui-table-numeric">完整回報／呼叫</th>
                </tr>
              </thead>
              <tbody>
                @for (row of models(); track row.id) {
                  <tr
                    nxTableRow
                    [class.is-selected]="selectedModel() === row.id"
                    (rowActivate)="pick(row.id)"
                  >
                    <th scope="row">
                      <button
                        type="button"
                        class="ui-table-link"
                        data-row-action
                        [attr.aria-pressed]="selectedModel() === row.id"
                        (click)="pick(row.id)"
                      >
                        {{ modelName(row) }}
                      </button>
                    </th>
                    <td class="ui-table-numeric" data-label="輸入">
                      {{ number(row.input) }}
                    </td>
                    <td class="ui-table-numeric" data-label="輸出">
                      {{ number(row.output) }}
                    </td>
                    <td class="ui-table-numeric" data-label="合計">
                      <strong>{{ number(row.input + row.output) }}</strong
                      ><meter
                        min="0"
                        [max]="allTokens() || 1"
                        [value]="row.input + row.output"
                        [attr.aria-label]="modelName(row) + ' Token 占比'"
                      ></meter>
                    </td>
                    <td class="ui-table-numeric" data-label="完整回報／呼叫">
                      {{ row.known }} / {{ row.requests }}
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </nx-data-table>
        </details>
      }
    } @else {
      <p class="form-note">目前無法取得 Token 統計。</p>
    }`,
  styles: `
    :host {
      display: block;
      min-width: 0;
    }
    .token-controls {
      display: flex;
      gap: 0.75rem;
      flex-wrap: wrap;
      margin-bottom: 1rem;
    }
    .token-controls nx-select:first-child {
      flex: 1;
      min-width: 180px;
    }
    .token-totals {
      display: flex;
      flex-wrap: wrap;
      gap: 1rem;
      font-size: var(--text-caption);
      color: var(--secondary);
      margin: 0.75rem 0;
    }
    .token-totals strong {
      color: var(--ink);
      margin-left: 0.35rem;
      font-variant-numeric: tabular-nums;
    }
    .token-models {
      border-top: 1px solid var(--line);
      margin-top: 1rem;
    }
    summary {
      cursor: pointer;
      padding: 0.8rem 0;
      color: var(--secondary);
      font-size: var(--text-caption);
    }
    .token-table {
      margin-bottom: 1rem;
    }
    meter {
      display: block;
      width: 100%;
      min-width: 60px;
      height: 4px;
      margin-top: 0.4rem;
    }
  `,
})
export class TokenUsageChart {
  readonly usage = input<TokenUsageDto | null | undefined>(null);
  readonly model = signal('all');
  readonly metric = signal('total');
  readonly modelName = formatModelDisplayName;
  readonly number = formatNumber;
  readonly metrics = METRICS.map(({ value, label }) => ({ value, label: label + ' Token' }));
  readonly metricLabel = computed(() => metricOf(this.metric()).label);
  readonly models = computed(() => {
    const map = new Map<string, ModelUsage>();
    for (const day of this.usage()?.daily ?? []) {
      const row = map.get(day.modelId) ?? {
        id: day.modelId,
        modelDisplayName: day.modelDisplayName,
        input: 0,
        output: 0,
        requests: 0,
        known: 0,
      };
      row.input += day.inputTokens;
      row.output += day.outputTokens;
      row.requests += day.requests;
      row.known += day.requestsWithUsage;
      map.set(day.modelId, row);
    }
    return [...map.values()].sort(
      (first, second) => second.input + second.output - first.input - first.output,
    );
  });
  readonly allTokens = computed(() =>
    this.models().reduce((sum, row) => sum + row.input + row.output, 0),
  );
  readonly choices = computed(() => [
    { value: 'all', label: '所有模型' },
    ...this.models().map((row) => ({ value: row.id, label: this.modelName(row) })),
  ]);
  readonly selectedModel = computed(() =>
    this.models().some((row) => row.id === this.model()) ? this.model() : 'all',
  );
  /** 明細列會把圖表篩選到其模型；再次選取則顯示所有模型。 */
  pick(id: string) {
    this.model.set(this.selectedModel() === id ? 'all' : id);
  }
  readonly rows = computed(() =>
    (this.usage()?.daily ?? []).filter(
      (row) => this.selectedModel() === 'all' || row.modelId === this.selectedModel(),
    ),
  );
  readonly totals = computed(() =>
    this.rows().reduce(
      (sum, row) => ({
        input: sum.input + row.inputTokens,
        output: sum.output + row.outputTokens,
        requests: sum.requests + row.requests,
        known: sum.known + row.requestsWithUsage,
      }),
      { input: 0, output: 0, requests: 0, known: 0 },
    ),
  );
  readonly offsetLabel = computed(() => {
    const minutes = this.usage()?.timezoneOffsetMinutes ?? 0;
    const abs = Math.abs(minutes);
    const pad = (value: number) => String(value).padStart(2, '0');
    return `${minutes < 0 ? '-' : '+'}${pad(Math.floor(abs / 60))}:${pad(abs % 60)}`;
  });
  /** 期間內每一天都有一點（沒有用量的日子為 0）；日期依報表的時區偏移切分，不用瀏覽器時區。 */
  readonly points = computed(() => {
    const usage = this.usage();
    if (!usage) return [];
    const date = (value: number) =>
      new Date(value + usage.timezoneOffsetMinutes * 60000).toISOString().slice(0, 10);
    const start = date(Date.parse(usage.from)),
      end = date(Date.parse(usage.until) - 1);
    const tokens = metricOf(this.metric()).tokens;
    const grouped = new Map<string, number>();
    for (const row of this.rows())
      grouped.set(row.date, (grouped.get(row.date) ?? 0) + tokens(row));
    const points = [];
    for (let day = Date.parse(start), last = Date.parse(end); day <= last; day += DAY) {
      const label = new Date(day).toISOString().slice(0, 10);
      points.push({ label, value: grouped.get(label) ?? 0 });
    }
    return points;
  });
}
