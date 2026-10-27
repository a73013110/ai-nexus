import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeAll, describe, expect, it, vi } from 'vitest';
import type { AdminFeatureDto, AdminGroupDto, AdminRoleDto } from '@core/api/schema';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { AccessControlApi } from '../access-control-api';
import { ConfirmDialog } from '@shared/ui/confirm-dialog';
import { AccessControlStore } from '../access-control-store';
import { AccessEditorDialog } from './access-editor-dialog';

const catalog = {
  roles: [
    { id: 'member', name: '成員', enabled: true, groupIds: ['basic'] },
    { id: 'reviewer', name: '審閱者', enabled: true, groupIds: ['review'] },
    { id: 'retired', name: '退役', enabled: false, groupIds: ['admin'] },
  ],
  groups: [
    { id: 'basic', name: '基本', enabled: true, featureIds: ['chat'] },
    { id: 'review', name: '審閱', enabled: true, featureIds: ['repositories', 'off'] },
    { id: 'admin', name: '管理', enabled: true, featureIds: ['admin'] },
  ],
  features: [
    { id: 'chat', name: '對話', enabled: true, icon: 'chat' },
    { id: 'repositories', name: '程式庫', enabled: true, icon: 'git' },
    { id: 'off', name: '停用功能', enabled: false, icon: 'x' },
    { id: 'admin', name: '平台管理', enabled: true, icon: 'lock' },
  ],
  models: [],
  navigationSections: [
    { id: 'collaboration', path: ['協作'], featureIds: ['repositories'] },
    { id: 'work', path: ['AI 工作台'], featureIds: ['chat'] },
  ],
};
const role = catalog.roles[0] as unknown as AdminRoleDto;
const group = { ...catalog.groups[1], policy: null } as unknown as AdminGroupDto;

function setup() {
  const api = {
    role: vi.fn(async () => undefined),
    group: vi.fn(async () => undefined),
    feature: vi.fn(async () => undefined),
  };
  const store = {
    catalog: signal(catalog),
    saved: vi.fn(),
    toast: { success: vi.fn() },
  };
  TestBed.configureTestingModule({
    providers: [
      { provide: AccessControlApi, useValue: api },
      { provide: AccessControlStore, useValue: store },
      { provide: WorkspaceSession, useValue: {} },
    ],
  });
  // 測試的是編輯器的邏輯；它的範本需要 jsdom 沒有的瀏覽器版面 API。
  TestBed.overrideComponent(AccessEditorDialog, {
    set: {
      template: '<dialog #editorDialog></dialog><nx-confirm-dialog />',
      imports: [ConfirmDialog],
    },
  });
  const fixture = TestBed.createComponent(AccessEditorDialog);
  fixture.detectChanges();
  const native = fixture.nativeElement.querySelector('dialog') as HTMLDialogElement;
  const confirm = fixture.debugElement.children[1].componentInstance as ConfirmDialog;
  return { dialog: fixture.componentInstance, api, store, native, confirm };
}

describe('AccessEditorDialog', () => {
  beforeAll(() => {
    HTMLDialogElement.prototype.showModal ??= function (this: HTMLDialogElement) {
      this.setAttribute('open', '');
    };
    HTMLDialogElement.prototype.close ??= function (this: HTMLDialogElement) {
      this.removeAttribute('open');
    };
  });
  afterEach(() => TestBed.resetTestingModule());

  it('previews the enabled features of the enabled groups a role would join', () => {
    const { dialog } = setup();
    dialog.open('role', role);
    expect(dialog.proposedFeatures().map((item) => item.id)).toEqual(['chat']);
    dialog.update('ids', ['basic', 'review']);
    expect(dialog.proposedFeatures().map((item) => item.id)).toEqual(['chat', 'repositories']);
    dialog.update('enabled', false);
    expect(dialog.proposedFeatures()).toEqual([]);
  });

  it('saves a role with its groups and reports the change', async () => {
    const { dialog, api, store } = setup();
    dialog.open('role', role);
    dialog.update('ids', ['basic', 'review']);
    await dialog.save(new Event('submit'));
    expect(api.role).toHaveBeenCalledWith('member', {
      name: '成員',
      enabled: true,
      groupIds: ['basic', 'review'],
    });
    expect(store.saved).toHaveBeenCalled();
  });

  it('requires an identifier and a name for new roles', async () => {
    const { dialog, store } = setup();
    dialog.open('role');
    await dialog.save(new Event('submit'));
    expect(dialog.editorError()).toBe('請輸入識別碼與名稱。');
    expect(store.saved).not.toHaveBeenCalled();
  });

  it('lists group features where the menu puts them and the groups of a role as one card', () => {
    const { dialog } = setup();
    dialog.open('group', group);
    expect(
      dialog
        .featureChoices()
        .map((item) => [item.name, item.options.map((option) => option.label)]),
    ).toEqual([
      ['協作', ['程式庫']],
      ['AI 工作台', ['對話']],
      // 不在目錄中的 ID 收在最後一張卡片。
      ['其他', ['停用功能（停用）', '平台管理']],
    ]);
    expect(dialog.groupChoices()).toEqual([
      expect.objectContaining({ name: '全部功能群組', options: expect.any(Array) }),
    ]);
    expect(dialog.groupChoices()[0].options.map((item) => item.id)).toEqual([
      'basic',
      'review',
      'admin',
    ]);
  });

  it('names a feature by id and route, leaves out a missing route and saves name and switch only', async () => {
    const { dialog, api } = setup();
    const feature = (id: string, route: string) =>
      ({
        id,
        name: '日誌匯出',
        route,
        enabled: true,
        icon: 'download',
      }) as AdminFeatureDto;
    dialog.open('feature', feature('diagnostics.logs', '/admin/diagnostics/logs'));
    expect(dialog.editor()!.subtitle).toBe('diagnostics.logs · /admin/diagnostics/logs');
    dialog.open('feature', feature('diagnostics.logs.export', ''));
    expect(dialog.editor()!.subtitle).toBe('diagnostics.logs.export');
    dialog.update('name', '匯出日誌');
    await dialog.save(new Event('submit'));
    expect(api.feature).toHaveBeenCalledWith('diagnostics.logs.export', {
      name: '匯出日誌',
      enabled: true,
    });
  });

  it('saves a blank feature name, or the catalog default, as no override', async () => {
    const { dialog, api } = setup();
    const feature = { ...catalog.features[0], name: '聊天', route: '/work/chat' };
    dialog.open('feature', feature as AdminFeatureDto);
    expect(dialog.editor()!.defaultName).toBe('對話');
    dialog.update('name', ' ');
    await dialog.save(new Event('submit'));
    expect(dialog.editorError()).toBe('');
    expect(api.feature).toHaveBeenLastCalledWith('chat', expect.objectContaining({ name: null }));
    dialog.open('feature', feature as AdminFeatureDto);
    dialog.update('name', '對話');
    await dialog.save(new Event('submit'));
    expect(api.feature).toHaveBeenLastCalledWith('chat', expect.objectContaining({ name: null }));
  });

  it('saves both tabs of a group at once and opens the tab that holds an invalid value', async () => {
    const { dialog, api } = setup();
    dialog.open('group', group);
    dialog.selectSection('models');
    dialog.update('name', ' ');
    await dialog.save(new Event('submit'));
    expect(dialog.groupSection()).toBe('settings');
    dialog.update('name', '審閱');
    dialog.update('modelPolicy', {
      ...dialog.editor()!.modelPolicy,
      tokenLimits: { m: '-1' },
    });
    await dialog.save(new Event('submit'));
    expect(dialog.groupSection()).toBe('models');
    dialog.update('modelPolicy', { ...dialog.editor()!.modelPolicy, tokenLimits: {} });
    dialog.update('storage', 'abc');
    await dialog.save(new Event('submit'));
    expect(dialog.groupSection()).toBe('settings');
    expect(dialog.editorError()).not.toBe('');
    expect(api.group).not.toHaveBeenCalled();
    dialog.update('storage', '2');
    await dialog.save(new Event('submit'));
    expect(api.group).toHaveBeenCalledWith('review', {
      name: '審閱',
      enabled: true,
      featureIds: ['repositories', 'off'],
      scopes: {},
      managerScopes: {},
      policy: { allowedModelIds: null, dailyTokenLimits: {}, storedAttachmentLimitBytes: 2e9 },
    });
  });

  it('offers only the scopes a feature declares and defaults new grants to the narrowest', async () => {
    const { dialog, api } = setup();
    dialog.open('group', group);
    expect(dialog.scopedGrants()).toEqual([]);
    dialog.update('ids', ['repositories', 'audit']);
    const [grant] = dialog.scopedGrants();
    expect(grant.id).toBe('audit');
    expect(grant.value).toBe('own');
    expect(grant.options.map((option) => option.value)).toEqual([
      'own',
      'unit',
      'unit-tree',
      'all',
    ]);
    dialog.updateScope('audit', 'unit-tree');
    await dialog.save(new Event('submit'));
    expect(api.group).toHaveBeenCalledWith(
      'review',
      expect.objectContaining({
        featureIds: ['repositories', 'audit'],
        scopes: { audit: 'unit-tree' },
      }),
    );
  });

  it('offers manager scopes only where declared, defaulting to none and folding them into all', async () => {
    const { dialog, api } = setup();
    dialog.open('group', group);
    dialog.update('ids', ['audit', 'attendance.records']);
    const [audit, records] = dialog.scopedGrants();
    // 稽核沒宣告主管範圍，不顯示第二個選單；出勤紀錄新勾選時是最窄的本人＋不另外。
    expect(audit.manager).toBeNull();
    expect(records.value).toBe('own');
    expect(records.manager?.value).toBe('');
    expect(records.manager?.options.map((option) => option.label)).toEqual([
      '不另外',
      '所管單位',
      '所管單位及下層',
    ]);
    expect(dialog.managerScoped()).toBe(true);
    dialog.updateScope('attendance.records', 'managed-tree', 'managerScopes');
    expect(dialog.scopedGrants()[1].manager?.value).toBe('managed-tree');
    // 所有成員選全部時已含主管可看的範圍：第二個選單停用、顯示「已含」，也不送出。
    dialog.updateScope('attendance.records', 'all');
    expect(dialog.scopedGrants()[1].manager).toMatchObject({
      included: true,
      value: 'included',
      options: [{ value: 'included', label: '已含' }],
    });
    await dialog.save(new Event('submit'));
    expect(api.group).toHaveBeenLastCalledWith(
      'review',
      expect.objectContaining({
        scopes: { audit: 'own', 'attendance.records': 'all' },
        managerScopes: {},
      }),
    );
    // 改回窄的範圍時，先前選的主管範圍還在。
    dialog.updateScope('attendance.records', 'own');
    await dialog.save(new Event('submit'));
    expect(api.group).toHaveBeenLastCalledWith(
      'review',
      expect.objectContaining({ managerScopes: { 'attendance.records': 'managed-tree' } }),
    );
  });

  it('opens saved manager scopes and treats changing them as unsaved', () => {
    const { dialog } = setup();
    const scoped = {
      ...group,
      featureIds: ['attendance.records'],
      scopes: { 'attendance.records': 'own' },
      managerScopes: { 'attendance.records': 'managed-tree' },
    } as AdminGroupDto;
    dialog.open('group', scoped);
    expect(dialog.scopedGrants()[0].manager?.value).toBe('managed-tree');
    dialog.updateScope('attendance.records', '', 'managerScopes');
    expect(dialog.dirty()).toBe(true);
    dialog.updateScope('attendance.records', 'managed-tree', 'managerScopes');
    expect(dialog.dirty()).toBe(false);
  });

  it('treats a changed scope as unsaved and ignores scopes of features no longer granted', () => {
    const { dialog } = setup();
    const scoped = { ...group, featureIds: ['audit'], scopes: { audit: 'all' } } as AdminGroupDto;
    dialog.open('group', scoped);
    expect(dialog.scopedGrants()[0].value).toBe('all');
    dialog.updateScope('audit', 'unit');
    expect(dialog.dirty()).toBe(true);
    dialog.updateScope('audit', 'all');
    expect(dialog.dirty()).toBe(false);
  });

  it('tracks unsaved changes against the values as opened and reverts to them', async () => {
    const { dialog, api } = setup();
    dialog.open('group', group);
    expect(dialog.dirty()).toBe(false);
    // 沒有可儲存的內容：儲存會像其停用的按鈕一樣被拒絕。
    await dialog.save(new Event('submit'));
    expect(api.group).not.toHaveBeenCalled();
    // 相同的 id 以不同順序排列，且空白的 token 上限，儲存的政策相同。
    dialog.update('ids', ['off', 'repositories']);
    dialog.update('modelPolicy', { ...dialog.editor()!.modelPolicy, tokenLimits: { m: ' ' } });
    expect(dialog.dirty()).toBe(false);
    dialog.selectSection('models');
    dialog.update('storage', '3');
    dialog.update('enabled', false);
    expect(dialog.dirty()).toBe(true);
    // 一次儲存涵蓋兩個分頁，所以持有變更的那個分頁會被標記。
    expect(dialog.groupSections().map((item) => item.dirty)).toEqual([true, false]);
    dialog.update('modelPolicy', { ...dialog.editor()!.modelPolicy, restricted: true });
    expect(dialog.groupSections().map((item) => item.dirty)).toEqual([true, true]);
    dialog.revert();
    expect(dialog.dirty()).toBe(false);
    expect(dialog.editor()!.enabled).toBe(true);
    expect(dialog.editor()!.storage).toBe('');
    expect(dialog.groupSection()).toBe('models');
  });

  it('opens every group on its settings tab', () => {
    const { dialog } = setup();
    dialog.open('group', group);
    dialog.selectSection('models');
    dialog.open('group', group);
    expect(dialog.groupSection()).toBe('settings');
  });

  it('asks before closing with unsaved changes and closes at once without them', async () => {
    const { dialog, native, confirm } = setup();
    const ask = vi.spyOn(confirm, 'ask');
    dialog.open('role', role);
    await dialog.requestClose();
    expect(ask).not.toHaveBeenCalled();
    expect(native.open).toBe(false);
    dialog.open('role', role);
    dialog.update('name', '成員們');
    ask.mockResolvedValueOnce(false);
    await dialog.requestClose();
    expect(ask).toHaveBeenCalledWith(expect.objectContaining({ confirm: '放棄變更' }));
    expect(native.open).toBe(true);
    expect(dialog.editor()!.name).toBe('成員們');
    ask.mockResolvedValueOnce(true);
    await dialog.requestClose();
    expect(native.open).toBe(false);
  });
});
