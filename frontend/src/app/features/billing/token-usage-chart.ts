import { ChangeDetectionStrategy, Component, computed, input, signal } from '@angular/core';
import type { TokenUsageDto } from '../../core/api/schema';
import { formatModelDisplayName } from '../../shared/browser/format';
import { Select } from '../../shared/ui/select';
import { TrendChart } from '../../shared/ui/trend-chart';

@Component({
  selector: 'nx-token-usage-chart',
  imports: [Select, TrendChart],
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
          >輸入 <strong>{{ totals().input.toLocaleString() }}</strong></span
        ><span
          >輸出 <strong>{{ totals().output.toLocaleString() }}</strong></span
        ><span
          >合計 <strong>{{ (totals().input + totals().output).toLocaleString() }}</strong></span
        >
      </div>
      <p class="form-note">
        {{ totals().known }} / {{ totals().requests }} 次模型呼叫有完整用量回報。缺少的 Token
        不視為零；合計納入已回報的數量。UTC{{ offsetLabel() }} · 包含聊天與背景模型處理。
      </p>
      @if (models().length) {
        <details class="token-models">
          <summary>依模型檢視 Token 分布（{{ models().length }}）</summary>
          <div class="token-table">
            <table>
              <caption class="sr-only">
                各模型 Token 使用量
              </caption>
              <thead>
                <tr>
                  <th scope="col">模型</th>
                  <th scope="col">輸入</th>
                  <th scope="col">輸出</th>
                  <th scope="col">合計</th>
                  <th scope="col">完整回報／呼叫</th>
                </tr>
              </thead>
              <tbody>
                @for (row of models(); track row.id) {
                  <tr>
                    <th scope="row">
                      <button
                        (click)="model.set(row.id)"
                        [attr.aria-pressed]="selectedModel() === row.id"
                      >
                        {{ modelName(row) }}
                      </button>
                    </th>
                    <td>{{ row.input.toLocaleString() }}</td>
                    <td>{{ row.output.toLocaleString() }}</td>
                    <td>
                      <strong>{{ (row.input + row.output).toLocaleString() }}</strong
                      ><meter
                        min="0"
                        [max]="allTokens() || 1"
                        [value]="row.input + row.output"
                        [attr.aria-label]="modelName(row) + ' Token 占比'"
                      ></meter>
                    </td>
                    <td>{{ row.known }} / {{ row.requests }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
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
      overflow: auto;
    }
    table {
      width: 100%;
      font-size: var(--text-caption);
      border-collapse: collapse;
    }
    th,
    td {
      padding: 0.6rem 0.4rem;
      text-align: right;
      border-bottom: 1px solid var(--line);
      white-space: nowrap;
    }
    th:first-child {
      text-align: left;
    }
    th {
      font-weight: 500;
    }
    td meter {
      display: block;
      width: 100%;
      min-width: 60px;
      height: 4px;
      margin-top: 0.4rem;
    }
    th button {
      text-align: left;
      padding: 0.3rem;
      border-radius: var(--p-radius-sm);
    }
    th button[aria-pressed='true'] {
      background: var(--accent-soft);
      color: var(--signal);
    }
  `,
})
export class TokenUsageChart {
  readonly usage = input<TokenUsageDto | null | undefined>(null);
  readonly model = signal('all');
  readonly metric = signal('total');
  readonly modelName = formatModelDisplayName;
  readonly metrics = [
    { value: 'total', label: '合計 Token' },
    { value: 'input', label: '輸入 Token' },
    { value: 'output', label: '輸出 Token' },
  ];
  readonly metricLabel = computed(() =>
    this.metric() === 'input' ? '輸入' : this.metric() === 'output' ? '輸出' : '合計',
  );
  readonly models = computed(() => {
    const map = new Map<
      string,
      {
        id: string;
        modelDisplayName?: string | null;
        input: number;
        output: number;
        requests: number;
        known: number;
      }
    >();
    for (const day of this.usage()?.daily ?? []) {
      const row = map.get(day.modelId) || {
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
    return [...map.values()].sort((a, b) => b.input + b.output - a.input - a.output);
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
    return `${minutes < 0 ? '-' : '+'}${String(Math.floor(Math.abs(minutes) / 60)).padStart(2, '0')}:${String(Math.abs(minutes) % 60).padStart(2, '0')}`;
  });
  readonly points = computed(() => {
    const usage = this.usage();
    if (!usage) return [];
    const date = (value: number) =>
      new Date(value + usage.timezoneOffsetMinutes * 60000).toISOString().slice(0, 10);
    const start = date(Date.parse(usage.from)),
      end = date(Date.parse(usage.until) - 1);
    const grouped = new Map<string, number>();
    for (const row of this.rows())
      grouped.set(
        row.date,
        (grouped.get(row.date) || 0) +
          (this.metric() === 'input'
            ? row.inputTokens
            : this.metric() === 'output'
              ? row.outputTokens
              : row.inputTokens + row.outputTokens),
      );
    const points = [];
    for (let day = Date.parse(start), last = Date.parse(end); day <= last; day += 86400000) {
      const label = new Date(day).toISOString().slice(0, 10);
      points.push({ label, value: grouped.get(label) || 0 });
    }
    return points;
  });
}
