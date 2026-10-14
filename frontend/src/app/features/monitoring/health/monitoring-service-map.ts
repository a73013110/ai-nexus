import { ChangeDetectionStrategy, Component, computed, inject, output } from '@angular/core';
import { ThemeService } from '@core/preferences/theme-service';
import { formatNumber } from '@shared/browser/format';
import { Icon } from '@shared/ui/icon';
import { Panel } from '@shared/ui/panel';
import { StatusBadge } from '@shared/ui/status-badge';
import { MonitoringStore } from '../monitoring-store';
import { dependencyStatus, monitoringLatency, toneOf } from '../monitoring-format';

type Point = readonly [number, number];
const lerp = (a: Point, b: Point, t: number): Point => [
  a[0] + (b[0] - a[0]) * t,
  a[1] + (b[1] - a[1]) * t,
];
const d = (points: readonly Point[]) =>
  `M${points[0].join(' ')} C${points
    .slice(1)
    .map((p) => p.join(' '))
    .join(', ')}`;
/** A wire from the core's anchor (0, 50) to a row at `y`, and its tail from 18% on (de Casteljau). */
export function wire(y: number) {
  const curve: Point[] = [
    [0, 50],
    [50, 50],
    [50, y],
    [100, y],
  ];
  const t = 0.18;
  const a = lerp(curve[0], curve[1], t),
    b = lerp(curve[1], curve[2], t),
    c = lerp(curve[2], curve[3], t);
  const ab = lerp(a, b, t),
    bc = lerp(b, c, t);
  return { wire: d(curve), transit: d([lerp(ab, bc, t), bc, c, curve[3]]) };
}

/**
 * The application and the services it calls: one row per observed dependency, joined to the core
 * by hairlines. A wire carries a moving signal only while calls are in flight on live data;
 * dependencies never observed wait in a disclosure, since 「尚無觀測」 is not a state to watch.
 */
@Component({
  selector: 'nx-monitoring-service-map',
  imports: [Icon, Panel, StatusBadge],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './monitoring-service-map.html',
  styleUrls: ['./monitoring-service-map.scss', './monitoring-service-rows.scss'],
})
export class MonitoringServiceMap {
  readonly store = inject(MonitoringStore);
  private readonly theme = inject(ThemeService);
  readonly selected = output<string>();
  readonly status = dependencyStatus;
  readonly tone = toneOf;
  readonly latency = monitoringLatency;
  readonly snapshot = this.store.snapshot;
  readonly moving = computed(() => !this.store.stale() && !this.theme.reducedMotion());
  readonly observed = computed(() =>
    this.store.dependencies().filter((d) => d.status !== 'unobserved'),
  );
  readonly unobserved = computed(() =>
    this.store.dependencies().filter((d) => d.status === 'unobserved'),
  );
  private readonly count = computed(() => this.observed().length);
  /**
   * Wires depend on the number of rows only: rows share the column's height equally. Each leaves
   * the core's anchor and lands on its row level at both ends; the moving signal is the same curve
   * minus its first stretch, so dashes never pile up where the wires meet.
   */
  readonly paths = computed(() =>
    Array.from({ length: this.count() }, (_, i) => wire(((i + 0.5) * 100) / this.count())),
  );
  rate(value: number) {
    return value.toLocaleString('zh-TW', { maximumFractionDigits: value >= 10 ? 1 : 2 });
  }
  number(value: number) {
    return formatNumber(value);
  }
}
