import { Card } from '../../../shared/ui/card';
import { StatusBadge } from '../../../shared/ui/status-badge';
import { MonitoringSessions } from './monitoring-sessions';
import { MonitoringInspector } from './monitoring-inspector';
import { Notice } from '../../../shared/ui/notice';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { WorkspaceSession } from '../../../core/auth/workspace-session';
import { ThemeService } from '../../../core/preferences/theme-service';
import { FeaturePage } from '../../../shared/ui/feature-page';
import { Icon, EXTRA_ICONS } from '../../../shared/ui/icon';
import { Monitor, Server, Network } from 'lucide';
import { Select } from '../../../shared/ui/select';
import { TrendChart } from '../../../shared/ui/trend-chart';
import { DetailDrawer } from '../../../shared/ui/detail-drawer';
import { EmptyState } from '../../../shared/ui/empty-state';
import { formatBytes } from '../../../shared/browser/format';
import { downloadBlob } from '../../../shared/browser/download';
import { safeMessage } from '../../../core/api/safe-errors';
import { MonitoringStore, type OnlineSession, type DependencyTraffic } from './monitoring-store';
import { RuntimeTopology } from './runtime-topology';
import {
  dependencyStatus,
  monitoringTime,
  monitoringLatency,
  monitoringFeature,
} from './monitoring-format';

@Component({
  selector: 'nx-monitoring-page',
  imports: [
    Notice,
    FeaturePage,
    Icon,
    RouterLink,
    Select,
    TrendChart,
    DetailDrawer,
    EmptyState,
    RuntimeTopology,
    Card,
    StatusBadge,
    MonitoringSessions,
    MonitoringInspector,
  ],
  providers: [
    MonitoringStore,
    { provide: EXTRA_ICONS, useValue: { monitor: Monitor, server: Server, network: Network } },
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './monitoring-page.html',
  styleUrl: './monitoring-page.scss',
})
export class MonitoringPage {
  readonly session = inject(WorkspaceSession);
  readonly store = inject(MonitoringStore);
  readonly theme = inject(ThemeService);
  readonly ready = signal(false);
  readonly featureFilter = signal('all');
  readonly metric = signal('requests');
  readonly selectedSession = signal<OnlineSession | null>(null);
  readonly selectedDependency = signal<DependencyTraffic | null>(null);
  readonly exporting = signal(false);
  readonly actionError = signal('');
  private readonly drawer = viewChild.required(DetailDrawer);
  readonly bytes = formatBytes;
  readonly dependencyStatus = dependencyStatus;
  readonly time = monitoringTime;
  readonly latency = monitoringLatency;
  readonly windows = [
    { value: '1', label: '最近 1 分鐘' },
    { value: '5', label: '最近 5 分鐘' },
    { value: '15', label: '最近 15 分鐘' },
  ];
  readonly metrics = [
    { value: 'requests', label: 'API 請求速率' },
    { value: 'latency', label: '平均回應時間' },
    { value: 'bytes', label: 'HTTP 回應量' },
    { value: 'errors', label: '錯誤比例' },
  ];
  readonly chartPoints = computed(() =>
    (this.store.snapshot()?.timeline ?? []).map((p) => ({
      label: this.time(p.at),
      value:
        this.metric() === 'requests'
          ? p.requestsPerSecond
          : this.metric() === 'latency'
            ? (p.averageMs ?? 0)
            : this.metric() === 'errors'
              ? p.errorPercent
              : p.sentBytes / 10 / 1000,
    })),
  );
  readonly chartUnit = computed(
    () =>
      ({ requests: '次／秒', latency: 'ms', bytes: 'KB／秒', errors: '%' })[this.metric()] ?? '',
  );
  readonly chartTitle = computed(() => this.metrics.find((m) => m.value === this.metric())!.label);
  readonly moduleCounts = computed(() => {
    const counts = new Map<string, number>();
    for (const s of this.store.snapshot()?.sessions ?? [])
      counts.set(s.feature, (counts.get(s.feature) ?? 0) + 1);
    return [...counts]
      .sort((a, b) => b[1] - a[1])
      .slice(0, 6)
      .map(([feature, count]) => ({
        feature,
        count,
        percent: (100 * count) / Math.max(1, this.store.snapshot()?.onlineSessions ?? 0),
      }));
  });
  readonly stale = computed(
    () =>
      this.store.connection() !== 'live' ||
      this.store.now() - Date.parse(this.store.snapshot()?.at ?? '') >
        Math.max(15000, (this.store.snapshot()?.refreshSeconds ?? 3) * 4000),
  );
  readonly liveSession = computed(
    () => this.store.snapshot()?.sessions.find((s) => s.id === this.selectedSession()?.id) ?? null,
  );
  readonly liveDependency = computed(
    () =>
      this.store.snapshot()?.dependencies.find((d) => d.id === this.selectedDependency()?.id) ??
      null,
  );
  readonly statusText = computed(() =>
    this.stale() && this.store.connection() === 'live'
      ? '資料已過期'
      : {
          connecting: '正在連線',
          live: '即時連線',
          reconnecting: '正在重連',
          paused: '已暫停',
          hidden: '背景暫停',
          denied: '無法存取',
        }[this.store.connection()],
  );
  constructor() {
    void this.load();
  }
  private async load() {
    try {
      await this.session.load();
      if (this.session.has('monitoring')) this.store.start();
    } catch (error) {
      this.actionError.set(safeMessage(error));
    } finally {
      this.ready.set(true);
    }
  }
  readonly featureName = monitoringFeature;
  showSession(session: OnlineSession) {
    this.selectedDependency.set(null);
    this.selectedSession.set(session);
    this.drawer().open();
  }
  showDependency(dependency: DependencyTraffic) {
    this.selectedSession.set(null);
    this.selectedDependency.set(dependency);
    this.drawer().open();
  }
  async export() {
    this.exporting.set(true);
    this.actionError.set('');
    try {
      const response = await this.store.export();
      downloadBlob(await response.blob(), 'AI-Nexus-即時監控', 'json');
    } catch (error) {
      this.actionError.set(safeMessage(error));
    } finally {
      this.exporting.set(false);
    }
  }
}
