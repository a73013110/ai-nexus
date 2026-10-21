import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AuthService } from '@core/auth/auth-service';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { WorkspaceApi } from '@core/api/workspace-api';
import { ClientIssues } from '@core/errors/client-issues';
import { ThemeService } from '@core/preferences/theme-service';
import { DraftRepository } from '@core/preferences/draft-repository';
import { UserSettingsService, defaultSettings } from '@core/preferences/user-settings';
import { ProductTour, tourSteps } from '@shared/ui/product-tour';
import { SettingsPage } from './settings-page';
import { SETTINGS_TABS } from './settings-tabs';
import { ownFeatureSections } from './tabs/account-access';
import { AccountSecurityApi } from './security/account-security-api';

const access = {
  roles: [{ id: 'members', name: '成員' }],
  groups: [],
  features: [
    { id: 'chat', name: '對話', route: '/work/chat', icon: 'chat' },
    { id: 'diagnostics.logs', name: '系統日誌', route: '/admin/diagnostics/logs', icon: 'logs' },
    { id: 'diagnostics.logs.export', name: '日誌匯出', route: '', icon: 'download' },
  ],
  navigation: [
    {
      id: 'work',
      kind: 'group',
      name: 'AI 工作台',
      icon: null,
      featureId: null,
      route: null,
      url: null,
      openInNewTab: false,
      children: [
        {
          id: 'chat-page',
          kind: 'page',
          name: '對話',
          icon: 'chat',
          featureId: 'chat',
          route: '/work/chat',
          url: null,
          openInNewTab: false,
          children: [],
        },
        {
          id: 'logs-page',
          kind: 'page',
          name: '系統日誌',
          icon: 'logs',
          featureId: 'diagnostics.logs',
          route: '/admin/diagnostics/logs',
          url: null,
          openInNewTab: false,
          children: [],
        },
      ],
    },
  ],
};

function setup({
  usage = vi.fn(async (): Promise<unknown> => null),
  policy = vi.fn(async (): Promise<unknown> => null),
} = {}) {
  const generation = signal(1);
  const session = {
    auth: { generation, session: signal({ mode: 'Ldap', method: 'ad' }), logout: vi.fn() },
    load: vi.fn(async () => true),
    me: signal({ id: 'u1', displayName: '王小明', account: 'wang', access }),
    has: (feature: string) => feature === 'chat',
    testing: signal(false),
  };
  const service = {
    load: vi.fn(async () => defaultSettings()),
    usage,
    policy,
    preview: vi.fn(),
    restore: vi.fn(),
    save: vi.fn(async (value) => value),
  };
  const api = {
    models: vi.fn(async () => null),
    webSearchStatus: vi
      .fn()
      .mockRejectedValueOnce(new Error('web search unavailable'))
      .mockResolvedValue({ available: true, notice: '' }),
  };
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      { provide: WorkspaceSession, useValue: session },
      { provide: AuthService, useValue: { guard: () => () => true, generation } },
      { provide: UserSettingsService, useValue: service },
      { provide: WorkspaceApi, useValue: api },
      { provide: DraftRepository, useValue: { clearAccount: vi.fn() } },
      { provide: ThemeService, useValue: { reducedMotion: signal(true) } },
      { provide: ProductTour, useValue: { loading: signal(false), active: signal(false) } },
      { provide: ClientIssues, useValue: {} },
      { provide: AccountSecurityApi, useValue: { get: vi.fn(async () => null) } },
    ],
  });
  const fixture = TestBed.createComponent(SettingsPage);
  const root = fixture.nativeElement as HTMLElement;
  const open = async (id: string) => {
    fixture.componentInstance.tabId.set(id);
    await fixture.whenStable();
  };
  const button = (name: string) =>
    [...root.querySelectorAll('button')].find((item) => item.textContent?.trim() === name);
  return { fixture, root, api, service, open, button };
}

describe('SettingsPage', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('describes every tab in its own words', () => {
    const descriptions = SETTINGS_TABS.map((tab) => tab.description);
    expect(new Set(descriptions).size).toBe(SETTINGS_TABS.length);
  });

  it('keeps a failed web search read inside the conversation tab and reads it again on reload', async () => {
    const { fixture, root, api, open, button } = setup();
    await fixture.whenStable();
    expect(root.querySelector('nx-notice')).toBeNull();
    await open('conversation');
    expect(api.webSearchStatus).toHaveBeenCalledOnce();
    expect(root.querySelector('nx-notice')).not.toBeNull();
    button('重新載入')!.click();
    await fixture.whenStable();
    expect(api.webSearchStatus).toHaveBeenCalledTimes(2);
    expect(root.textContent).toContain('網路搜尋 · 已啟用');
  });

  it('shows usage failures only on the usage tab', async () => {
    const { fixture, root, open } = setup({
      usage: vi.fn(async () => {
        throw new Error('usage unavailable');
      }),
    });
    await fixture.whenStable();
    expect(root.textContent).not.toContain('使用狀況暫時無法取得');
    await open('usage');
    expect(root.textContent).toContain('使用狀況暫時無法取得');
    await open('appearance');
    expect(root.textContent).not.toContain('使用狀況暫時無法取得');
  });

  it('shows the save bar on saved tabs, and on read-only tabs only while changes are unsaved', async () => {
    const { fixture, root, open } = setup();
    await fixture.whenStable();
    expect(root.querySelector('nx-dialog-save-bar')).not.toBeNull();
    await open('shortcuts');
    expect(root.querySelector('nx-dialog-save-bar')).toBeNull();
    fixture.componentInstance.store.update('enterToSend', false);
    await fixture.whenStable();
    expect(root.querySelector('nx-dialog-save-bar')).not.toBeNull();
  });

  it('saves through the save bar and reports it there', async () => {
    const { fixture, root, service, button } = setup();
    await fixture.whenStable();
    fixture.componentInstance.store.appearance('theme', 'dark');
    await fixture.whenStable();
    button('儲存變更')!.click();
    await fixture.whenStable();
    expect(service.save).toHaveBeenCalledOnce();
    expect(root.querySelector('.dialog-note')?.textContent).toContain('已儲存');
  });

  it('lists the shortcuts from the shared catalog, following the unsaved Enter setting', async () => {
    const { fixture, root, open } = setup();
    await open('shortcuts');
    expect(root.querySelectorAll('kbd').length).toBeGreaterThan(5);
    const send = () =>
      [...root.querySelectorAll('nx-setting-row')].find((row) =>
        row.textContent?.includes('送出訊息'),
      );
    expect(send()?.querySelectorAll('kbd').length).toBe(1);
    fixture.componentInstance.store.update('enterToSend', false);
    await fixture.whenStable();
    expect(send()?.querySelectorAll('kbd').length).toBe(2);
  });

  it('summarizes roles and features without the workspace navigation', async () => {
    const { root, open } = setup();
    await open('account');
    expect(root.querySelector('nx-workspace-navigation')).toBeNull();
    expect(root.textContent).toContain('成員');
    expect(root.textContent).toContain('日誌匯出');
  });
});

describe('settings tour targets', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('shows the tour button and finds every step on every tab', async () => {
    const storage = {
      usedBytes: 0,
      limitBytes: 1024,
      remainingBytes: 1024,
      personalLimitBytes: null,
      groupLimitBytes: null,
      defaultLimitBytes: 1024,
      limitSource: 'default',
    };
    const { fixture, root, open } = setup({
      usage: vi.fn(async () => ({
        days: 30,
        requests: 0,
        completed: 0,
        failed: 0,
        cancelled: 0,
        inputTokens: 0,
        outputTokens: 0,
        daily: [],
        storage,
        totalDurationMilliseconds: 0,
        timedRequests: 0,
        tokens: null,
      })),
      policy: vi.fn(async () => ({
        resetsAt: '2026-10-22T00:00:00Z',
        models: [],
        storedAttachmentLimitBytes: null,
      })),
    });
    await fixture.whenStable();
    expect(root.querySelector('.settings-tools nx-product-tour-button')).not.toBeNull();
    for (const tab of SETTINGS_TABS) {
      await open(tab.id);
      // 對話分頁的網路搜尋第一次讀取失敗（見 setup），重新載入後才有那一列。
      if (tab.id === 'conversation') {
        fixture.componentInstance.store.reload();
        await fixture.whenStable();
      }
      const steps = tourSteps(fixture.componentInstance.tour());
      expect(steps.length, tab.id).toBeGreaterThan(tab.saved ? 2 : 1);
      for (const step of steps)
        expect(
          root.querySelector(step.target.replace(/^nx-settings-page /, '')),
          step.target,
        ).not.toBeNull();
    }
  });
});

describe('ownFeatureSections', () => {
  it('groups features by the viewer’s own menu, with actions after their page', () => {
    expect(ownFeatureSections(access.navigation, access.features)).toEqual([
      {
        id: 'work',
        path: ['AI 工作台'],
        featureIds: ['chat', 'diagnostics.logs', 'diagnostics.logs.export'],
      },
    ]);
  });
});
