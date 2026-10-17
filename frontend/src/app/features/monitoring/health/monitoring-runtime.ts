import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import type { MonitoringSnapshot } from '@core/api/schema';
import { below } from '@shared/browser/breakpoints';
import { formatBytes, formatNumber, metricText } from '@shared/browser/format';
import { mediaQuery } from '@shared/browser/media-query';
import { Icon } from '@shared/ui/icon';
import { NumberTicker } from '@shared/ui/number-ticker';
import { Panel } from '@shared/ui/panel';
import { Sparkline } from '@shared/ui/sparkline';
import { MonitoringStore } from '../monitoring-store';

const known = (values: readonly (number | null)[]) =>
  values.filter((value2): value2 is number => value2 !== null && Number.isFinite(value2));
const megabytes = (value: number) => formatNumber(Math.round(value / 1e6));
const percent = (value: number) => metricText(value, 'percent').replace('%', '');
const count = (value: number) => formatNumber(Math.round(value));

/**
 * 此頁面背後的 .NET 程序：GC 看到的記憶體與上限、受管理的堆積、
 * 執行緒池的佇列與為垃圾回收而暫停的時間，以及它們背後的計數器。
 * 手機會把它收起來；上方的生命指標已經包含 CPU 與記憶體。
 */
@Component({
  selector: 'nx-monitoring-runtime',
  imports: [Icon, NumberTicker, Panel, Sparkline],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './monitoring-runtime.scss',
  template: `<nx-panel heading="執行環境">
    <span panel-actions class="runtime-caption">此程序 · 每 3 秒取樣</span>
    <details class="runtime-fold" [open]="!phone()">
      <summary>
        顯示記憶體、執行緒與 GC 指標<nx-icon class="fold-chevron" name="chevron-down" />
      </summary>
      <div class="runtime-memory">
        <span class="runtime-label">記憶體</span>
        <strong
          >{{ bytes(data().resources.workingSetBytes) }}
          @if (limit(); as limit) {
            <small>／可用上限 {{ bytes(limit) }}</small>
          }
        </strong>
        @if (limit()) {
          <span class="runtime-meter" aria-hidden="true"
            ><i [style.width.%]="share(data().resources.workingSetBytes)"></i
            ><i class="heap" [style.width.%]="share(data().resources.managedHeapBytes)"></i
          ></span>
          <span class="runtime-legend"
            ><i></i>工作集 {{ percentOf(data().resources.workingSetBytes)
            }}<i class="heap"></i>受管理堆積
            {{ percentOf(data().resources.managedHeapBytes) }}</span
          >
        } @else {
          <small class="runtime-legend">GC 未回報可用記憶體上限。</small>
        }
      </div>
      <div class="runtime-figures">
        @for (figure of figures(); track figure.key) {
          <article [attr.data-tone]="figure.alert ? 'warning' : null">
            <span class="runtime-label">{{ figure.label }}</span>
            <strong
              ><nx-number-ticker [value]="figure.value" [format]="figure.format" />
              @if (figure.value !== null) {
                <em>{{ figure.unit }}</em>
              }
            </strong>
            <nx-sparkline [values]="figure.trend" />
          </article>
        }
      </div>
      <dl class="runtime-facts">
        <div>
          <dt>GC 次數（第 0／1／2 代）</dt>
          <dd>
            {{ number(data().resources.gen0Collections) }} /
            {{ number(data().resources.gen1Collections) }} /
            {{ number(data().resources.gen2Collections) }}
          </dd>
        </div>
        <div>
          <dt>執行緒集區</dt>
          <dd>{{ number(data().resources.threadPoolThreads) }} 個執行緒</dd>
        </div>
        <div>
          <dt>鎖定競爭</dt>
          <dd>{{ rate(data().resources.lockContentionsPerSecond) }} 次／秒</dd>
        </div>
        <div>
          <dt>處理序執行緒</dt>
          <dd>{{ number(data().resources.threadCount) }}</dd>
        </div>
      </dl>
    </details>
  </nx-panel>`,
})
export class MonitoringRuntime {
  private readonly store = inject(MonitoringStore);
  readonly data = input.required<MonitoringSnapshot>();
  readonly phone = mediaQuery(below('phone'));
  readonly bytes = formatBytes;
  readonly number = formatNumber;
  readonly limit = computed(() => this.data().resources.memoryLimitBytes);
  private readonly series = computed(() => {
    const points = this.store.resourceTimeline();
    return {
      heap: known(points.map((part) => part.managedHeapBytes)),
      queue: known(points.map((part2) => part2.threadPoolQueueLength)),
      gc: known(points.map((part3) => part3.gcPausePercent)),
    };
  });
  private readonly alerts = computed(
    () => new Set((this.store.health()?.issues ?? []).map((issue) => issue.code)),
  );
  readonly figures = computed(() => {
    const resources = this.data().resources,
      series = this.series(),
      alerts = this.alerts();
    return [
      {
        key: 'heap',
        label: '受管理堆積',
        value: resources.managedHeapBytes,
        format: megabytes,
        unit: 'MB',
        trend: series.heap,
        alert: false,
      },
      {
        key: 'queue',
        label: '執行緒集區排隊',
        value: resources.threadPoolQueueLength,
        format: count,
        unit: '項',
        trend: series.queue,
        alert: alerts.has('thread_pool_starvation'),
      },
      {
        key: 'gc',
        label: 'GC 暫停比例',
        value: resources.gcPausePercent,
        format: percent,
        unit: '%',
        trend: series.gc,
        alert: alerts.has('gc_pause_high'),
      },
    ];
  });
  share(value: number) {
    const limit = this.limit();
    return limit ? Math.min(100, (100 * value) / limit) : 0;
  }
  percentOf(value: number) {
    return metricText(this.share(value), 'percent');
  }
  rate(value: number) {
    return value.toLocaleString('zh-TW', { maximumFractionDigits: 1 });
  }
}
