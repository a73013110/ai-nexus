import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { Panel } from '../../../shared/ui/panel';
import { TrendChart } from '../../../shared/ui/trend-chart';
import { ViewSwitch } from '../../../shared/ui/view-switch';
import { MonitoringStore } from './monitoring-store';
import { monitoringTime } from './monitoring-format';

const METRICS = [
  { value: 'requests', label: '請求速率', title: 'API 請求速率', unit: '次／秒' },
  { value: 'p95', label: 'P95 回應時間', title: 'P95 回應時間', unit: 'ms' },
  { value: 'errors', label: '伺服器錯誤', title: '伺服器錯誤', unit: '次' },
  { value: 'bytes', label: 'HTTP 回應量', title: 'HTTP 回應量', unit: 'KB／秒' },
] as const;

/** Traffic over the chosen window, one metric at a time. */
@Component({
  selector: 'nx-monitoring-traffic',
  imports: [Panel, TrendChart, ViewSwitch],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: block;
      min-width: 0;
    }
    nx-panel {
      height: 100%;
    }
    nx-view-switch {
      margin: 0;
      --view-switch-button-font-size: var(--p-font-xs);
    }
    p {
      margin: var(--p-space-2) 0 0;
      color: var(--muted);
      font-size: var(--p-font-xs);
      line-height: 1.6;
    }
  `,
  template: `<nx-panel heading="流量趨勢">
    <nx-view-switch
      panel-actions
      label="趨勢指標"
      appearance="segment"
      [options]="metrics"
      [value]="metric()"
      (valueChange)="metric.set($event)"
    />
    <nx-trend-chart
      [label]="current().title"
      [points]="points()"
      [unit]="current().unit"
      granularity="interval"
    />
    <p>
      每點 10 秒，只含已完成的區間；串流不計入回應時間。{{
        metric() === 'p95' ? '沒有請求的區間不畫點。' : ''
      }}
    </p>
  </nx-panel>`,
})
export class MonitoringTraffic {
  private readonly store = inject(MonitoringStore);
  readonly metrics = METRICS;
  readonly metric = signal<string>('requests');
  readonly current = computed(() => METRICS.find((m) => m.value === this.metric()) ?? METRICS[0]);
  readonly points = computed(() => {
    const metric = this.metric();
    const points: { label: string; value: number }[] = [];
    for (const p of this.store.timeline()) {
      const value =
        metric === 'requests'
          ? p.requestsPerSecond
          : metric === 'p95'
            ? p.p95Ms
            : metric === 'errors'
              ? p.serverErrors
              : p.sentBytes / 10 / 1000;
      if (value !== null) points.push({ label: monitoringTime(p.at), value });
    }
    return points;
  });
}
