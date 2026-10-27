import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AuthService } from '@core/auth/auth-service';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { ClientIssues } from '@core/errors/client-issues';
import { ProductTour, tourSteps } from '@shared/ui/product-tour';
import { OperationsApi } from './operations-api';
import { UsagePage } from './usage-page';
import { usageTour } from './usage-tour';

const titles = (definition: Parameters<typeof tourSteps>[0]) =>
  tourSteps(definition).map((step) => step.title);

describe('usageTour', () => {
  it('covers period, figures, reporting, models, sources and services once there is usage', () => {
    expect(titles(usageTour({ empty: false, dashboard: '總覽', prices: '價格' }))).toEqual([
      '看全平台的 AI 用量',
      '固定最近 30 天',
      '四個摘要數字',
      'tokens 只計模型回報',
      '各模型使用狀況',
      '用量從哪裡來',
      '服務狀態',
    ]);
  });

  it('skips reporting and sources and explains the empty model table without usage', () => {
    const steps = tourSteps(usageTour({ empty: true, dashboard: '總覽', prices: null }));
    expect(steps.map((step) => step.title)).not.toContain('tokens 只計模型回報');
    expect(steps.map((step) => step.title)).not.toContain('用量從哪裡來');
    expect(steps.find((step) => step.title === '各模型使用狀況')?.description).toContain(
      '還沒有模型使用紀錄',
    );
  });

  it('points to costs by the granted page names only', () => {
    const coverage = (dashboard: string | null, prices: string | null) =>
      tourSteps(usageTour({ empty: false, dashboard, prices })).find(
        (step) => step.title === 'tokens 只計模型回報',
      )!.description;
    expect(coverage('我的總覽', '模型價格')).toContain('依「模型價格」');
    expect(coverage('我的總覽', '模型價格')).toContain('到「我的總覽」切換「整個平台」');
    expect(coverage('我的總覽', null)).toContain('依價格表');
    expect(coverage(null, '模型價格')).not.toContain('費用');
  });

  it('scopes every target to the page', () => {
    for (const empty of [false, true])
      for (const step of tourSteps(usageTour({ empty, dashboard: '總覽', prices: '價格' })))
        expect(step.target).toMatch(/^nx-usage-page /);
  });
});

describe('usage tour targets', () => {
  afterEach(() => {
    TestBed.resetTestingModule();
    vi.unstubAllGlobals();
  });

  it('renders the tour button and finds every target once usage loads', async () => {
    vi.stubGlobal('matchMedia', () => ({
      matches: false,
      addEventListener: () => undefined,
      removeEventListener: () => undefined,
    }));
    const generation = signal(1);
    const usage = {
      users: 3,
      activeUsers: 2,
      requests: 5,
      completed: 4,
      failed: 1,
      cancelled: 0,
      inputTokens: 100,
      outputTokens: 50,
      requestsWithUsage: 4,
      totalDurationMilliseconds: 2000,
      timedRequests: 5,
      storedBytes: 1024,
      storedFiles: 1,
      models: [
        {
          modelId: 'm',
          requests: 5,
          failed: 1,
          requestsWithUsage: 4,
          inputTokens: 100,
          outputTokens: 50,
          durationMilliseconds: 2000,
        },
      ],
      kinds: [{ kind: 'chat', requests: 5, inputTokens: 100, outputTokens: 50 }],
      providers: [{ id: 'openai', available: true, notice: '' }],
      webSearch: { available: false, notice: '尚未設定' },
      since: '2026-09-22T00:00:00Z',
      until: '2026-10-21T00:00:00Z',
    };
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: OperationsApi, useValue: { usage: vi.fn(async () => usage) } },
        {
          provide: WorkspaceSession,
          useValue: {
            auth: { generation },
            me: signal(null),
            load: vi.fn(async () => true),
            has: (feature: string) => ['operations.usage', 'dashboard'].includes(feature),
            featureName: (id: string) => (id === 'dashboard' ? '總覽' : '平台用量'),
          },
        },
        { provide: AuthService, useValue: { generation, guard: AuthService.prototype.guard } },
        { provide: ProductTour, useValue: { loading: signal(false), active: signal(false) } },
        { provide: ClientIssues, useValue: {} },
      ],
    });
    const fixture = TestBed.createComponent(UsagePage);
    await TestBed.inject(ApplicationRef).whenStable();
    fixture.detectChanges();
    const host = fixture.nativeElement as HTMLElement;
    const page = fixture.componentInstance;
    expect(host.querySelector('nx-product-tour-button')).not.toBeNull();
    const steps = tourSteps(page.tour());
    expect(steps.map((step) => step.title)).toContain('用量從哪裡來');
    for (const step of steps) {
      const selector = step.target.replace(/^nx-usage-page /, '');
      expect(host.querySelector(selector), step.target).not.toBeNull();
    }
  });
});
