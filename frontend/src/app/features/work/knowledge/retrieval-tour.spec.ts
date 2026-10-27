import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AuthService } from '@core/auth/auth-service';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { ClientIssues } from '@core/errors/client-issues';
import { JobToasts } from '@features/work/jobs/job-toasts';
import { ProductTour, tourSteps } from '@shared/ui/product-tour';
import { KnowledgeApi } from './knowledge-api';
import { RetrievalPage } from './retrieval-page';
import { RetrievalAdmin } from './retrieval/retrieval-admin';
import { retrievalTour } from './retrieval-tour';

const titles = (definition: Parameters<typeof tourSteps>[0]) =>
  tourSteps(definition).map((step) => step.title);

describe('retrievalTour', () => {
  it('covers the index lifecycle, capabilities and the bench', () => {
    expect(titles(retrievalTour())).toEqual([
      'AI 回答的依據從這裡找',
      '檢索索引與覆蓋率',
      '重建索引',
      '啟用新索引',
      '清除退役向量',
      '服務能力',
      '驗證檢索模型',
      '檢索測試台',
      '命中結果',
    ]);
  });

  it('scopes every target to the page', () => {
    for (const step of tourSteps(retrievalTour()))
      expect(step.target).toMatch(/^nx-retrieval-page /);
  });
});

describe('retrieval tour targets', () => {
  afterEach(() => {
    TestBed.resetTestingModule();
    vi.unstubAllGlobals();
  });

  it('finds every target once indexes in each state and a result are shown', async () => {
    vi.stubGlobal('matchMedia', () => ({
      matches: false,
      addEventListener: () => undefined,
      removeEventListener: () => undefined,
    }));
    const generation = signal(1);
    const coverage = (complete: boolean) => ({
      ratio: complete ? 1 : 0.5,
      complete,
      completedChunks: complete ? 10 : 5,
      totalChunks: 10,
      pendingDocuments: complete ? 0 : 1,
    });
    const profile = (id: number, status: string, complete: boolean) => ({
      id,
      key: `k${id}`,
      provider: 'ollama',
      model: `embed-${id}`,
      dimensions: 768,
      status,
      createdAt: '2026-01-01T00:00:00Z',
      activatedAt: null,
      retiredAt: status === 'retired' ? '2026-02-01T00:00:00Z' : null,
      coverage: coverage(complete),
      job: null,
    });
    const connection = {
      provider: 'ollama',
      model: 'm',
      endpoint: '',
      available: null,
      notice: '',
    };
    TestBed.configureTestingModule({
      providers: [
        {
          provide: KnowledgeApi,
          useValue: {
            embeddingProfiles: vi.fn(async () => [
              profile(1, 'active', true),
              profile(2, 'building', false),
              profile(3, 'retired', true),
            ]),
            retrievalCapabilities: vi.fn(async () => ({
              sql: null,
              embedding: connection,
              rerank: connection,
            })),
            collections: vi.fn(async () => []),
          },
        },
        {
          provide: WorkspaceSession,
          useValue: {
            auth: { generation },
            me: signal(null),
            load: vi.fn(async () => true),
            has: (feature: string) => feature === 'knowledge.retrieval',
            featureName: () => '檢索管理',
          },
        },
        { provide: AuthService, useValue: { generation, guard: AuthService.prototype.guard } },
        { provide: JobToasts, useValue: {} },
        { provide: ProductTour, useValue: { loading: signal(false), active: signal(false) } },
        { provide: ClientIssues, useValue: {} },
      ],
    });
    const fixture = TestBed.createComponent(RetrievalPage);
    const settle = async () => {
      await TestBed.inject(ApplicationRef).whenStable();
      fixture.detectChanges();
    };
    await settle();
    const admin = fixture.debugElement.query((element) => element.name === 'nx-retrieval-admin')
      .componentInstance as RetrievalAdmin;
    // 沒有命中時仍會渲染結果區（顯示資料不足），不必準備閱讀器連結的依賴。
    admin.result.set({
      mode: 'hybrid',
      hits: [],
      rewriteMs: 0,
      embedMs: 0,
      searchMs: 0,
      rerankMs: 0,
    });
    await settle();
    const host = fixture.nativeElement as HTMLElement;
    expect(host.querySelector('nx-product-tour-button')).not.toBeNull();
    for (const step of tourSteps(fixture.componentInstance.tour)) {
      const selector = step.target.replace(/^nx-retrieval-page /, '');
      expect(host.querySelector(selector), step.target).not.toBeNull();
    }
  });
});
