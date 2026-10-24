import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { UnitImportPreviewDto } from '@core/api/schema';
import { AuthService } from '@core/auth/auth-service';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { ClientIssues } from '@core/errors/client-issues';
import { ProductTour, tourSteps } from '@shared/ui/product-tour';
import { OrganizationApi } from './organization-api';
import { OrganizationPage } from './organization-page';
import { organizationTour, unitDialogTour, unitImportTour } from './organization-tour';

const titles = (definition: Parameters<typeof tourSteps>[0]) =>
  tourSteps(definition).map((step) => step.title);

describe('organizationTour', () => {
  it('covers creating, tree actions, editing and members for administrators', () => {
    expect(titles(organizationTour({ testing: false }))).toEqual([
      '部門決定資料範圍',
      '新增單位',
      '瀏覽部門樹',
      '標「公文」的單位',
      '單位操作',
      '編輯、搬移與停用',
      '加入成員',
      '主要所屬與主管',
    ]);
  });

  it('adds the import step only with the import grant, read-only while testing', () => {
    expect(titles(organizationTour({ testing: false, canImport: true }))).toContain(
      '從公文系統匯入',
    );
    const testing = tourSteps(organizationTour({ testing: true, canImport: true }));
    expect(testing.find((step) => step.title === '從公文系統匯入')?.description).toContain(
      '不能套用',
    );
  });

  it('drops the disabled write steps and explains read-only while testing an identity', () => {
    const steps = tourSteps(organizationTour({ testing: true }));
    expect(steps.map((step) => step.title)).toEqual([
      '部門決定資料範圍',
      '瀏覽部門樹',
      '標「公文」的單位',
      '主要所屬與主管',
    ]);
    expect(steps.at(-1)?.description).toContain('只能查看');
  });

  it('scopes every target to the page', () => {
    for (const step of tourSteps(organizationTour({ testing: false, canImport: true })))
      expect(step.target).toMatch(/^nx-organization-page /);
  });
});

describe('unitDialogTour', () => {
  it('describes the parent as a move only when editing', () => {
    const editing = tourSteps(unitDialogTour({ editing: true, testing: false }));
    const creating = tourSteps(unitDialogTour({ editing: false, testing: false }));
    expect(editing.map((step) => step.title)).toEqual(['單位名稱', '搬到其他上層', '啟用與停用']);
    expect(creating.map((step) => step.title)).toEqual(['單位名稱', '放在哪個上層', '啟用與停用']);
    expect(editing[1].description).toContain('整棵下層跟著搬');
  });

  it('notes read-only while testing an identity', () => {
    expect(tourSteps(unitDialogTour({ editing: true, testing: true }))[0].description).toContain(
      '只能查看',
    );
  });

  it('warns first that an imported unit is reverted by the next import', () => {
    const steps = tourSteps(unitDialogTour({ editing: true, testing: false, imported: true }));
    expect(steps.map((step) => step.title)).toEqual([
      '下次匯入會改回',
      '單位名稱',
      '搬到其他上層',
      '啟用與停用',
    ]);
    expect(titles(unitDialogTour({ editing: true, testing: false }))).not.toContain(
      '下次匯入會改回',
    );
  });

  it('scopes every target to the dialog', () => {
    for (const editing of [false, true])
      for (const step of tourSteps(unitDialogTour({ editing, testing: false, imported: true })))
        expect(step.target).toMatch(/^nx-unit-dialog /);
  });
});

describe('unitImportTour', () => {
  it('walks the preview from the summary to applying', () => {
    expect(titles(unitImportTour({ testing: false }))).toEqual([
      '先看變動摘要',
      '以公文系統為準',
      '勾選要套用的部門',
      '略過的問題',
      '套用匯入',
    ]);
  });

  it('says applying is not possible while testing an identity', () => {
    expect(tourSteps(unitImportTour({ testing: true })).at(-1)?.description).toContain('不能套用');
  });

  it('scopes every target to the dialog', () => {
    for (const step of tourSteps(unitImportTour({ testing: false })))
      expect(step.target).toMatch(/^nx-unit-import-dialog /);
  });
});

describe('organization tour targets', () => {
  afterEach(() => {
    TestBed.resetTestingModule();
    vi.unstubAllGlobals();
  });

  it('finds every page and dialog target once a unit is selected', async () => {
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
    const preview: UnitImportPreviewDto = {
      fingerprint: 'f1',
      sourceUnits: 2,
      units: [
        {
          gdDepNo: 12,
          unitId: 'it',
          action: 'Update',
          path: ['總公司', '資訊處'],
          previousPath: null,
          enabled: true,
          fields: ['Name'],
        },
      ],
      managers: [
        {
          gdDepNo: 12,
          unitId: 'it',
          path: ['總公司', '資訊處'],
          userId: 'bob',
          account: 'TEST\\bob',
          displayName: 'Bob',
          added: true,
          deputy: false,
          joinsUnit: false,
        },
      ],
      issues: [{ gdDepNo: 30, name: '', code: 'NameMissing', gdUserId: null }],
    };
    const units = [
      {
        id: 'company',
        parentId: null,
        name: '總公司',
        enabled: true,
        sortOrder: 10,
        memberCount: 0,
        gdDepNo: null,
      },
      {
        id: 'it',
        parentId: 'company',
        name: '資訊處',
        enabled: true,
        sortOrder: 10,
        memberCount: 1,
        gdDepNo: 12,
      },
    ];
    TestBed.configureTestingModule({
      providers: [
        {
          provide: OrganizationApi,
          useValue: {
            units: vi.fn(async () => units),
            previewImport: vi.fn(async () => preview),
            members: vi.fn(async () => [
              {
                userId: 'bob',
                account: 'TEST\\bob',
                displayName: 'Bob',
                active: true,
                isPrimary: true,
                isManager: false,
              },
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
            has: (feature: string) =>
              feature === 'organization' || feature === 'organization.import',
            featureName: () => '組織',
          },
        },
        { provide: AuthService, useValue: { generation, guard: AuthService.prototype.guard } },
        { provide: ProductTour, useValue: { loading: signal(false), active: signal(false) } },
        { provide: ClientIssues, useValue: {} },
      ],
    });
    const fixture = TestBed.createComponent(OrganizationPage);
    const settle = async () => {
      await TestBed.inject(ApplicationRef).whenStable();
      fixture.detectChanges();
    };
    await settle();
    const page = fixture.componentInstance;
    page.store.select(page.store.units()[1]);
    await settle();
    const host = fixture.nativeElement as HTMLElement;
    for (const step of tourSteps(page.tour())) {
      const selector = step.target.replace(/^nx-organization-page /, '');
      expect(host.querySelector(selector), step.target).not.toBeNull();
    }
    page.editSelected();
    await settle();
    for (const step of tourSteps(unitDialogTour({ editing: true, testing: false, imported: true })))
      expect(host.querySelector(step.target), step.target).not.toBeNull();
    page.openImport();
    await settle();
    expect(host.querySelector('nx-unit-import-dialog nx-product-tour-button')).not.toBeNull();
    for (const step of tourSteps(unitImportTour({ testing: false })))
      expect(host.querySelector(step.target), step.target).not.toBeNull();
  });
});
