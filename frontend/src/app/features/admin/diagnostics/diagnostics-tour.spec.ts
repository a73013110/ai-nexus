import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideRouter } from '@angular/router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { DiagnosticDetail, DiagnosticPage, DiagnosticSummary } from '@core/api/schema';
import { AuthService } from '@core/auth/auth-service';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { ClientIssues } from '@core/errors/client-issues';
import { ProductTour, tourSteps, type ProductTourDefinition } from '@shared/ui/product-tour';
import { DiagnosticsApi } from './diagnostics-api';
import { logDetailTour, systemLogsTour } from './diagnostics-tour';
import { LogDetailDrawer } from './log-detail-drawer';
import { SystemLogsPage } from './system-logs-page';

const titles = (definition: ProductTourDefinition | null) =>
  definition ? tourSteps(definition).map((step) => step.title) : [];
const copy = (definition: ProductTourDefinition) =>
  tourSteps(definition)
    .map((step) => step.description)
    .join('\n');

const reader = { detail: false, export: false, degraded: false, monitoring: null, audit: null };
const operator = {
  detail: true,
  export: true,
  degraded: true,
  monitoring: '即時監控',
  audit: '活動稽核',
};

describe('systemLogsTour', () => {
  it('walks query, search, table and paging for summary-only readers', () => {
    expect(titles(systemLogsTour(reader))).toEqual([
      '日誌回答什麼',
      '查詢條件',
      '一個搜尋框查各種識別',
      '事件列表',
      '翻頁',
    ]);
    expect(copy(systemLogsTour(reader))).toContain('沒有詳情權限');
  });

  it('adds the degraded notice and export, and opens details when granted', () => {
    const definition = systemLogsTour(operator);
    expect(titles(definition)).toEqual([
      '日誌回答什麼',
      '結果可能不完整',
      '查詢條件',
      '一個搜尋框查各種識別',
      '事件列表',
      '匯出 CSV',
      '翻頁',
    ]);
    expect(copy(definition)).toContain('開啟診斷詳情');
  });

  it('names the monitoring and audit pages only when they are granted', () => {
    const granted = copy(systemLogsTour(operator));
    expect(granted).toContain('「即時監控」的日誌管線卡');
    expect(granted).toContain('「活動稽核」');
    const denied = copy(systemLogsTour({ ...operator, monitoring: null, audit: null }));
    expect(denied).not.toContain('監控');
    expect(denied).not.toContain('稽核');
  });

  it('describes the flow and audit lookup only when the event supports them', () => {
    expect(titles(logDetailTour({ exception: false, flow: false, audit: null }))).toEqual([
      '事件摘要',
      '事件訊息',
      '屬性、例外與流程',
      '關聯識別',
      '上一筆／下一筆',
    ]);
    const full = copy(logDetailTour({ exception: true, flow: true, audit: '活動稽核' }));
    expect(full).toContain('最多 50 筆');
    expect(full).toContain('跳到例外堆疊');
    expect(full).toContain('開啟活動稽核');
    const bare = copy(logDetailTour({ exception: false, flow: false, audit: null }));
    expect(bare).toContain('只列出它自己');
    expect(bare).not.toContain('例外型別');
    expect(bare).not.toContain('稽核');
  });

  it('scopes page steps to the page and drawer steps to the drawer', () => {
    for (const step of tourSteps(systemLogsTour(operator)))
      expect(step.target).toMatch(/^nx-system-logs-page /);
    for (const step of tourSteps(logDetailTour({ exception: true, flow: true, audit: 'x' })))
      expect(step.target).toMatch(/^nx-log-detail-drawer nx-detail-drawer /);
  });
});

const entry: DiagnosticSummary = {
  logId: '00000000-0000-0000-0000-0000000000a1',
  at: '2026-10-21T04:00:00Z',
  level: 'Error',
  category: 'AiNexus.Features.Work.Chat',
  eventId: 2001,
  eventName: 'retrieval.degraded',
  messageTemplate: 'Retrieval degraded {Reason}',
  issueCode: 'NX-0123456789ABCDEF0123456789ABCDEF',
  traceId: '0123456789abcdef0123456789abcdef',
  spanId: null,
  requestId: null,
  operationId: null,
  jobId: null,
  runId: null,
  attempt: null,
  method: 'POST',
  route: '/api/v1/chat',
  statusCode: 500,
  durationMs: 120,
  externalService: null,
  errorCode: null,
  instance: 'WEB-01',
  untrustedClient: false,
  message: 'Retrieval degraded fulltext',
};
const page: DiagnosticPage = {
  events: [entry],
  nextCursor: null,
  from: '2026-10-20T04:00:00Z',
  to: '2026-10-21T04:00:00Z',
  degraded: true,
};
const detail: DiagnosticDetail = {
  event: entry,
  service: 'AiNexus',
  environment: 'Development',
  version: '1.0.0',
  userId: null,
  propertiesJson: '{"Reason":"fulltext"}',
  exceptionType: 'System.InvalidOperationException',
  exceptionDetail: 'at Foo()',
};

describe('system logs tour targets', () => {
  afterEach(() => {
    TestBed.resetTestingModule();
    vi.unstubAllGlobals();
  });

  it('finds every page and drawer target once a page and a detail are shown', async () => {
    vi.stubGlobal('matchMedia', () => ({
      matches: false,
      addEventListener: () => undefined,
      removeEventListener: () => undefined,
    }));
    // jsdom 沒有 dialog.close() 與 scrollTo()；每次套用查詢頁面都會關閉抽屜並重設表格捲動。
    HTMLDialogElement.prototype.close ??= function (this: HTMLDialogElement) {
      this.removeAttribute('open');
    };
    Element.prototype.scrollTo ??= () => undefined;
    const generation = signal(1);
    const granted = new Set([
      'diagnostics.logs',
      'diagnostics.logs.detail',
      'diagnostics.logs.export',
      'monitoring',
      'audit',
    ]);
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        {
          provide: DiagnosticsApi,
          useValue: { list: vi.fn(async () => page), detail: vi.fn(async () => detail) },
        },
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
    const fixture = TestBed.createComponent(SystemLogsPage);
    const settle = async () => {
      await TestBed.inject(ApplicationRef).whenStable();
      fixture.detectChanges();
    };
    await settle();
    await settle();
    const component = fixture.componentInstance;
    const host = fixture.nativeElement as HTMLElement;
    expect(component.events()).toHaveLength(1);
    const expectTargets = (definition: ProductTourDefinition | null, prefix?: RegExp) => {
      expect(definition).not.toBeNull();
      for (const step of tourSteps(definition!))
        expect(
          host.querySelector(prefix ? step.target.replace(prefix, '') : step.target),
          step.target,
        ).not.toBeNull();
    };
    expect(host.querySelector('nx-feature-page nx-product-tour-button')).not.toBeNull();
    expect(titles(component.tour())).toContain('結果可能不完整');
    expectTargets(component.tour(), /^nx-system-logs-page /);

    // 直接設定選取，不經由 jsdom 的 dialog.show()；抽屜內容不論開關都在 DOM。
    component.selectedEntry.set(entry);
    await settle();
    await settle();
    expect(host.querySelector('nx-detail-drawer nx-product-tour-button')).not.toBeNull();
    const drawer = fixture.debugElement.query(By.directive(LogDetailDrawer))
      .componentInstance as LogDetailDrawer;
    // 抽屜宿主就在頁面宿主內，選擇器原樣查詢。
    expectTargets(drawer.tour());
    const drawerCopy = copy(drawer.tour()!);
    expect(drawerCopy).toContain('跳到例外堆疊');
    expect(drawerCopy).toContain('最多 50 筆');
    expect(drawerCopy).toContain('開啟audit');
  });
});
