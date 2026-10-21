import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { AuditCatalogDto, AuditDto } from '@core/api/schema';
import { AuthService } from '@core/auth/auth-service';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { ClientIssues } from '@core/errors/client-issues';
import { cursorPaginationStep } from '@shared/ui/data-workspace-tour';
import { ProductTour, tourSteps, type ProductTourDefinition } from '@shared/ui/product-tour';
import { ActivityAuditPage } from './activity-audit-page';
import { AuditApi } from './audit-api';
import { activityAuditTour, auditDetailTour } from './audit-tour';

const titles = (definition: ProductTourDefinition | null) =>
  definition ? tourSteps(definition).map((step) => step.title) : [];
const copy = (definition: ProductTourDefinition) =>
  tourSteps(definition)
    .map((step) => step.description)
    .join('\n');

describe('activityAuditTour', () => {
  it('walks category, query, table, export and paging', () => {
    expect(titles(activityAuditTour({ logs: null }))).toEqual([
      '稽核回答什麼',
      '依分類切換',
      '查詢條件',
      '活動紀錄',
      '匯出 CSV',
      '翻頁',
    ]);
  });

  it('points to system logs only when they are granted', () => {
    expect(copy(activityAuditTour({ logs: '系統日誌' }))).toContain('請看「系統日誌」');
    expect(copy(activityAuditTour({ logs: null }))).not.toContain('日誌');
  });

  it('describes fixed newest-first paging without a sort toggle', () => {
    const paging = tourSteps(activityAuditTour({ logs: null })).at(-1)!;
    expect(paging).toEqual(cursorPaginationStep('nx-activity-audit-page', { sortable: false }));
    expect(paging.description).toContain('由新到舊');
    expect(paging.description).not.toContain('排序');
  });

  it('adds changes, flow and the log link only when the record supports them', () => {
    expect(titles(auditDetailTour({ changes: false, flow: false, logs: null }))).toEqual([
      '紀錄摘要',
      '基本資訊',
      '原始紀錄',
      '上一筆／下一筆',
    ]);
    const full = auditDetailTour({ changes: true, flow: true, logs: '系統日誌' });
    expect(titles(full)).toEqual([
      '紀錄摘要',
      '前後差異',
      '基本資訊',
      '流程關聯',
      '原始紀錄',
      '上一筆／下一筆',
    ]);
    expect(copy(full)).toContain('開啟「系統日誌」');
    expect(copy(auditDetailTour({ changes: true, flow: true, logs: null }))).not.toContain('日誌');
  });

  it('scopes page steps to the page and drawer steps to the drawer', () => {
    for (const step of tourSteps(activityAuditTour({ logs: 'x' })))
      expect(step.target).toMatch(/^nx-activity-audit-page /);
    for (const step of tourSteps(auditDetailTour({ changes: true, flow: true, logs: 'x' })))
      expect(step.target).toMatch(/^nx-activity-audit-page nx-detail-drawer /);
  });
});

const entry: AuditDto = {
  id: 42,
  actor: 'alice',
  action: 'accesscontrol.role.save',
  resourceId: '00000000-0000-0000-0000-0000000000a1',
  result: 'saved',
  at: '2026-10-21T04:00:00Z',
  detailsJson: JSON.stringify({
    before: { allowedModelIds: ['a'] },
    after: { allowedModelIds: ['b'] },
  }),
  actingAs: null,
  category: 'admin',
  traceId: '0123456789abcdef0123456789abcdef',
  operationId: null,
  issueCode: null,
};
const catalog: AuditCatalogDto = {
  features: [],
  models: [],
  categories: [{ value: 'admin', label: '管理異動' }],
  actions: [{ value: 'accesscontrol.', label: '存取管理', categories: ['admin'] }],
  navigationSections: [],
};

describe('activity audit tour targets', () => {
  afterEach(() => {
    TestBed.resetTestingModule();
    vi.unstubAllGlobals();
  });

  it('finds every page and drawer target once a page and a record are shown', async () => {
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
    const granted = new Set(['audit', 'diagnostics.logs']);
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        {
          provide: AuditApi,
          useValue: { catalog: vi.fn(async () => catalog), query: vi.fn(async () => [entry]) },
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
    const fixture = TestBed.createComponent(ActivityAuditPage);
    const settle = async () => {
      await TestBed.inject(ApplicationRef).whenStable();
      fixture.detectChanges();
    };
    await settle();
    await settle();
    const component = fixture.componentInstance;
    const host = fixture.nativeElement as HTMLElement;
    expect(component.rows()).toHaveLength(1);
    // 元件宿主本身就是頁面標籤，查詢時去掉開頭的宿主選擇器。
    const expectTargets = (definition: ProductTourDefinition | null) => {
      expect(definition).not.toBeNull();
      for (const step of tourSteps(definition!))
        expect(
          host.querySelector(step.target.replace(/^nx-activity-audit-page /, '')),
          step.target,
        ).not.toBeNull();
    };
    expect(host.querySelector('nx-feature-page nx-product-tour-button')).not.toBeNull();
    expect(copy(component.tour()!)).toContain('「diagnostics.logs」');
    expectTargets(component.tour());
    expect(component.drawerTour()).toBeNull();

    // 直接設定選取，不經由 jsdom 的 dialog.show()；抽屜內容不論開關都在 DOM。
    component.selectedId.set(entry.id);
    await settle();
    expect(host.querySelector('nx-detail-drawer nx-product-tour-button')).not.toBeNull();
    expect(titles(component.drawerTour())).toEqual([
      '紀錄摘要',
      '前後差異',
      '基本資訊',
      '流程關聯',
      '原始紀錄',
      '上一筆／下一筆',
    ]);
    expect(copy(component.drawerTour()!)).toContain('開啟「diagnostics.logs」');
    expectTargets(component.drawerTour());
  });
});
