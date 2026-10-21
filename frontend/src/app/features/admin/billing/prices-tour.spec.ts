import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { PriceDto } from '@core/api/schema';
import { AuthService } from '@core/auth/auth-service';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { ClientIssues } from '@core/errors/client-issues';
import { ProductTour, tourSteps } from '@shared/ui/product-tour';
import { BillingApi } from './billing-api';
import { PricesPage } from './prices-page';
import { priceVersionTour, pricesTour } from './prices-tour';

const titles = (definition: Parameters<typeof tourSteps>[0]) =>
  tourSteps(definition).map((step) => step.title);

describe('pricesTour', () => {
  it('covers unpriced items, effective times and adjusting on the current tab', () => {
    expect(titles(pricesTour({ view: 'current', testing: false }))).toEqual([
      '費用依當時的價格計算',
      '目前價格與價格歷史',
      '新增價格版本',
      '未定價的項目',
      '生效時間',
      '調整某一項',
    ]);
  });

  it('switches to version states and reusing on the history tab', () => {
    expect(titles(pricesTour({ view: 'history', testing: false }))).toEqual([
      '費用依當時的價格計算',
      '目前價格與價格歷史',
      '新增價格版本',
      '版本狀態',
      '帶入舊版本',
    ]);
  });

  it('drops the disabled write steps and explains read-only while testing an identity', () => {
    const current = tourSteps(pricesTour({ view: 'current', testing: true }));
    expect(current.map((step) => step.title)).toEqual([
      '費用依當時的價格計算',
      '目前價格與價格歷史',
      '未定價的項目',
      '生效時間',
    ]);
    expect(current[1].description).toContain('只能查看');
    expect(titles(pricesTour({ view: 'history', testing: true }))).toEqual([
      '費用依當時的價格計算',
      '目前價格與價格歷史',
      '版本狀態',
    ]);
  });

  it('scopes every target to the page', () => {
    for (const view of ['current', 'history'] as const)
      for (const step of tourSteps(pricesTour({ view, testing: false })))
        expect(step.target).toMatch(/^nx-prices-page /);
  });
});

describe('priceVersionTour', () => {
  it('says a copied version is saved as a new one', () => {
    const copied = tourSteps(priceVersionTour({ copied: true, testing: false }));
    const blank = tourSteps(priceVersionTour({ copied: false, testing: false }));
    expect(copied.map((step) => step.title)).toEqual([
      '計費對象',
      '幣別與費用類型',
      '費率的單位',
      '單次計費時機',
      '生效時間',
      '價格依據',
    ]);
    expect(copied[0].description).toContain('原版本保持原樣');
    expect(blank[0].description).toContain('原生識別碼');
  });

  it('notes saving is refused while testing an identity', () => {
    expect(tourSteps(priceVersionTour({ copied: false, testing: true }))[0].description).toContain(
      '不能儲存',
    );
  });

  it('scopes every target to the dialog', () => {
    for (const copied of [false, true])
      for (const step of tourSteps(priceVersionTour({ copied, testing: false })))
        expect(step.target).toMatch(/^nx-price-version-dialog /);
  });
});

describe('prices tour targets', () => {
  afterEach(() => {
    TestBed.resetTestingModule();
    vi.unstubAllGlobals();
  });

  it('finds every target on both tabs and in the dialog', async () => {
    vi.stubGlobal('matchMedia', () => ({
      matches: false,
      addEventListener: () => undefined,
      removeEventListener: () => undefined,
    }));
    // jsdom 沒有 showModal；這裡只需要對話框內容渲染出來。
    HTMLDialogElement.prototype.showModal ??= function (this: HTMLDialogElement) {
      this.open = true;
    };
    const generation = signal(1);
    const price: PriceDto = {
      id: 'p1',
      provider: 'google',
      modelId: 'gemma',
      currency: 'USD',
      kind: 'api',
      inputPerMillion: 0.1,
      cachedInputPerMillion: 0,
      outputPerMillion: 0.4,
      perRequest: 0,
      requestCharge: 'completed',
      effectiveAt: '2026-01-01T00:00:00Z',
      note: '',
      modelDisplayName: null,
    };
    TestBed.configureTestingModule({
      providers: [
        {
          provide: BillingApi,
          useValue: {
            prices: vi.fn(async () => [price]),
            targets: vi.fn(async () => [
              { provider: 'google', modelId: 'gemma', displayName: '雲端助理' },
              { provider: 'ollama', modelId: 'qwen3:8b', displayName: '本地助理' },
            ]),
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
            has: (feature: string) => feature === 'billing.prices',
            featureName: () => '模型與工具價格',
          },
        },
        { provide: AuthService, useValue: { generation, guard: AuthService.prototype.guard } },
        { provide: ProductTour, useValue: { loading: signal(false), active: signal(false) } },
        { provide: ClientIssues, useValue: {} },
      ],
    });
    const fixture = TestBed.createComponent(PricesPage);
    const settle = async () => {
      await TestBed.inject(ApplicationRef).whenStable();
      fixture.detectChanges();
    };
    await settle();
    const host = fixture.nativeElement as HTMLElement;
    const expectTargets = () => {
      for (const step of tourSteps(fixture.componentInstance.tour())) {
        const selector = step.target.replace(/^nx-prices-page /, '');
        expect(host.querySelector(selector), step.target).not.toBeNull();
      }
    };
    expect(host.querySelector('nx-product-tour-button')).not.toBeNull();
    expectTargets();
    const book = fixture.debugElement.query(
      (element) => element.name === 'nx-price-book',
    ).componentInstance;
    book.view.set('history');
    await settle();
    expect(titles(fixture.componentInstance.tour())).toContain('帶入舊版本');
    expectTargets();
    book.reuse(book.history()[0]);
    await settle();
    const dialog = host.querySelector('dialog.price-dialog')!;
    expect(dialog.querySelector('nx-product-tour-button')).not.toBeNull();
    for (const step of tourSteps(priceVersionTour({ copied: true, testing: false })))
      expect(host.querySelector(step.target), step.target).not.toBeNull();
  });
});
