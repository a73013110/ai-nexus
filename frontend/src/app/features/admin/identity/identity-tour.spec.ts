import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { AdminUserDto } from '@core/api/schema';
import { AuthService } from '@core/auth/auth-service';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { ClientIssues } from '@core/errors/client-issues';
import { ProductTour, tourSteps } from '@shared/ui/product-tour';
import { AccessControlApi } from '@features/admin/access-control/access-control-api';
import { IdentityApi } from './identity-api';
import { directoryImportTour, userInspectorTour, usersTour } from './identity-tour';
import { UsersPage } from './users-page';

const titles = (definition: Parameters<typeof tourSteps>[0]) =>
  tourSteps(definition).map((step) => step.title);

describe('usersTour', () => {
  it('covers creating and row actions for administrators', () => {
    expect(titles(usersTour({ testing: false }))).toEqual([
      '管理身分與 AI 政策',
      '找到使用者',
      '新增使用者',
      '最近 30 天用量',
      '開啟使用者詳情',
      '測試身分與刪除',
    ]);
  });

  it('introduces the AD import only with the import grant and outside test identities', () => {
    expect(titles(usersTour({ testing: false, directoryImport: true }))).toContain('從 AD 匯入');
    expect(titles(usersTour({ testing: true, directoryImport: true }))).not.toContain('從 AD 匯入');
  });

  it('introduces AD import before creating and AD update with the sync grant', () => {
    const tour = usersTour({ testing: false, directoryImport: true, directorySync: true });
    expect(titles(tour)).toEqual([
      '管理身分與 AI 政策',
      '找到使用者',
      '從 AD 匯入',
      '新增使用者',
      '從 AD 更新',
      '最近 30 天用量',
      '開啟使用者詳情',
      '測試身分與刪除',
    ]);
    expect(tourSteps(tour).at(-1)?.description).toContain('「從 AD 更新」只更新這一位');
    expect(
      titles(usersTour({ testing: true, directoryImport: true, directorySync: true })),
    ).not.toContain('從 AD 更新');
    expect(tourSteps(usersTour({ testing: false })).at(-1)?.description).not.toContain(
      '從 AD 更新',
    );
  });

  it('drops the disabled actions and explains read-only while testing an identity', () => {
    const steps = tourSteps(usersTour({ testing: true }));
    expect(steps.map((step) => step.title)).toEqual([
      '管理身分與 AI 政策',
      '找到使用者',
      '最近 30 天用量',
      '開啟使用者詳情',
    ]);
    expect(steps.at(-1)?.description).toContain('唯讀');
  });

  it('scopes every target to the page', () => {
    for (const step of tourSteps(
      usersTour({ testing: false, directoryImport: true, directorySync: true }),
    ))
      expect(step.target).toMatch(/^nx-users-page /);
  });
});

describe('directoryImportTour', () => {
  it('follows the dialog from searching and picking to previewing and confirming', () => {
    expect(titles(directoryImportTour({ step: 'select' }))).toEqual([
      '搜尋 AD',
      '勾選要匯入的人',
      '跨搜尋累積',
      '先預覽',
    ]);
    expect(titles(directoryImportTour({ step: 'preview' }))).toEqual([
      '預覽摘要',
      'AD 已停用的帳號',
      '逐人結果',
      '確認匯入',
    ]);
  });

  it('explains what to do when someone could not be imported', () => {
    const steps = tourSteps(directoryImportTour({ step: 'result' }));
    expect(steps.map((step) => step.title)).toEqual(['有人未完成', '逐人結果']);
    expect(steps[0].description).toContain('重新預覽');
  });

  it('scopes every target to the dialog', () => {
    for (const step of ['select', 'preview', 'result'] as const)
      for (const item of tourSteps(directoryImportTour({ step })))
        expect(item.target).toMatch(/^nx-directory-import-dialog /);
  });
});

describe('userInspectorTour', () => {
  it('introduces the summary, tabs and pager outside the conversations tab', () => {
    expect(
      titles(userInspectorTour({ audit: true, testing: false, conversations: false })),
    ).toEqual(['這位使用者的概況', '反查活動稽核', '依頁籤管理', '上一位、下一位']);
  });

  it('omits the audit lookup without the audit grant', () => {
    expect(
      titles(userInspectorTour({ audit: false, testing: false, conversations: false })),
    ).not.toContain('反查活動稽核');
    expect(
      titles(userInspectorTour({ audit: false, testing: false, conversations: true })),
    ).not.toContain('反查活動稽核');
  });

  it('switches to the audited read-only view on the conversations tab', () => {
    expect(titles(userInspectorTour({ audit: true, testing: false, conversations: true }))).toEqual(
      [
        '受稽核的唯讀檢視',
        '搜尋對話',
        '包含已刪除對話',
        '切換對話',
        '完整的對話內容',
        '反查活動稽核',
      ],
    );
  });

  it('says the tabs are read-only while testing an identity', () => {
    const tabs = tourSteps(
      userInspectorTour({ audit: false, testing: true, conversations: false }),
    ).find((step) => step.title === '依頁籤管理');
    expect(tabs?.description).toContain('唯讀');
  });

  it('scopes every target to the inspector', () => {
    for (const conversations of [false, true])
      for (const step of tourSteps(
        userInspectorTour({ audit: true, testing: false, conversations }),
      ))
        expect(step.target).toMatch(/^nx-admin-user-inspector /);
  });
});

describe('users tour targets', () => {
  afterEach(() => {
    TestBed.resetTestingModule();
    vi.unstubAllGlobals();
  });

  it('finds every step with the AD import and update grants', async () => {
    vi.stubGlobal('matchMedia', (query: string) => ({
      matches: false,
      media: query,
      addEventListener() {},
      removeEventListener() {},
    }));
    // jsdom 沒有捲動；列表載入後表格會重設捲動位置。
    Element.prototype.scrollTo ??= function () {};
    const generation = signal(1);
    const user: AdminUserDto = {
      id: 'u1',
      account: 'TEST\\alice',
      displayName: 'Alice',
      lastSeenAt: '2026-10-01T00:00:00Z',
      roleIds: ['member'],
      activity: null,
      enabled: true,
      authentication: null,
      storage: null,
      email: null,
      emailSource: null,
      emailVerifiedAt: null,
    };
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        {
          provide: WorkspaceSession,
          useValue: {
            auth: { generation },
            load: vi.fn(async () => true),
            me: signal(null),
            has: (feature: string) => feature.startsWith('identity.users'),
            featureName: () => '使用者',
            testing: signal(false),
            testingNote: signal(''),
          },
        },
        { provide: AuthService, useValue: { generation, guard: AuthService.prototype.guard } },
        {
          provide: AccessControlApi,
          useValue: {
            catalog: vi.fn(async () => ({
              roles: [{ id: 'member', name: '成員' }],
              groups: [],
              features: [],
              models: [],
            })),
          },
        },
        {
          provide: IdentityApi,
          useValue: { users: vi.fn(async () => ({ users: [user], total: 1, offset: 0 })) },
        },
        { provide: ProductTour, useValue: { loading: signal(false), active: signal(false) } },
        { provide: ClientIssues, useValue: {} },
      ],
    });
    const fixture = TestBed.createComponent(UsersPage);
    await TestBed.inject(ApplicationRef).whenStable();
    fixture.detectChanges();
    await TestBed.inject(ApplicationRef).whenStable();
    fixture.detectChanges();
    const host = fixture.nativeElement as HTMLElement;
    const tour = fixture.componentInstance.tour();
    expect(titles(tour)).toContain('從 AD 更新');
    for (const step of tourSteps(tour))
      expect(
        host.querySelector(step.target.replace(/^nx-users-page /, '')),
        step.target,
      ).not.toBeNull();
  });
});
