import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { ResourceAclDto } from '@core/api/schema';
import { AuthService } from '@core/auth/auth-service';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { ClientIssues } from '@core/errors/client-issues';
import { ProductTour, tourSteps } from '@shared/ui/product-tour';
import { CollaborationApi } from './collaboration-api';
import { ResourceSharing } from './resource-sharing';
import { resourceSharingTour } from './resource-sharing-tour';

const base = {
  noun: '專案',
  listName: '專案',
  sharingName: '分享',
  members: false,
  groups: false,
  testing: false,
};
const titles = (options: Partial<typeof base>) =>
  tourSteps(resourceSharingTour({ ...base, ...options })).map((step) => step.title);

describe('resourceSharingTour', () => {
  it('only explains searching and saving while the owner is the sole member', () => {
    expect(titles({})).toEqual(['誰能存取這個專案', '搜尋並加入成員', '具名成員', '儲存後才生效']);
    const [, , members] = tourSteps(resourceSharingTour(base));
    expect(members.description).toContain('目前只有你');
  });

  it('adds role, removal and group steps when they apply', () => {
    expect(titles({ members: true, groups: true })).toEqual([
      '誰能存取這個專案',
      '搜尋並加入成員',
      '具名成員',
      '可檢視與可編輯',
      '移除成員',
      '群組只能檢視',
      '儲存後才生效',
    ]);
  });

  it('uses the resource noun and feature names instead of fixed wording', () => {
    const steps = tourSteps(
      resourceSharingTour({
        ...base,
        noun: '知識庫',
        listName: '團隊知識',
        sharingName: '唯讀分享',
      }),
    );
    expect(steps[0].title).toBe('誰能存取這個知識庫');
    expect(steps[0].description).toContain('「唯讀分享」');
    expect(steps.at(-1)!.description).toContain('「團隊知識」');
  });

  it('warns that saving changes the tested user’s resource', () => {
    const save = tourSteps(resourceSharingTour({ ...base, testing: true })).at(-1)!;
    expect(save.description).toContain('該使用者');
  });

  it('scopes every target to the dialog host', () => {
    for (const step of tourSteps(resourceSharingTour({ ...base, members: true, groups: true })))
      expect(step.target).toMatch(/^nx-resource-sharing /);
  });
});

describe('resource sharing tour targets', () => {
  afterEach(() => {
    TestBed.resetTestingModule();
    vi.unstubAllGlobals();
  });

  async function render(acl: ResourceAclDto, allowGroups: boolean) {
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
          provide: CollaborationApi,
          useValue: {
            access: vi.fn(async () => acl),
            groups: vi.fn(async () => [{ id: 'g1', name: '教務處' }]),
          },
        },
        {
          provide: WorkspaceSession,
          useValue: {
            auth: { generation },
            testing: signal(false),
            load: vi.fn(async () => true),
            me: () => ({ id: 'me' }),
            featureName: (id: string) => id,
          },
        },
        { provide: AuthService, useValue: { generation, guard: AuthService.prototype.guard } },
        { provide: ProductTour, useValue: { loading: signal(false), active: signal(false) } },
        { provide: ClientIssues, useValue: {} },
      ],
    });
    const fixture = TestBed.createComponent(ResourceSharing);
    fixture.componentRef.setInput('kind', 'project');
    fixture.componentRef.setInput('resourceId', 'p1');
    fixture.componentRef.setInput('name', '新生輔導');
    fixture.componentRef.setInput('allowGroups', allowGroups);
    const settle = async () => {
      await TestBed.inject(ApplicationRef).whenStable();
      fixture.detectChanges();
    };
    await settle();
    fixture.componentInstance.open();
    await settle();
    return fixture;
  }

  function expectTargets(fixture: Awaited<ReturnType<typeof render>>) {
    const host = fixture.nativeElement as HTMLElement;
    expect(host.querySelector('nx-dialog-header nx-product-tour-button')).not.toBeNull();
    for (const step of tourSteps(fixture.componentInstance.tour()!)) {
      const selector = step.target.replace(/^nx-resource-sharing /, '');
      expect(host.querySelector(selector), step.target).not.toBeNull();
    }
  }

  it('finds every target when only the owner has access', async () => {
    const fixture = await render({ members: [], groupIds: [] }, false);
    expect(tourSteps(fixture.componentInstance.tour()!).map((step) => step.title)).not.toContain(
      '移除成員',
    );
    expectTargets(fixture);
  });

  it('finds every target with members and groups', async () => {
    const fixture = await render(
      {
        members: [{ userId: 'u1', account: 'wang', displayName: '王小明', role: 'editor' }],
        groupIds: ['g1'],
      },
      true,
    );
    const steps = tourSteps(fixture.componentInstance.tour()!).map((step) => step.title);
    expect(steps).toContain('移除成員');
    expect(steps).toContain('群組只能檢視');
    expectTargets(fixture);
  });
});
