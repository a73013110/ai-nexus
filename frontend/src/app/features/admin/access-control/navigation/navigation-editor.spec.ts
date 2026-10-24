import { computed, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { NavigationItemDto } from '@core/api/schema';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { ApiError } from '@core/errors/safe-errors';
import { publicErrorHints } from '@core/errors/public-error-catalog';
import { ThemeService } from '@core/preferences/theme-service';
import { ConfirmDialog } from '@shared/ui/confirm-dialog';
import { MenuPanel } from '@shared/ui/menu-panel';
import { AccessControlApi } from '../access-control-api';
import { AccessControlStore } from '../access-control-store';
import { NavigationStore } from '../navigation-store';
import { NavigationEditor } from './navigation-editor';

const item = (
  id: string,
  parentId: string | null,
  sortOrder: number,
  kind = 'page',
  extra: Partial<NavigationItemDto> = {},
): NavigationItemDto => ({
  id,
  parentId,
  kind,
  featureId: kind === 'page' ? id : null,
  name: kind === 'page' ? null : id,
  url: kind === 'link' ? 'https://wiki.example.com/' : null,
  openInNewTab: true,
  icon: null,
  sortOrder,
  audienceGroupIds: [],
  ...extra,
});
const seed = [
  item('dashboard', null, 10),
  item('work', null, 20, 'group', { name: 'AI 工作台' }),
  item('chat', 'work', 10),
  item('files', 'work', 20),
  item('system', null, 30, 'group', { name: '系統管理' }),
  item('audit', 'system', 10),
  item('wiki', 'system', 20, 'link', { name: '維運手冊', audienceGroupIds: ['administrators'] }),
];

function setup(testing = false) {
  const items = signal(seed.map((entry) => ({ ...entry })));
  const api = {
    orderNavigationItems: vi.fn(async () => undefined),
    deleteNavigationItem: vi.fn(async () => undefined),
  };
  const store = {
    items,
    actionError: signal(''),
    toast: { success: vi.fn() },
    replace: vi.fn((next: NavigationItemDto[]) => items.set(next)),
    changed: vi.fn(),
    name: (entry: NavigationItemDto) => entry.name ?? entry.featureId ?? '',
    disabled: () => false,
  };
  TestBed.configureTestingModule({
    providers: [
      { provide: AccessControlApi, useValue: api },
      { provide: NavigationStore, useValue: store },
      {
        provide: AccessControlStore,
        useValue: {
          catalog: computed(() => ({ groups: [{ id: 'administrators', name: '平台管理' }] })),
        },
      },
      { provide: WorkspaceSession, useValue: { testing: signal(testing) } },
      { provide: ThemeService, useValue: { reducedMotion: () => true } },
    ],
  });
  // 測試的是編輯器的邏輯；拖曳需要 jsdom 沒有的版面量測，由 e2e 涵蓋。
  TestBed.overrideComponent(NavigationEditor, {
    set: {
      template:
        '<nx-menu-panel [items]="actions()" /><dialog #moveDialog></dialog><nx-confirm-dialog />',
      imports: [MenuPanel, ConfirmDialog],
    },
  });
  HTMLDialogElement.prototype.showModal ??= function (this: HTMLDialogElement) {
    this.setAttribute('open', '');
  };
  HTMLDialogElement.prototype.close ??= function (this: HTMLDialogElement) {
    this.removeAttribute('open');
  };
  // 浮層需要 jsdom 沒有的 popover API；這裡只關心選單提供哪些動作。
  vi.spyOn(MenuPanel.prototype, 'open').mockImplementation(() => undefined);
  const fixture = TestBed.createComponent(NavigationEditor);
  fixture.detectChanges();
  const confirm = fixture.debugElement.children[2].componentInstance as ConfirmDialog;
  return { editor: fixture.componentInstance, api, store, confirm };
}
const row = (editor: NavigationEditor, id: string) =>
  editor.rows().find((entry) => entry.item.id === id)!;
const key = (name: string) => new KeyboardEvent('keydown', { key: name, cancelable: true });

describe('NavigationEditor', () => {
  afterEach(() => {
    TestBed.resetTestingModule();
    vi.restoreAllMocks();
  });

  it('lists the tree depth first with each row placed in its level', () => {
    const { editor } = setup();
    expect(
      editor
        .rows()
        .map((entry) => [entry.item.id, entry.depth, entry.index, entry.count, entry.path]),
    ).toEqual([
      ['dashboard', 0, 0, 3, ''],
      ['work', 0, 1, 3, ''],
      ['chat', 1, 0, 2, 'AI 工作台'],
      ['files', 1, 1, 2, 'AI 工作台'],
      ['system', 0, 2, 3, ''],
      ['audit', 1, 0, 2, '系統管理'],
      ['wiki', 1, 1, 2, '系統管理'],
    ]);
    expect(editor.audience(row(editor, 'wiki').item)).toBe('平台管理');
    expect(editor.audience(row(editor, 'audit').item)).toBe('所有登入者');
    expect(editor.icon(row(editor, 'wiki').item)).toBe('external');
  });

  it('moves within the level with the arrow keys and saves the whole move at once', async () => {
    const { editor, api, store } = setup();
    editor.key(key('ArrowUp'), row(editor, 'files'));
    await vi.waitFor(() => expect(api.orderNavigationItems).toHaveBeenCalled());
    expect(api.orderNavigationItems).toHaveBeenCalledWith({
      items: [{ id: 'files', parentId: 'work', sortOrder: 0 }],
    });
    expect(
      editor
        .rows()
        .map((entry) => entry.item.id)
        .slice(2, 4),
    ).toEqual(['files', 'chat']);
    await vi.waitFor(() => expect(store.changed).toHaveBeenCalled());
    expect(editor.announcement()).toBe('files：AI 工作台第 1 個，共 2 個');
  });

  it('moves out of a group after it and into the group above with left and right', async () => {
    const { editor, api } = setup();
    editor.key(key('ArrowLeft'), row(editor, 'chat'));
    await vi.waitFor(() =>
      expect(api.orderNavigationItems).toHaveBeenLastCalledWith({
        items: [{ id: 'chat', parentId: null, sortOrder: 25 }],
      }),
    );
    await vi.waitFor(() => expect(editor.saving()).toBe(false));
    // dashboard 上方沒有分群：不送出，只報讀原因。
    editor.key(key('ArrowRight'), row(editor, 'dashboard'));
    expect(editor.announcement()).toContain('上方沒有可以移入的分群');
    editor.key(key('ArrowRight'), row(editor, 'chat'));
    await vi.waitFor(() =>
      expect(api.orderNavigationItems).toHaveBeenLastCalledWith({
        items: [{ id: 'chat', parentId: 'work', sortOrder: 30 }],
      }),
    );
  });

  it('restores the previous order and shows the hint when the server refuses', async () => {
    const { editor, api, store } = setup();
    api.orderNavigationItems.mockRejectedValueOnce(
      new ApiError(400, 'navigation_depth_exceeded', 'x'),
    );
    const before = editor.rows().map((entry) => entry.item.id);
    editor.key(key('End'), row(editor, 'chat'));
    await vi.waitFor(() => expect(store.actionError()).not.toBe(''));
    expect(store.actionError()).toContain(publicErrorHints['navigation_depth_exceeded']);
    expect(editor.rows().map((entry) => entry.item.id)).toEqual(before);
    expect(editor.announcement()).toBe('無法儲存位置，已還原');
    expect(store.changed).not.toHaveBeenCalled();
  });

  it('offers only the moves the tree allows and refuses to delete a group that is not empty', () => {
    const { editor } = setup();
    const actions = (id: string) => {
      editor.openMenu(row(editor, id), document.createElement('button'));
      return Object.fromEntries(editor.actions().map((action) => [action.id, action]));
    };
    const dashboard = actions('dashboard');
    expect(dashboard['up'].disabled).toBe(true);
    expect(dashboard['outdent'].disabled).toBe(true);
    expect(dashboard['indent'].disabled).toBe(true);
    expect(dashboard['add']).toBeUndefined();
    const audit = actions('audit');
    expect(audit['outdent'].disabled).toBe(false);
    const wiki = actions('wiki');
    expect(wiki['delete'].disabled).toBe(false);
    const system = actions('system');
    expect(system['indent'].label).toBe('移入上方分群「AI 工作台」');
    expect(system['delete']).toMatchObject({ disabled: true, label: '刪除（請先清空分群）' });
    expect(system['add'].disabled).toBe(false);
  });

  it('moves to the end of another level from the move dialog, skipping places that do not fit', async () => {
    const { editor, api } = setup();
    editor.openMove(row(editor, 'work'));
    // 分群不能移到自己底下；也不出現在清單中。
    expect(editor.topDisabled()).toBe(false);
    expect(editor.destinations().map((node) => [node.label, !!node.disabled])).toEqual([
      ['系統管理', false],
    ]);
    editor.moving.set({ row: row(editor, 'work'), parentId: 'system' });
    await editor.confirmMove(new Event('submit'));
    expect(api.orderNavigationItems).toHaveBeenCalledWith({
      items: [{ id: 'work', parentId: 'system', sortOrder: 30 }],
    });
  });

  it('deletes after confirmation and tells the sidebar', async () => {
    const { editor, api, store, confirm } = setup();
    const ask = vi.spyOn(confirm, 'ask').mockResolvedValueOnce(false).mockResolvedValueOnce(true);
    editor.openMenu(row(editor, 'wiki'), document.createElement('button'));
    editor.act('delete');
    await vi.waitFor(() => expect(ask).toHaveBeenCalledTimes(1));
    expect(api.deleteNavigationItem).not.toHaveBeenCalled();
    editor.act('delete');
    await vi.waitFor(() => expect(api.deleteNavigationItem).toHaveBeenCalledWith('wiki'));
    expect(ask).toHaveBeenLastCalledWith(
      expect.objectContaining({ title: '刪除外部連結？', danger: true }),
    );
    await vi.waitFor(() => expect(store.changed).toHaveBeenCalled());
    expect(editor.rows().some((entry) => entry.item.id === 'wiki')).toBe(false);
  });

  it('does not move anything while testing another identity', () => {
    const { editor, api } = setup(true);
    editor.key(key('ArrowUp'), row(editor, 'files'));
    expect(api.orderNavigationItems).not.toHaveBeenCalled();
  });
});
