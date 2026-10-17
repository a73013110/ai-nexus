import {
  afterNextRender,
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import type { EndpointTraffic, TrafficActivity } from '@core/api/schema';
import { below } from '@shared/browser/breakpoints';
import { formatNumber } from '@shared/browser/format';
import { mediaQuery } from '@shared/browser/media-query';
import { Icon } from '@shared/ui/icon';
import { Panel } from '@shared/ui/panel';
import { monitoringFeatureNames, monitoringLatency, monitoringTime } from '../monitoring-format';

/**
 * 最近的變更、錯誤與取消，旁邊是最忙碌的 API 路由。第一批列渲染後，新事件會落在最上方；
 * 第一次繪製時不會讓每一列都做動畫。
 */
@Component({
  selector: 'nx-monitoring-activity',
  imports: [Icon, Panel, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './monitoring-activity.scss',
  host: { '[class.is-settled]': 'settled()' },
  templateUrl: './monitoring-activity.html',
})
export class MonitoringActivity {
  readonly activities = input.required<TrafficActivity[]>();
  readonly endpoints = input.required<EndpointTraffic[]>();
  readonly canTrace = input(false);
  readonly phone = mediaQuery(below('phone'));
  readonly settled = signal(false);
  readonly time = monitoringTime;
  readonly latency = monitoringLatency;
  readonly featureName = monitoringFeatureNames();
  readonly number = formatNumber;
  readonly visible = computed(() => this.activities().slice(0, this.phone() ? 8 : 20));
  constructor() {
    afterNextRender(() => this.settled.set(true));
  }
}
