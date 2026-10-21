import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { MonitoringSnapshot, OnlineSession } from '@core/api/schema';
import { AuthService } from '@core/auth/auth-service';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { ClientIssues } from '@core/errors/client-issues';
import { ProductTour, tourSteps, type ProductTourDefinition } from '@shared/ui/product-tour';
import { MonitoringApi } from './monitoring-api';
import { MonitoringPage } from './monitoring-page';
import { monitoringDrawerTour, monitoringTour } from './monitoring-tour';

const titles = (definition: ProductTourDefinition | null) =>
  definition ? tourSteps(definition).map((step) => step.title) : [];

const PAGE_TITLES = [
  '這個執行個體正常嗎',
  '一句話回答健康',
  '時間範圍',
  '自動更新',
  '匯出快照',
  '周邊服務',
  '子系統',
  '線上人員',
  '操作動態與熱門 API',
];

describe('monitoringTour', () => {
  it('walks the health, controls and every dashboard section', () => {
    expect(titles(monitoringTour({ refreshSeconds: 3, logs: null, audit: null }))).toEqual(
      PAGE_TITLES,
    );
  });

  it('states the server refresh interval once a snapshot arrives', () => {
    const step = (refreshSeconds: number | null) =>
      tourSteps(monitoringTour({ refreshSeconds, logs: null, audit: null })).find(
        (item) => item.title === '自動更新',
      )!.description;
    expect(step(5)).toContain('每 5 秒');
    expect(step(null)).toContain('每隔幾秒');
  });

  it('points to the log and audit pages only when they are granted', () => {
    const copy = (logs: string | null, audit: string | null) =>
      tourSteps(monitoringTour({ refreshSeconds: 3, logs, audit }))
        .map((step) => step.description)
        .join('\n');
    const granted = copy('系統日誌', '活動稽核');
    expect(granted).toContain('依 trace ID 開啟系統日誌');
    expect(granted).toContain('「活動稽核」');
    const denied = copy(null, null);
    expect(denied).not.toContain('trace ID');
    expect(denied).not.toContain('稽核');
  });

  it('adds the copyable identifiers and the pager only for sessions', () => {
    expect(titles(monitoringDrawerTour('session'))).toEqual([
      '狀態',
      '主要數據',
      '連線位址與識別',
      '上一筆／下一筆',
      '原始資料',
    ]);
    for (const kind of ['dependency', 'subsystem'] as const)
      expect(titles(monitoringDrawerTour(kind))).toEqual(['狀態', '主要數據', '原始資料']);
  });

  it('scopes every target to the page, and drawer steps to the drawer', () => {
    for (const step of tourSteps(monitoringTour({ refreshSeconds: 3, logs: 'x', audit: 'y' })))
      expect(step.target).toMatch(/^nx-monitoring-page /);
    for (const kind of ['session', 'dependency', 'subsystem'] as const)
      for (const step of tourSteps(monitoringDrawerTour(kind)))
        expect(step.target).toMatch(/^nx-monitoring-page nx-detail-drawer /);
  });
});

const metrics = {
  requests: 10,
  errors: 0,
  serverErrors: 0,
  cancelled: 0,
  receivedBytes: 0,
  sentBytes: 0,
  requestsPerSecond: 1,
  errorPercent: 0,
  averageMs: 10,
  p95Ms: 25,
};
const at = (index: number) => new Date(Date.UTC(2026, 9, 13, 4, 0, index * 10)).toISOString();
const onlineSession: OnlineSession = {
  id: 'tab-1',
  userId: '00000000-0000-0000-0000-000000000001',
  account: 'alice',
  displayName: 'Alice',
  authentication: 'local',
  address: '10.0.0.1',
  browser: 'Edge',
  device: 'Windows',
  feature: 'monitoring',
  state: 'active',
  connectedAt: at(0),
  lastSeenAt: at(3),
  lastAction: null,
  lastActionAt: null,
  requests: 3,
  errors: 0,
  receivedBytes: 0,
  sentBytes: 0,
  testingIdentity: false,
};
const snapshot: MonitoringSnapshot = {
  version: 2,
  enabled: true,
  instance: 'WEB-01',
  startedAt: at(0),
  at: at(3),
  windowMinutes: 5,
  refreshSeconds: 3,
  sessionTimeoutSeconds: 90,
  health: { status: 'healthy', issues: [] },
  onlineUsers: 1,
  onlineSessions: 1,
  activeSessions: 1,
  inFlightRequests: 0,
  openStreams: 0,
  omittedSessions: 0,
  droppedSessions: 0,
  resources: {
    cpuPercent: 4,
    workingSetBytes: 1,
    managedHeapBytes: 1,
    memoryLimitBytes: null,
    threadCount: 1,
    threadPoolThreads: 1,
    threadPoolQueueLength: 0,
    lockContentionsPerSecond: 0,
    gen0Collections: 0,
    gen1Collections: 0,
    gen2Collections: 0,
    gcPausePercent: null,
    uptimeSeconds: 300,
  },
  resourceTimeline: [],
  traffic: metrics,
  recentTraffic: metrics,
  timeline: [],
  sessions: [onlineSession],
  activities: [],
  dependencies: [
    {
      id: 'sql',
      name: '主資料庫',
      kind: 'database',
      status: 'healthy',
      inFlight: 0,
      lastSeenAt: at(3),
      metrics,
    },
  ],
  endpoints: [],
  subsystems: [
    { id: 'jobs', name: '背景工作', status: 'healthy', summary: null, facts: [], observedAt: null },
  ],
};

describe('monitoring tour targets', () => {
  afterEach(() => {
    TestBed.resetTestingModule();
    vi.unstubAllGlobals();
  });

  it('finds every page and drawer target once a snapshot is shown', async () => {
    vi.stubGlobal('matchMedia', () => ({
      matches: false,
      addEventListener: () => undefined,
      removeEventListener: () => undefined,
    }));
    const generation = signal(1);
    const granted = new Set(['monitoring', 'diagnostics.logs', 'audit']);
    TestBed.configureTestingModule({
      providers: [
        // 串流永遠不回應；快照由測試直接交給 store，如同收到第一幀。
        { provide: MonitoringApi, useValue: { events: () => new Promise(() => undefined) } },
        {
          provide: WorkspaceSession,
          useValue: {
            auth: { generation },
            testing: signal(false),
            me: signal(null),
            load: vi.fn(async () => true),
            has: (feature: string) => granted.has(feature),
            featureName: (feature: string) => feature,
          },
        },
        { provide: AuthService, useValue: { generation, guard: AuthService.prototype.guard } },
        { provide: ProductTour, useValue: { loading: signal(false), active: signal(false) } },
        { provide: ClientIssues, useValue: {} },
      ],
    });
    const fixture = TestBed.createComponent(MonitoringPage);
    const settle = async () => {
      await TestBed.inject(ApplicationRef).whenStable();
      fixture.detectChanges();
    };
    await settle();
    const page = fixture.componentInstance;
    page.store.receive(snapshot);
    await settle();
    const host = fixture.nativeElement as HTMLElement;
    const expectTargets = (definition: ProductTourDefinition | null) => {
      expect(definition).not.toBeNull();
      for (const step of tourSteps(definition!)) {
        const selector = step.target.replace(/^nx-monitoring-page /, '');
        expect(host.querySelector(selector), step.target).not.toBeNull();
      }
    };
    expect(host.querySelector('nx-feature-page nx-product-tour-button')).not.toBeNull();
    expect(page.tour()?.steps.at(-1)).toMatchObject({
      description: expect.stringContaining('trace ID'),
    });
    expectTargets(page.tour());
    expect(page.drawerTour()).toBeNull();

    // 直接設定選取，不經由 jsdom 的 dialog.show()；抽屜內容不論開關都在 DOM。
    for (const selection of [
      { kind: 'session', session: onlineSession },
      { kind: 'dependency', id: 'sql' },
      { kind: 'subsystem', id: 'jobs' },
    ] as const) {
      page.selection.set(selection);
      await settle();
      expect(host.querySelector('nx-detail-drawer nx-product-tour-button')).not.toBeNull();
      expectTargets(page.drawerTour());
    }
  });
});
