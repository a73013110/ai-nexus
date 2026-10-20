import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import type { MonitoringSnapshot } from '@core/api/schema';
import { LiveDot } from '@shared/ui/live-dot';
import { ViewMotion } from '@shared/ui/view-motion';
import { MonitoringStore } from '../monitoring-store';
import {
  dependencyStatus,
  healthHeadline,
  isProblem,
  monitoringTime,
  subsystemStatus,
  toneOf,
  uptimeText,
} from '../monitoring-format';

const CONNECTION: Record<string, string> = {
  connecting: '正在連線',
  live: '即時連線',
  reconnecting: '正在重連',
  paused: '已暫停',
  hidden: '背景暫停',
  denied: '無法存取',
};

/**
 * 頁面的一眼答案：系統健康的一個標題、下一步、資料來源與新鮮度，以及每個依賴與子系統的一個點，
 * 這是頁面中唯一讀取一秒時鐘的部分。
 */
@Component({
  selector: 'nx-monitoring-hero',
  imports: [LiveDot, ViewMotion],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './monitoring-hero.scss',
  host: {
    role: 'region',
    'aria-labelledby': 'monitor-health-title',
    '[attr.data-tone]': 'headline().tone',
  },
  template: `<div class="hero-main">
      <div class="hero-heading">
        <nx-live-dot class="hero-dot" [tone]="headline().tone" [live]="live()" />
        <h2 id="monitor-health-title" [nxViewMotion]="headline().key">
          {{ headline().title }}
        </h2>
      </div>
      <p class="hero-detail">{{ headline().detail }}</p>
      <p class="hero-meta">
        <span>{{ data().instance }}</span
        ><span>已運作 {{ uptime() }}</span
        ><span>更新於 {{ time(data().at) }}</span
        ><span class="hero-connection" [class.is-live]="live()">{{ connection() }}</span>
      </p>
    </div>
    <dl class="hero-summary">
      @for (group of groups(); track group.key) {
        <div>
          <dt>{{ group.label }}</dt>
          <dd>
            <span class="hero-dots" aria-hidden="true">
              @for (dot of group.dots; track dot.id) {
                <i [attr.data-tone]="dot.tone" [attr.title]="dot.title"></i>
              }
            </span>
            <span [class.is-alert]="group.problems > 0">{{ group.text }}</span>
          </dd>
        </div>
      }
    </dl>
    <ng-content />
    <p class="sr-only" role="status" aria-live="polite">{{ store.announcement() }}</p>`,
})
export class MonitoringHero {
  readonly store = inject(MonitoringStore);
  readonly data = input.required<MonitoringSnapshot>();
  readonly time = monitoringTime;
  readonly headline = computed(
    () =>
      healthHeadline({
        enabled: this.data().enabled,
        health: this.store.health() ?? this.data().health,
        connection: this.store.connection(),
        stale: this.store.stale(),
        at: this.data().at,
        minutes: this.data().windowMinutes || this.store.minutes(),
      }),
    {
      equal: (first, second) =>
        first.key === second.key && first.title === second.title && first.detail === second.detail,
    },
  );
  readonly live = computed(
    () => this.data().enabled && this.store.connection() === 'live' && !this.store.stale(),
  );
  readonly connection = computed(() =>
    this.store.stale() && this.store.connection() === 'live'
      ? '資料已過期'
      : (CONNECTION[this.store.connection()] ?? ''),
  );
  readonly uptime = computed(() =>
    uptimeText((this.store.now() - Date.parse(this.data().startedAt)) / 1000),
  );
  readonly groups = computed(() => {
    const group = (
      key: string,
      label: string,
      items: readonly { id: string; name: string; status: string }[],
      words: (status: string) => string,
    ) => {
      const problems = items.filter((item) => isProblem(item.status));
      const healthy = items.filter((item) => item.status === 'healthy').length;
      return {
        key,
        label,
        problems: problems.length,
        dots: items.map((item) => ({
          id: item.id,
          tone: toneOf(item.status),
          title: `${item.name}：${words(item.status)}`,
        })),
        text: !items.length
          ? '尚未設定'
          : problems.length
            ? `${problems.length} 項異常`
            : healthy
              ? '沒有異常'
              : '尚無觀測',
      };
    };
    return [
      group('dependencies', '周邊服務', this.store.dependencies(), dependencyStatus),
      group('subsystems', '子系統', this.store.subsystems(), subsystemStatus),
    ];
  });
}
