import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeAll, describe, expect, it, vi } from 'vitest';
import type { AdminFeatureDto, AdminGroupDto, AdminRoleDto } from '../../../core/api/schema';
import { AdminApi } from '../admin-api';
import { ConfirmDialog } from '../../../shared/ui/confirm-dialog';
import { AdminStore } from '../admin-store';
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
    { id: 'chat', name: '對話', enabled: true },
    { id: 'repositories', name: '程式庫', enabled: true },
    { id: 'off', name: '停用功能', enabled: false },
    { id: 'admin', name: '平台管理', enabled: true },
  ],
  models: [],
};
const role = catalog.roles[0] as unknown as AdminRoleDto;
const group = { ...catalog.groups[1], policy: null } as unknown as AdminGroupDto;

function setup(testing = false) {
  const api = {
    role: vi.fn(async () => undefined),
    group: vi.fn(async () => undefined),
    feature: vi.fn(async () => undefined),
  };
  const store = { catalog: signal(catalog), saved: vi.fn(), testing: signal(testing) };
  TestBed.configureTestingModule({
    providers: [
      { provide: AdminApi, useValue: api },
      { provide: AdminStore, useValue: store },
    ],
  });
  // The editor's logic is under test; its template needs browser layout APIs jsdom lacks.
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
    expect(dialog.proposedFeatures().map((x) => x.id)).toEqual(['chat']);
    dialog.update('ids', ['basic', 'review']);
    expect(dialog.proposedFeatures().map((x) => x.id)).toEqual(['chat', 'repositories']);
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

  it('lists group features by category and the groups of a role as one card', () => {
    const { dialog } = setup();
    dialog.open('group', group);
    expect(
      dialog.featureChoices().map((x) => [x.name, x.options.map((option) => option.label)]),
    ).toEqual([
      ['工作', ['對話']],
      ['協作與品質', ['程式庫']],
      ['系統', ['平台管理']],
      ['更多工具', ['停用功能（停用）']],
    ]);
    expect(dialog.groupChoices()).toEqual([
      expect.objectContaining({ name: '全部功能群組', options: expect.any(Array) }),
    ]);
    expect(dialog.groupChoices()[0].options.map((x) => x.id)).toEqual(['basic', 'review', 'admin']);
  });

  it('names a feature by id and route, leaves out a missing route and keeps its order', async () => {
    const { dialog, api } = setup();
    const feature = (route: string) =>
      ({
        id: 'logs.export',
        name: '日誌匯出',
        route,
        sortOrder: 95,
        enabled: true,
      }) as AdminFeatureDto;
    dialog.open('feature', feature('/admin/logs'));
    expect(dialog.editor()!.subtitle).toBe('logs.export · /admin/logs');
    dialog.open('feature', feature(''));
    expect(dialog.editor()!.subtitle).toBe('logs.export');
    dialog.update('name', '匯出日誌');
    await dialog.save(new Event('submit'));
    expect(api.feature).toHaveBeenCalledWith('logs.export', {
      name: '匯出日誌',
      enabled: true,
      sortOrder: 95,
    });
  });

  it('saves every tab of a group at once and opens the tab that holds an invalid value', async () => {
    const { dialog, api } = setup();
    dialog.open('group', group);
    dialog.groupSection.set('models');
    dialog.update('name', ' ');
    await dialog.save(new Event('submit'));
    expect(dialog.groupSection()).toBe('general');
    dialog.update('name', '審閱');
    dialog.update('storage', 'abc');
    await dialog.save(new Event('submit'));
    expect(dialog.groupSection()).toBe('storage');
    expect(dialog.editorError()).not.toBe('');
    expect(api.group).not.toHaveBeenCalled();
    dialog.update('storage', '2');
    await dialog.save(new Event('submit'));
    expect(api.group).toHaveBeenCalledWith('review', {
      name: '審閱',
      enabled: true,
      featureIds: ['repositories', 'off'],
      policy: { allowedModelIds: null, dailyTokenLimits: {}, storedAttachmentLimitBytes: 2e9 },
    });
  });

  it('tracks unsaved changes against the values as opened and reverts to them', async () => {
    const { dialog, api } = setup();
    dialog.open('group', group);
    expect(dialog.dirty()).toBe(false);
    // Nothing to save: the save is refused like its disabled button.
    await dialog.save(new Event('submit'));
    expect(api.group).not.toHaveBeenCalled();
    // The same ids in another order, and a blank token limit, save the same policy.
    dialog.update('ids', ['off', 'repositories']);
    dialog.update('modelPolicy', { ...dialog.editor()!.modelPolicy, tokenLimits: { m: ' ' } });
    expect(dialog.dirty()).toBe(false);
    dialog.groupSection.set('storage');
    dialog.update('storage', '3');
    dialog.update('enabled', false);
    expect(dialog.dirty()).toBe(true);
    dialog.revert();
    expect(dialog.dirty()).toBe(false);
    expect(dialog.editor()!.enabled).toBe(true);
    expect(dialog.editor()!.storage).toBe('');
    expect(dialog.groupSection()).toBe('storage');
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

  it('refuses writes while testing another identity', async () => {
    const { dialog, api } = setup(true);
    dialog.open('role', role);
    expect(dialog.locked()).toBe(true);
    dialog.update('ids', ['basic', 'review']);
    await dialog.save(new Event('submit'));
    expect(api.role).not.toHaveBeenCalled();
  });
});
