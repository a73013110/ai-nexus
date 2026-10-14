import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import type { MonitoringSnapshot } from '@core/api/schema';
import { formatBytes, formatNumber, metricText } from '@shared/browser/format';
import type { BadgeTone } from '@shared/ui/count-badge';
import { NumberTicker } from '@shared/ui/number-ticker';
import { Sparkline } from '@shared/ui/sparkline';
import { StatusBadge } from '@shared/ui/status-badge';
import { MonitoringStore } from '../monitoring-store';
import { monitoringLatency } from '../monitoring-format';

/** Known values of a series; decorative trends skip the buckets that had no samples. */
const known = (values: readonly (number | null)[]) =>
  values.filter((v): v is number => v !== null && Number.isFinite(v));
const decimals = (value: number) =>
  value.toLocaleString('zh-TW', { maximumFractionDigits: value >= 100 ? 0 : value >= 10 ? 1 : 2 });
const whole = (value: number) => formatNumber(Math.round(value));
const fixed = [0, 1, 2].map((digits) => (value: number) => value.toFixed(digits));
/** Bytes split into a rolling number and a fixed unit, scaled by the current value. */
function byteScale(value: number) {
  return value >= 1e9
    ? { scale: 1e9, unit: 'GB', digits: 2 }
    : { scale: 1e6, unit: 'MB', digits: 0 };
}

interface Vital {
  key: string;
  label: string;
  value: number | null;
  format: (value: number) => string;
  unit: string;
  hint: string;
  trend: readonly number[] | null;
  split: readonly { key: string; share: number }[] | null;
  tone: BadgeTone;
}

/**
 * Six vital signs of the instance right now (the last minute, whatever the window) with their
 * trend over the window. Tiles and their sparklines keep their instances across snapshots, so
 * numbers roll and lines draw only once.
 */
@Component({
  selector: 'nx-monitoring-vitals',
  imports: [NumberTicker, Sparkline, StatusBadge],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './monitoring-vitals.scss',
  host: { role: 'region', 'aria-labelledby': 'monitor-vitals-title' },
  template: `<h2 class="sr-only" id="monitor-vitals-title">關鍵指標</h2>
    <div class="vitals">
      @for (vital of vitals(); track vital.key) {
        <article class="vital" [attr.data-vital]="vital.key" [attr.data-tone]="vital.tone">
          <span class="vital-label"
            ><span>{{ vital.label }}</span>
            @if (vital.tone !== 'neutral') {
              <nx-status-badge [tone]="vital.tone">{{
                vital.tone === 'danger' ? '嚴重' : '注意'
              }}</nx-status-badge>
            }
          </span>
          <strong class="vital-value"
            ><nx-number-ticker [value]="vital.value" [format]="vital.format" />
            @if (vital.unit && vital.value !== null) {
              <em>{{ vital.unit }}</em>
            }
          </strong>
          <small>{{ vital.hint }}</small>
          @if (vital.split; as split) {
            <span class="vital-split" aria-hidden="true">
              @for (part of split; track part.key) {
                <i [attr.data-part]="part.key" [style.flex-grow]="part.share"></i>
              }
            </span>
          } @else if (vital.trend) {
            <nx-sparkline [values]="vital.trend" />
          }
        </article>
      }
    </div>
    <p class="vitals-note">數值為最近 1 分鐘；走勢為所選範圍，每點 10 秒。</p>`,
})
export class MonitoringVitals {
  private readonly store = inject(MonitoringStore);
  readonly data = input.required<MonitoringSnapshot>();
  // One array per series, recomputed only when a new bucket closes (store slices).
  private readonly series = computed(() => {
    const traffic = this.store.timeline(),
      resources = this.store.resourceTimeline();
    return {
      requests: traffic.map((p) => p.requestsPerSecond),
      p95: known(traffic.map((p) => p.p95Ms)),
      errors: traffic.map((p) => p.serverErrors),
      cpu: known(resources.map((p) => p.cpuPercent)),
      memory: known(resources.map((p) => p.workingSetBytes)),
    };
  });
  private readonly tones = computed(() => {
    const tones = new Map<string, BadgeTone>();
    const raise = (key: string, severity: string) => {
      const tone: BadgeTone = severity === 'critical' ? 'danger' : 'warning';
      if (tones.get(key) !== 'danger') tones.set(key, tone);
    };
    const keys: Record<string, string> = {
      sessions_dropped: 'users',
      latency_high: 'p95',
      server_errors: 'errors',
      error_rate_high: 'errors',
      cpu_high: 'cpu',
      memory_high: 'memory',
      gc_pause_high: 'memory',
    };
    for (const issue of this.store.health()?.issues ?? [])
      if (keys[issue.code]) raise(keys[issue.code], issue.severity);
    return tones;
  });
  readonly vitals = computed<Vital[]>(() => {
    const data = this.data(),
      now = data.recentTraffic,
      resources = data.resources,
      series = this.series(),
      tones = this.tones();
    const states = { active: 0, idle: 0, background: 0 } as Record<string, number>;
    for (const session of data.sessions) states[session.state] = (states[session.state] ?? 0) + 1;
    const p95 = now.p95Ms,
      memory = byteScale(resources.workingSetBytes);
    const limit = resources.memoryLimitBytes;
    return [
      {
        key: 'users',
        label: '在線使用者',
        value: data.onlineUsers,
        format: formatNumber,
        unit: '人',
        hint: `${data.activeSessions} 使用中 · ${states['idle']} 閒置 · ${states['background']} 背景`,
        trend: null,
        split: ['active', 'idle', 'background'].map((key) => ({ key, share: states[key] })),
        tone: tones.get('users') ?? 'neutral',
      },
      {
        key: 'requests',
        label: 'API 請求速率',
        value: now.requestsPerSecond,
        format: decimals,
        unit: '次／秒',
        hint: `${data.inFlightRequests} 處理中 · ${data.openStreams} 串流連線`,
        trend: series.requests,
        split: null,
        tone: 'neutral',
      },
      {
        key: 'p95',
        label: 'P95 回應時間',
        value: p95 === null ? null : p95 >= 1000 ? p95 / 1000 : p95,
        format: p95 !== null && p95 >= 1000 ? fixed[1] : whole,
        unit: p95 !== null && p95 >= 1000 ? '秒' : 'ms',
        hint: `平均 ${monitoringLatency(now.averageMs)}`,
        trend: series.p95,
        split: null,
        tone: tones.get('p95') ?? 'neutral',
      },
      {
        key: 'errors',
        label: '伺服器錯誤',
        value: now.serverErrors,
        format: whole,
        unit: '次',
        hint: `同一分鐘 4xx／5xx 共 ${formatNumber(now.errors)} 次 · 錯誤率 ${metricText(now.errorPercent, 'percent')}`,
        trend: series.errors,
        split: null,
        tone: tones.get('errors') ?? 'neutral',
      },
      {
        key: 'cpu',
        label: '程序 CPU',
        value: resources.cpuPercent,
        format: fixed[1],
        unit: '%',
        hint:
          resources.cpuPercent === null
            ? '第一次取樣後顯示'
            : `占所有核心 · ${formatNumber(resources.threadCount)} 執行緒`,
        trend: series.cpu,
        split: null,
        tone: tones.get('cpu') ?? 'neutral',
      },
      {
        key: 'memory',
        label: '記憶體',
        value: resources.workingSetBytes / memory.scale,
        format: fixed[memory.digits],
        unit: memory.unit,
        hint: limit
          ? `可用上限 ${formatBytes(limit)} · 已用 ${metricText((100 * resources.workingSetBytes) / limit, 'percent')}`
          : `受管理堆積 ${formatBytes(resources.managedHeapBytes)}`,
        trend: series.memory,
        split: null,
        tone: tones.get('memory') ?? 'neutral',
      },
    ];
  });
}
