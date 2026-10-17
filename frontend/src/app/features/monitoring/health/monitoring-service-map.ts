import { ChangeDetectionStrategy, Component, computed, inject, output } from '@angular/core';
import { ThemeService } from '@core/preferences/theme-service';
import { formatNumber } from '@shared/browser/format';
import { Icon } from '@shared/ui/icon';
import { Panel } from '@shared/ui/panel';
import { StatusBadge } from '@shared/ui/status-badge';
import { MonitoringStore } from '../monitoring-store';
import { dependencyStatus, monitoringLatency, toneOf } from '../monitoring-format';

type Point = readonly [number, number];
const lerp = (first: Point, second: Point, time: number): Point => [
  first[0] + (second[0] - first[0]) * time,
  first[1] + (second[1] - first[1]) * time,
];
const d = (points: readonly Point[]) =>
  `M${points[0].join(' ')} C${points
    .slice(1)
    .map((part) => part.join(' '))
    .join(', ')}`;
/** 從核心錨點（0, 50）到第 `y` 位置那一列的連線，以及從 18% 起的尾段（de Casteljau 演算法）。 */
export function wire(pointY: number) {
  const curve: Point[] = [
    [0, 50],
    [50, 50],
    [50, pointY],
    [100, pointY],
  ];
  const target = 0.18;
  const first2 = lerp(curve[0], curve[1], target),
    second2 = lerp(curve[1], curve[2], target),
    card = lerp(curve[2], curve[3], target);
  const ab = lerp(first2, second2, target),
    bc = lerp(second2, card, target);
  return { wire: d(curve), transit: d([lerp(ab, bc, target), bc, card, curve[3]]) };
}

/**
 * 應用程式與它呼叫的服務：每個觀察到的依賴一列，以細線連到核心。
 * 連線只有在即時資料上有呼叫進行中時才帶著移動的訊號；
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
    this.store.dependencies().filter((draft) => draft.status !== 'unobserved'),
  );
  readonly unobserved = computed(() =>
    this.store.dependencies().filter((draft2) => draft2.status === 'unobserved'),
  );
  private readonly count = computed(() => this.observed().length);
  /**
   * 連線只取決於列數：各列平分欄位高度。每條線從核心錨點出發，
   * 兩端都落在它自己那一列的高度上；移動的訊號與同一條曲線相同，只是略去起始的一段，
   * 因此虛線不會在連線相交處堆疊。
   */
  readonly paths = computed(() =>
    Array.from({ length: this.count() }, (_, index) => wire(((index + 0.5) * 100) / this.count())),
  );
  rate(value: number) {
    return value.toLocaleString('zh-TW', { maximumFractionDigits: value >= 10 ? 1 : 2 });
  }
  number(value: number) {
    return formatNumber(value);
  }
}
