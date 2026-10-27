import { ApplicationRef, signal, type Type } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AuthService } from '@core/auth/auth-service';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { ClientIssues } from '@core/errors/client-issues';
import { ThemeService } from '@core/preferences/theme-service';
import { ProductTour, tourSteps, type ProductTourDefinition } from '@shared/ui/product-tour';
import type { AdminFeatureDto, AdminGroupDto, AdminRoleDto } from '@core/api/schema';
import { AccessControlApi } from './access-control-api';
import {
  accessEditorTour,
  accessPageNames,
  featuresTour,
  groupsTour,
  navigationItemTour,
  navigationTour,
  rolesTour,
} from './access-control-tour';
import { FeaturesPage } from './features-page';
import { GroupsPage } from './groups-page';
import { NavigationPage } from './navigation-page';
import { RolesPage } from './roles-page';
import { AccessEditorDialog } from './roles/access-editor-dialog';
import { NavigationItemDialog } from './navigation/navigation-item-dialog';

const names = accessPageNames((id) => `〔${id}〕`);
const titles = (definition: ProductTourDefinition) =>
  tourSteps(definition).map((step) => step.title);
const hosts = (definition: ProductTourDefinition, host: string) => {
  for (const step of tourSteps(definition))
    for (const part of step.target.split(','))
      expect(part.trim(), step.title).toMatch(new RegExp(`^${host} `));
};

describe('access control page tours', () => {
  it('explains roles as groups and points at the groups and users pages', () => {
    const steps = tourSteps(rolesTour({ names, assign: true }));
    expect(steps.map((step) => step.title)).toEqual([
      '角色是授權的入口',
      '新增角色',
      '組成的功能群組',
      '指派給使用者',
      '編輯角色',
    ]);
    expect(steps[0].description).toContain('〔access-control.groups〕');
    expect(steps[3].description).toContain('在「〔identity.users〕」頁');
  });

  it('says who assigns roles when the viewer cannot open the users page', () => {
    const steps = tourSteps(rolesTour({ names, assign: false }));
    expect(steps[3].description).toContain('由有「〔identity.users〕」頁授權的管理員');
  });

  it('links each concept to the next page in the chain', () => {
    expect(tourSteps(groupsTour({ names }))[0].description).toContain('〔access-control.roles〕');
    expect(tourSteps(featuresTour({ names }))[0].description).toContain(
      '〔access-control.groups〕',
    );
    const navigation = tourSteps(navigationTour({ names }));
    expect(navigation.map((step) => step.title)).toEqual([
      '選單是資料',
      '新增項目',
      '調整順序與層級',
      '每個人看到的不同',
      '更多操作',
      '編輯項目',
    ]);
    expect(navigation[0].description).toContain('不授予功能');
  });

  it('scopes every target to its page', () => {
    hosts(rolesTour({ names, assign: true }), 'nx-roles-page');
    hosts(groupsTour({ names }), 'nx-groups-page');
    hosts(featuresTour({ names }), 'nx-features-page');
    hosts(navigationTour({ names }), 'nx-navigation-page');
  });
});

describe('access control dialog tours', () => {
  const editor = (options: Partial<Parameters<typeof accessEditorTour>[0]>) =>
    accessEditorTour({
      names,
      kind: 'role',
      creating: false,
      models: false,
      ...options,
    });

  it('follows the kind being edited and the group tab', () => {
    expect(titles(editor({ kind: 'role' }))).toEqual([
      '名稱與啟用',
      '加入功能群組',
      '儲存後可用功能',
    ]);
    expect(titles(editor({ kind: 'group' }))).toEqual([
      '兩個頁籤一起儲存',
      '名稱與啟用',
      '功能授權',
      '資料範圍',
      '附件容量',
    ]);
    expect(titles(editor({ kind: 'group', models: true }))).toEqual([
      '兩個頁籤一起儲存',
      '模型與每日額度',
    ]);
    expect(titles(editor({ kind: 'feature' }))).toEqual(['名稱覆寫', '啟用功能']);
    expect(tourSteps(editor({ kind: 'role', creating: true }))[0].title).toBe('識別碼與名稱');
  });

  it('only offers kind and position while creating a navigation item', () => {
    const tour = (options: Partial<Parameters<typeof navigationItemTour>[0]>) =>
      titles(navigationItemTour({ names, kind: 'link', creating: false, ...options }));
    expect(tour({ creating: true })).toEqual([
      '選擇種類',
      '放在哪裡',
      '連結名稱',
      '網址與允許清單',
      '可見群組',
    ]);
    expect(tour({ kind: 'page' })).toEqual(['挑選頁面']);
    expect(tour({ kind: 'group' })).toEqual(['分群名稱']);
  });

  it('scopes every target to its dialog', () => {
    for (const kind of ['role', 'group', 'feature'] as const)
      for (const models of [false, true])
        hosts(editor({ kind, models, creating: true }), 'nx-access-editor-dialog');
    for (const kind of ['group', 'page', 'link'] as const)
      hosts(navigationItemTour({ names, kind, creating: true }), 'nx-navigation-item-dialog');
  });
});

describe('access control tour targets', () => {
  afterEach(() => {
    TestBed.resetTestingModule();
    vi.unstubAllGlobals();
  });

  const catalog = {
    roles: [{ id: 'member', name: '成員', enabled: true, groupIds: ['workspace'], userCount: 3 }],
    groups: [
      {
        id: 'workspace',
        name: '工作台',
        enabled: true,
        featureIds: ['chat', 'audit'],
        scopes: { audit: 'own' },
        policy: null,
      },
    ],
    features: [
      { id: 'chat', name: '聊天', enabled: true, icon: 'chat', route: '/work/chat' },
      { id: 'audit', name: '活動稽核', enabled: true, icon: 'audit', route: '/admin/audit' },
    ],
    models: [],
    navigationSections: [{ id: 'work', path: ['AI 工作台'], featureIds: ['chat', 'audit'] }],
  };
  const navigationItems = [
    {
      id: 'work',
      parentId: null,
      kind: 'group',
      featureId: null,
      name: 'AI 工作台',
      url: null,
      openInNewTab: true,
      icon: null,
      sortOrder: 10,
      audienceGroupIds: [],
    },
    {
      id: 'work.chat',
      parentId: 'work',
      kind: 'page',
      featureId: 'chat',
      name: null,
      url: null,
      openInNewTab: true,
      icon: null,
      sortOrder: 10,
      audienceGroupIds: [],
    },
  ];

  async function render<T>(page: Type<T>) {
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
    TestBed.configureTestingModule({
      providers: [
        {
          provide: AccessControlApi,
          useValue: {
            catalog: vi.fn(async () => catalog),
            navigationItems: vi.fn(async () => navigationItems),
          },
        },
        {
          provide: WorkspaceSession,
          useValue: {
            auth: { generation },
            me: signal(null),
            load: vi.fn(async () => true),
            has: () => true,
            featureName: (id: string) => id,
          },
        },
        { provide: AuthService, useValue: { generation, guard: AuthService.prototype.guard } },
        { provide: ThemeService, useValue: { reducedMotion: () => true } },
        { provide: ProductTour, useValue: { loading: signal(false), active: signal(false) } },
        { provide: ClientIssues, useValue: {} },
      ],
    });
    const fixture = TestBed.createComponent(page);
    const settle = async () => {
      await TestBed.inject(ApplicationRef).whenStable();
      fixture.detectChanges();
    };
    await settle();
    const host = fixture.nativeElement as HTMLElement;
    // 對話框開著時以它自己的 `tour()` 檢查，順便驗證接線。
    /** 頁面步驟以宿主標籤開頭，在 fixture 內查詢時去掉它；對話框步驟本來就在頁面之內。 */
    const expectTargets = (
      definition: ProductTourDefinition | null,
      prefix: RegExp | string = '',
    ) => {
      expect(definition).not.toBeNull();
      for (const step of tourSteps(definition!))
        expect(host.querySelector(step.target.replace(prefix, '')), step.target).not.toBeNull();
    };
    return { fixture, settle, host, expectTargets };
  }

  it('finds the roles page and role editor targets', async () => {
    const { fixture, settle, expectTargets } = await render(RolesPage);
    expectTargets(fixture.componentInstance.tour(), /^nx-roles-page /);
    const dialog = fixture.debugElement.query(By.directive(AccessEditorDialog))
      .componentInstance as AccessEditorDialog;
    dialog.open('role', catalog.roles[0] as unknown as AdminRoleDto);
    await settle();
    expectTargets(dialog.tour());
  });

  it('finds the groups page and both group editor tabs', async () => {
    const { fixture, settle, expectTargets } = await render(GroupsPage);
    expectTargets(fixture.componentInstance.tour(), /^nx-groups-page /);
    const dialog = fixture.debugElement.query(By.directive(AccessEditorDialog))
      .componentInstance as AccessEditorDialog;
    dialog.open('group', catalog.groups[0] as unknown as AdminGroupDto);
    await settle();
    expectTargets(dialog.tour());
    dialog.groupSection.set('models');
    await settle();
    expectTargets(dialog.tour());
  });

  it('finds the features page and feature editor targets', async () => {
    const { fixture, settle, expectTargets } = await render(FeaturesPage);
    expectTargets(fixture.componentInstance.tour(), /^nx-features-page /);
    const dialog = fixture.debugElement.query(By.directive(AccessEditorDialog))
      .componentInstance as AccessEditorDialog;
    dialog.open('feature', catalog.features[0] as unknown as AdminFeatureDto);
    await settle();
    expectTargets(dialog.tour());
  });

  it('finds the navigation page and item dialog targets', async () => {
    const { fixture, settle, expectTargets } = await render(NavigationPage);
    expectTargets(fixture.componentInstance.tour(), /^nx-navigation-page /);
    const dialog = fixture.debugElement.query(By.directive(NavigationItemDialog))
      .componentInstance as NavigationItemDialog;
    dialog.create('link', null);
    await settle();
    expectTargets(dialog.tour());
  });
});
