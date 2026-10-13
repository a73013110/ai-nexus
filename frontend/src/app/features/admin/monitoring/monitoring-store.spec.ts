import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it } from 'vitest';
import type { MonitoringSnapshot } from '../../../core/api/schema';
import { MonitoringApi } from './monitoring-api';
import { AnnouncementGate, MonitoringStore, readSnapshot } from './monitoring-store';

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
function snapshot(overrides: Partial<MonitoringSnapshot> = {}): MonitoringSnapshot {
  const at = (i: number) => new Date(Date.UTC(2026, 9, 13, 4, 0, i * 10)).toISOString();
  return {
    version: 2,
    enabled: true,
    instance: 'NEXUS-01',
    startedAt: at(0),
    at: at(30),
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
    resourceTimeline: [0, 1, 2].map((i) => ({
      at: at(i),
      cpuPercent: i,
      workingSetBytes: 1,
      managedHeapBytes: 1,
      threadPoolQueueLength: 0,
      gcPausePercent: null,
    })),
    traffic: metrics,
    recentTraffic: metrics,
    timeline: [0, 1, 2].map((i) => ({
      at: at(i),
      requestsPerSecond: i,
      errorPercent: 0,
      serverErrors: 0,
      averageMs: null,
      p95Ms: null,
      receivedBytes: 0,
      sentBytes: 0,
    })),
    sessions: [],
    activities: [],
    dependencies: [
      {
        id: 'sql',
        name: '主資料庫',
        kind: 'database',
        status: 'healthy',
        inFlight: 0,
        lastSeenAt: null,
        metrics,
      },
    ],
    endpoints: [],
    subsystems: [
      {
        id: 'jobs',
        name: '背景工作',
        status: 'healthy',
        summary: null,
        facts: [],
        observedAt: null,
      },
    ],
    ...overrides,
  };
}
/** A fresh copy, as every frame of the stream is. */
const copy = (value: MonitoringSnapshot) => JSON.parse(JSON.stringify(value)) as MonitoringSnapshot;

describe('readSnapshot', () => {
  it('accepts version 2 and fills what a newer server might leave out', () => {
    const value = snapshot() as Partial<MonitoringSnapshot>;
    delete value.recentTraffic;
    (value.subsystems![0] as { facts?: unknown }).facts = undefined;
    const read = readSnapshot(value);
    expect(read.recentTraffic).toEqual(metrics);
    expect(read.subsystems[0].facts).toEqual([]);
  });

  it('rejects other versions and malformed frames', () => {
    expect(() => readSnapshot(null)).toThrow('監控資料格式不正確');
    expect(() => readSnapshot({ ...snapshot(), version: 1 })).toThrow();
    expect(() => readSnapshot({ ...snapshot(), subsystems: undefined })).toThrow();
    expect(() => readSnapshot({ ...snapshot(), resourceTimeline: {} })).toThrow();
    expect(() => readSnapshot({ ...snapshot(), health: { status: 'healthy' } })).toThrow();
    expect(() => readSnapshot({ ...snapshot(), at: 'yesterday' })).toThrow();
  });
});

describe('AnnouncementGate', () => {
  it('stays silent for the first state and for flapping, then speaks once per settled change', () => {
    const gate = new AnnouncementGate();
    expect(gate.observe('healthy', '正常')).toBeNull();
    expect(gate.observe('healthy', '正常')).toBeNull();
    expect(gate.observe('attention', '注意')).toBeNull();
    expect(gate.observe('healthy', '正常')).toBeNull();
    expect(gate.observe('attention', '注意')).toBeNull();
    expect(gate.observe('attention', '注意')).toBe('注意');
    expect(gate.observe('attention', '注意')).toBeNull();
    expect(gate.observe('healthy', '正常')).toBeNull();
    expect(gate.observe('healthy', '正常')).toBe('正常');
  });
});

describe('MonitoringStore', () => {
  afterEach(() => TestBed.resetTestingModule());
  function store() {
    TestBed.configureTestingModule({
      providers: [MonitoringStore, { provide: MonitoringApi, useValue: {} }],
    });
    return TestBed.inject(MonitoringStore);
  }

  it('keeps slice references while their content is unchanged', () => {
    const monitoring = store();
    const first = snapshot();
    monitoring.receive(copy(first));
    const timeline = monitoring.timeline(),
      resources = monitoring.resourceTimeline(),
      health = monitoring.health(),
      dependencies = monitoring.dependencies(),
      subsystems = monitoring.subsystems();
    monitoring.receive({ ...copy(first), onlineUsers: 4 });
    expect(monitoring.snapshot()!.onlineUsers).toBe(4);
    expect(monitoring.timeline()).toBe(timeline);
    expect(monitoring.resourceTimeline()).toBe(resources);
    expect(monitoring.health()).toBe(health);
    expect(monitoring.dependencies()).toBe(dependencies);
    expect(monitoring.subsystems()).toBe(subsystems);

    const next = copy(first);
    next.timeline = [
      ...next.timeline.slice(1),
      { ...next.timeline[2], at: '2026-10-13T04:00:30Z' },
    ];
    next.dependencies[0].inFlight = 2;
    next.health = {
      status: 'attention',
      issues: [{ code: 'cpu_high', severity: 'warning', subject: 'cpu', title: 'CPU', detail: '' }],
    };
    monitoring.receive(next);
    expect(monitoring.timeline()).not.toBe(timeline);
    expect(monitoring.resourceTimeline()).toBe(resources);
    expect(monitoring.dependencies()).not.toBe(dependencies);
    expect(monitoring.health()).not.toBe(health);
    expect(monitoring.issueCount()).toBe(1);
    expect(monitoring.worstSeverity()).toBe('warning');
  });

  it('announces a health change only after it holds for two snapshots', () => {
    const monitoring = store();
    const attention = snapshot({
      health: {
        status: 'critical',
        issues: [
          {
            code: 'subsystem_failing',
            severity: 'critical',
            subject: 'jobs',
            title: '背景工作故障',
            detail: '',
          },
        ],
      },
    });
    monitoring.receive(snapshot());
    monitoring.receive(snapshot());
    monitoring.receive(attention);
    expect(monitoring.announcement()).toBe('');
    expect(monitoring.worstSeverity()).toBe('critical');
    monitoring.receive(attention);
    expect(monitoring.announcement()).toBe('系統有嚴重問題：背景工作故障');
  });

  it('treats data without a live stream, or older than four refreshes, as stale', () => {
    const monitoring = store();
    monitoring.receive(snapshot({ at: new Date().toISOString() }));
    expect(monitoring.stale()).toBe(true);
    monitoring.connection.set('live');
    expect(monitoring.stale()).toBe(false);
    monitoring.now.set(Date.now() + 16_000);
    expect(monitoring.stale()).toBe(true);
  });
});
