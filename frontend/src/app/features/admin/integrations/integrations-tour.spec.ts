import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { SourceDetailDto, SourceDto } from '@core/api/schema';
import { AuthService } from '@core/auth/auth-service';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { ClientIssues } from '@core/errors/client-issues';
import { ProductTour, tourSteps } from '@shared/ui/product-tour';
import { IntegrationsApi } from './integrations-api';
import { IntegrationsPage } from './integrations-page';
import { integrationsTour } from './integrations-tour';

const names = { chatName: '聊天', artifactsName: '成果' };
const titles = (options: Parameters<typeof integrationsTour>[0]) =>
  tourSteps(integrationsTour(options)).map((step) => step.title);

describe('integrationsTour', () => {
  it('walks through searching, reading and both hand-offs for a queryable source', () => {
    expect(titles({ queryable: true, chat: true, artifacts: true, ...names })).toEqual([
      '唯讀查詢公文與校務資料',
      '選擇來源',
      '搜尋標題或識別碼',
      '開啟一筆紀錄',
      '閱讀最新版本',
      '歷程與版本',
      '帶入對話',
      '儲存個人快照',
    ]);
  });

  it('drops hand-offs the account cannot use and names the destination pages', () => {
    expect(titles({ queryable: true, chat: false, artifacts: false, ...names })).not.toContain(
      '帶入對話',
    );
    const steps = tourSteps(
      integrationsTour({ queryable: true, chat: true, artifacts: true, ...names }),
    );
    expect(steps.find((step) => step.title === '帶入對話')!.description).toContain('「聊天」');
    expect(steps.find((step) => step.title === '儲存個人快照')!.description).toContain('「成果」');
  });

  it('explains why an unconfigured source cannot be queried', () => {
    expect(titles({ queryable: false, chat: true, artifacts: true, ...names })).toEqual([
      '唯讀查詢公文與校務資料',
      '選擇來源',
      '來源尚未可用',
    ]);
  });

  it('scopes every target to the page', () => {
    for (const queryable of [false, true])
      for (const step of tourSteps(
        integrationsTour({ queryable, chat: true, artifacts: true, ...names }),
      ))
        expect(step.target).toMatch(/^nx-integrations-page /);
  });
});

describe('integrations tour targets', () => {
  afterEach(() => {
    TestBed.resetTestingModule();
    vi.unstubAllGlobals();
  });

  it('finds every target for an open record and for an unavailable source', async () => {
    vi.stubGlobal('matchMedia', () => ({
      matches: false,
      addEventListener: () => undefined,
      removeEventListener: () => undefined,
    }));
    const generation = signal(1);
    const sources: SourceDto[] = [
      {
        id: 'gd',
        name: '公文系統',
        description: '公文',
        status: 'ready',
        notice: '',
        canQuery: true,
        kinds: ['document'],
      },
      {
        id: 'meiho',
        name: '校務系統',
        description: '校務',
        status: 'disabled',
        notice: '尚未設定連線。',
        canQuery: false,
        kinds: [],
      },
    ];
    const detail: SourceDetailDto = {
      sourceId: 'gd',
      record: {
        id: 'r1',
        kind: 'document',
        title: '公文一',
        status: '核准',
        revision: 'v1',
        modifiedAt: '2026-01-01T00:00:00Z',
      },
      body: '本文',
      history: [],
      historyLimited: false,
    };
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { queryParamMap: convertToParamMap({ source: 'gd', record: 'r1' }) },
          },
        },
        {
          provide: IntegrationsApi,
          useValue: {
            list: vi.fn(async () => sources),
            search: vi.fn(async () => [detail.record]),
            get: vi.fn(async () => detail),
          },
        },
        {
          provide: WorkspaceSession,
          useValue: {
            auth: { generation },
            testing: signal(false),
            testingNote: signal(''),
            me: signal(null),
            load: vi.fn(async () => true),
            has: () => true,
            featureName: (id: string) => id,
          },
        },
        { provide: AuthService, useValue: { generation, guard: AuthService.prototype.guard } },
        { provide: ProductTour, useValue: { loading: signal(false), active: signal(false) } },
        { provide: ClientIssues, useValue: {} },
      ],
    });
    const fixture = TestBed.createComponent(IntegrationsPage);
    const settle = async () => {
      await TestBed.inject(ApplicationRef).whenStable();
      fixture.detectChanges();
    };
    await settle();
    await settle();
    const host = fixture.nativeElement as HTMLElement;
    const expectTargets = () => {
      for (const step of tourSteps(fixture.componentInstance.tour())) {
        const selector = step.target.replace(/^nx-integrations-page /, '');
        expect(host.querySelector(selector), step.target).not.toBeNull();
      }
    };
    expect(host.querySelector('nx-product-tour-button')).not.toBeNull();
    expect(fixture.componentInstance.tour().steps.filter(Boolean)).toHaveLength(8);
    expectTargets();
    fixture.componentInstance.select(sources[1]);
    await settle();
    expect(tourSteps(fixture.componentInstance.tour()).map((step) => step.title)).toContain(
      '來源尚未可用',
    );
    expectTargets();
  });
});
