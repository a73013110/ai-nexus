import { computed, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeAll, describe, expect, it, vi } from 'vitest';
import type { NavigationItemDto } from '@core/api/schema';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { ApiError } from '@core/errors/safe-errors';
import { publicErrorHints } from '@core/errors/public-error-catalog';
import { ConfirmDialog } from '@shared/ui/confirm-dialog';
import { AccessControlApi } from '../access-control-api';
import { AccessControlStore } from '../access-control-store';
import { NavigationStore } from '../navigation-store';
import { NavigationItemDialog, wellFormedLink } from './navigation-item-dialog';

const items: NavigationItemDto[] = [
  {
    id: 'system',
    parentId: null,
    kind: 'group',
    featureId: null,
    name: '系統管理',
    url: null,
    openInNewTab: true,
    icon: null,
    sortOrder: 10,
    audienceGroupIds: [],
  },
  {
    id: 'wiki',
    parentId: 'system',
    kind: 'link',
    featureId: null,
    name: '維運手冊',
    url: 'https://wiki.example.com/ops',
    openInNewTab: false,
    icon: 'globe',
    sortOrder: 10,
    audienceGroupIds: ['administrators'],
  },
];

function setup() {
  const api = {
    createNavigationItem: vi.fn(async () => undefined),
    updateNavigationItem: vi.fn(async () => undefined),
  };
  const store = {
    items: signal(items),
    toast: { success: vi.fn() },
    changed: vi.fn(),
    name: (item: NavigationItemDto) => item.name ?? item.featureId ?? '',
  };
  TestBed.configureTestingModule({
    providers: [
      { provide: AccessControlApi, useValue: api },
      { provide: NavigationStore, useValue: store },
      {
        provide: AccessControlStore,
        useValue: {
          catalog: computed(() => ({
            features: [{ id: 'chat', name: '聊天', enabled: false }],
            groups: [{ id: 'administrators', name: '平台管理', enabled: true }],
          })),
        },
      },
      { provide: WorkspaceSession, useValue: { testing: signal(false), testingNote: () => '' } },
    ],
  });
  // 測試的是對話框的邏輯；範本的共用元件需要 jsdom 沒有的版面 API。
  TestBed.overrideComponent(NavigationItemDialog, {
    set: {
      template: '<dialog #itemDialog></dialog><nx-confirm-dialog />',
      imports: [ConfirmDialog],
    },
  });
  const fixture = TestBed.createComponent(NavigationItemDialog);
  fixture.detectChanges();
  return { dialog: fixture.componentInstance, api, store };
}

describe('NavigationItemDialog', () => {
  beforeAll(() => {
    HTMLDialogElement.prototype.showModal ??= function (this: HTMLDialogElement) {
      this.setAttribute('open', '');
    };
    HTMLDialogElement.prototype.close ??= function (this: HTMLDialogElement) {
      this.removeAttribute('open');
    };
  });
  afterEach(() => TestBed.resetTestingModule());

  it('accepts only absolute https links without credentials', () => {
    expect(wellFormedLink('https://wiki.example.com/a?b=1')).toBe(true);
    expect(wellFormedLink('http://wiki.example.com/')).toBe(false);
    expect(wellFormedLink('https://user:secret@wiki.example.com/')).toBe(false);
    expect(wellFormedLink('/work/chat')).toBe(false);
    expect(wellFormedLink('javascript:alert(1)')).toBe(false);
  });

  it('offers catalog pages with their overridden names and groups by path as places', () => {
    const { dialog } = setup();
    const chat = dialog.pages().find((option) => option.value === 'chat');
    expect(chat?.label).toBe('聊天（停用）');
    expect(dialog.pages().some((option) => option.value === 'diagnostics.logs.export')).toBe(false);
    expect(dialog.parents().map((node) => [node.value, node.label, node.parent])).toEqual([
      ['system', '系統管理', null],
    ]);
  });

  it('creates a page with only its feature, in the chosen group', async () => {
    const { dialog, api, store } = setup();
    dialog.create('page', 'system');
    await dialog.save(new Event('submit'));
    expect(dialog.fieldError()?.field).toBe('featureId');
    expect(api.createNavigationItem).not.toHaveBeenCalled();
    dialog.update('featureId', 'chat');
    dialog.update('name', '不該送出');
    await dialog.save(new Event('submit'));
    expect(api.createNavigationItem).toHaveBeenCalledWith({
      kind: 'page',
      parentId: 'system',
      featureId: 'chat',
      name: null,
      url: null,
      openInNewTab: true,
      icon: null,
      audienceGroupIds: null,
    });
    expect(store.toast.success).toHaveBeenCalledWith('已新增頁面', '側欄選單已更新。');
    expect(store.changed).toHaveBeenCalled();
  });

  it('checks a link before sending it and keeps the input when the server refuses its host', async () => {
    const { dialog, api, store } = setup();
    dialog.create('link');
    dialog.update('name', ' 外部 ');
    dialog.update('url', 'http://wiki.example.com/');
    await dialog.save(new Event('submit'));
    expect(dialog.fieldError()?.field).toBe('url');
    dialog.update('url', 'https://other.example.com/');
    expect(dialog.fieldError()).toBeNull();
    dialog.update('audienceGroupIds', ['administrators']);
    api.createNavigationItem.mockRejectedValueOnce(
      new ApiError(400, 'navigation_link_host_not_allowed', 'x'),
    );
    await dialog.save(new Event('submit'));
    expect(api.createNavigationItem).toHaveBeenCalledWith({
      kind: 'link',
      parentId: null,
      featureId: null,
      name: '外部',
      url: 'https://other.example.com/',
      openInNewTab: true,
      icon: null,
      audienceGroupIds: ['administrators'],
    });
    expect(dialog.error()).toContain(publicErrorHints['navigation_link_host_not_allowed']);
    expect(dialog.editor()!.draft.url).toBe('https://other.example.com/');
    expect(store.changed).not.toHaveBeenCalled();
  });

  it('edits a link in place without moving it and only when something changed', async () => {
    const { dialog, api } = setup();
    dialog.edit(items[1]);
    expect(dialog.editor()!.title).toBe('編輯外部連結：維運手冊');
    await dialog.save(new Event('submit'));
    expect(api.updateNavigationItem).not.toHaveBeenCalled();
    dialog.update('openInNewTab', true);
    expect(dialog.dirty()).toBe(true);
    await dialog.save(new Event('submit'));
    expect(api.updateNavigationItem).toHaveBeenCalledWith('wiki', {
      kind: 'link',
      featureId: null,
      name: '維運手冊',
      url: 'https://wiki.example.com/ops',
      openInNewTab: true,
      icon: 'globe',
      audienceGroupIds: ['administrators'],
    });
  });

  it('requires a name for a group', async () => {
    const { dialog, api } = setup();
    dialog.create('group');
    await dialog.save(new Event('submit'));
    expect(dialog.fieldError()).toEqual({ field: 'name', message: '請輸入 120 字元內的名稱。' });
    dialog.update('name', '報表');
    dialog.update('icon', 'chart');
    await dialog.save(new Event('submit'));
    expect(api.createNavigationItem).toHaveBeenCalledWith(
      expect.objectContaining({ kind: 'group', name: '報表', icon: 'chart', url: null }),
    );
  });
});
