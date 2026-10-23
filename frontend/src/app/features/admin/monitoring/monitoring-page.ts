import { RefreshButton } from '@shared/ui/refresh-button';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { Monitor, Network, Pause, Server } from 'lucide';
import type { OnlineSession } from '@core/api/schema';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { safeMessage } from '@core/errors/safe-errors';
import { FeaturePage } from '@core/layout/feature-page';
import { downloadBlob } from '@shared/browser/download';
import { DetailDrawer } from '@shared/ui/detail-drawer';
import { DrawerPager } from '@shared/ui/drawer-pager';
import { EmptyState } from '@shared/ui/empty-state';
import { EXTRA_ICONS, Icon } from '@shared/ui/icon';
import { Notice } from '@shared/ui/notice';
import { StatusBadge } from '@shared/ui/status-badge';
import { ViewSwitch } from '@shared/ui/view-switch';
import { MonitoringActivity } from './traffic/monitoring-activity';
import { MonitoringHero } from './health/monitoring-hero';
import { MonitoringInspector, monitoringSummary } from './monitoring-inspector';
import { monitoringTime } from './monitoring-format';
import { MonitoringIssues, type MonitoringTarget } from './health/monitoring-issues';
import { MonitoringPeople } from './presence/monitoring-people';
import { MonitoringRuntime } from './health/monitoring-runtime';
import { MonitoringServiceMap } from './health/monitoring-service-map';
import { MonitoringStore } from './monitoring-store';
import { MonitoringSubsystems } from './health/monitoring-subsystems';
import { MonitoringTraffic } from './traffic/monitoring-traffic';
import { monitoringDrawerTour, monitoringTour } from './monitoring-tour';
import { MonitoringVitals } from './health/monitoring-vitals';

type Selection =
  | { kind: 'session'; session: OnlineSession }
  | { kind: 'dependency'; id: string }
  | { kind: 'subsystem'; id: string };

const DRAWER_TITLES = {
  session: '工作階段詳情',
  dependency: '周邊服務詳情',
  subsystem: '子系統詳情',
} as const;

@Component({
  selector: 'nx-monitoring-page',
  imports: [
    RefreshButton,
    DetailDrawer,
    DrawerPager,
    EmptyState,
    FeaturePage,
    Icon,
    MonitoringActivity,
    MonitoringHero,
    MonitoringInspector,
    MonitoringIssues,
    MonitoringPeople,
    MonitoringRuntime,
    MonitoringServiceMap,
    MonitoringSubsystems,
    MonitoringTraffic,
    MonitoringVitals,
    Notice,
    StatusBadge,
    ViewSwitch,
  ],
  providers: [
    MonitoringStore,
    {
      provide: EXTRA_ICONS,
      useValue: { monitor: Monitor, server: Server, network: Network, pause: Pause },
    },
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './monitoring-page.html',
  styleUrl: './monitoring-page.scss',
})
export class MonitoringPage {
  readonly session = inject(WorkspaceSession);
  readonly store = inject(MonitoringStore);
  readonly ready = signal(false);
  readonly featureFilter = signal('all');
  readonly selection = signal<Selection | null>(null);
  readonly exporting = signal(false);
  readonly actionError = signal('');
  private readonly drawer = viewChild.required(DetailDrawer);
  private readonly people = viewChild(MonitoringPeople);
  readonly time = monitoringTime;
  readonly windows = [
    { value: '1', label: '1 分鐘' },
    { value: '5', label: '5 分鐘' },
    { value: '15', label: '15 分鐘' },
  ];
  // 快照每幾秒換一次參考；只取間隔數字，導覽定義才不會跟著每份快照重建。
  private readonly refreshSeconds = computed(() => this.store.snapshot()?.refreshSeconds ?? null);
  /** 沒有監控授權時頁面只顯示空狀態，不提供導覽。 */
  readonly tour = computed(() =>
    this.ready() && this.session.has('monitoring')
      ? monitoringTour({
          refreshSeconds: this.refreshSeconds(),
          logs: this.session.has('diagnostics.logs')
            ? this.session.featureName('diagnostics.logs')
            : null,
          audit: this.session.has('audit') ? this.session.featureName('audit') : null,
        })
      : null,
  );
  readonly drawerTour = computed(() => {
    const kind = this.selection()?.kind;
    return kind ? monitoringDrawerTour(kind) : null;
  });
  readonly drawerTitle = computed(() => DRAWER_TITLES[this.selection()?.kind ?? 'dependency']);
  /** 檢視器是否開啟；退場時內容會保留。 */
  readonly drawerOpen = signal(false);
  readonly openSessionId = computed(() =>
    this.drawerOpen() ? (this.selectedSession()?.id ?? null) : null,
  );
  readonly selectedSession = computed(() => {
    const selection = this.selection();
    return selection?.kind === 'session' ? selection.session : null;
  });
  readonly liveSession = computed(() => {
    const id = this.selectedSession()?.id;
    return (id && this.store.snapshot()?.sessions.find((segment) => segment.id === id)) || null;
  });
  readonly liveDependency = computed(() => {
    const selection = this.selection();
    return selection?.kind === 'dependency'
      ? (this.store.dependencies().find((draft) => draft.id === selection.id) ?? null)
      : null;
  });
  readonly subsystemId = computed(() => {
    const selection = this.selection();
    return selection?.kind === 'subsystem' ? selection.id : null;
  });
  readonly liveSubsystem = computed(() => {
    const id = this.subsystemId();
    return (id && this.store.subsystems().find((segment2) => segment2.id === id)) || null;
  });
  readonly summary = computed(() =>
    monitoringSummary(
      this.selectedSession(),
      this.liveSession(),
      this.subsystemId(),
      this.liveSubsystem(),
      this.liveDependency(),
    ),
  );
  /** 線上人員表格目前這一頁的工作階段；上下筆只在這一頁內移動。 */
  readonly pageSessions = computed(() => this.people()?.list()?.visible() ?? []);
  readonly sessionIndex = computed(() => {
    const id = this.selectedSession()?.id;
    return id ? this.pageSessions().findIndex((segment3) => segment3.id === id) : -1;
  });
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
  private show(selection: Selection) {
    this.selection.set(selection);
    this.drawerOpen.set(true);
    this.drawer().open();
  }
  showSession(session: OnlineSession) {
    this.show({ kind: 'session', session });
  }
  showAdjacentSession(direction: -1 | 1) {
    const next = this.pageSessions()[this.sessionIndex() + direction];
    if (next) this.selection.set({ kind: 'session', session: next });
  }
  showDependency(id: string) {
    this.show({ kind: 'dependency', id });
  }
  showSubsystem(id: string) {
    this.show({ kind: 'subsystem', id });
  }
  showTarget(target: MonitoringTarget) {
    this.show(target);
  }
  async export() {
    this.exporting.set(true);
    this.actionError.set('');
    try {
      const response = await this.store.export();
      downloadBlob(await response.blob(), 'ai-nexus-即時監控', 'json');
    } catch (error) {
      this.actionError.set(safeMessage(error));
    } finally {
      this.exporting.set(false);
    }
  }
}
