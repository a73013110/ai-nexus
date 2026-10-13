import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { Monitor, Network, Pause, Server } from 'lucide';
import type { OnlineSession } from '../../../core/api/schema';
import { WorkspaceSession } from '../../../core/auth/workspace-session';
import { safeMessage } from '../../../core/errors/safe-errors';
import { FeaturePage } from '../../../core/layout/feature-page';
import { downloadBlob } from '../../../shared/browser/download';
import { DetailDrawer } from '../../../shared/ui/detail-drawer';
import { EmptyState } from '../../../shared/ui/empty-state';
import { EXTRA_ICONS, Icon } from '../../../shared/ui/icon';
import { Notice } from '../../../shared/ui/notice';
import { ViewSwitch } from '../../../shared/ui/view-switch';
import { MonitoringActivity } from './monitoring-activity';
import { MonitoringHero } from './monitoring-hero';
import { MonitoringInspector } from './monitoring-inspector';
import { MonitoringIssues, type MonitoringTarget } from './monitoring-issues';
import { MonitoringPeople } from './monitoring-people';
import { MonitoringRuntime } from './monitoring-runtime';
import { MonitoringServiceMap } from './monitoring-service-map';
import { MonitoringStore } from './monitoring-store';
import { MonitoringSubsystems } from './monitoring-subsystems';
import { MonitoringTraffic } from './monitoring-traffic';
import { MonitoringVitals } from './monitoring-vitals';

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
    DetailDrawer,
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
  readonly windows = [
    { value: '1', label: '1 分鐘' },
    { value: '5', label: '5 分鐘' },
    { value: '15', label: '15 分鐘' },
  ];
  readonly drawerTitle = computed(() => DRAWER_TITLES[this.selection()?.kind ?? 'dependency']);
  readonly selectedSession = computed(() => {
    const selection = this.selection();
    return selection?.kind === 'session' ? selection.session : null;
  });
  readonly liveSession = computed(() => {
    const id = this.selectedSession()?.id;
    return (id && this.store.snapshot()?.sessions.find((s) => s.id === id)) || null;
  });
  readonly liveDependency = computed(() => {
    const selection = this.selection();
    return selection?.kind === 'dependency'
      ? (this.store.dependencies().find((d) => d.id === selection.id) ?? null)
      : null;
  });
  readonly subsystemId = computed(() => {
    const selection = this.selection();
    return selection?.kind === 'subsystem' ? selection.id : null;
  });
  readonly liveSubsystem = computed(() => {
    const id = this.subsystemId();
    return (id && this.store.subsystems().find((s) => s.id === id)) || null;
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
    this.drawer().open();
  }
  showSession(session: OnlineSession) {
    this.show({ kind: 'session', session });
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
      downloadBlob(await response.blob(), 'AI-Nexus-即時監控', 'json');
    } catch (error) {
      this.actionError.set(safeMessage(error));
    } finally {
      this.exporting.set(false);
    }
  }
}
