import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeAll, describe, expect, it, vi } from 'vitest';
import type { AdminUserDto } from '../../../core/api/schema';
import { AdminApi } from '../admin-api';
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
const user = {
  id: 'u1',
  displayName: '王小明',
  account: 'CORP\\wang',
  enabled: true,
  roleIds: ['member'],
  authentication: null,
} as unknown as AdminUserDto;

function setup() {
  const api = { roles: vi.fn(async () => undefined), updateUser: vi.fn(async () => undefined) };
  const store = { catalog: signal(catalog), saved: vi.fn() };
  TestBed.configureTestingModule({
    providers: [
      { provide: AdminApi, useValue: api },
      { provide: AdminStore, useValue: store },
    ],
  });
  // The editor's logic is under test; its template needs browser layout APIs jsdom lacks.
  TestBed.overrideComponent(AccessEditorDialog, {
    set: { template: '<dialog #editorDialog></dialog>', imports: [] },
  });
  const fixture = TestBed.createComponent(AccessEditorDialog);
  fixture.detectChanges();
  return { dialog: fixture.componentInstance, api, store };
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

  it('previews the enabled features a user would receive from enabled roles', () => {
    const { dialog } = setup();
    dialog.open('user', user);
    expect(dialog.proposedFeatures().map((x) => x.id)).toEqual(['chat']);
    dialog.check('reviewer', true);
    dialog.check('retired', true);
    expect(dialog.proposedFeatures().map((x) => x.id)).toEqual(['chat', 'repositories']);
  });

  it('saves only the roles when a user profile is unchanged', async () => {
    const { dialog, api, store } = setup();
    dialog.open('user', user);
    dialog.check('reviewer', true);
    await dialog.save(new Event('submit'));
    expect(api.roles).toHaveBeenCalledWith('u1', ['member', 'reviewer']);
    expect(api.updateUser).not.toHaveBeenCalled();
    expect(store.saved).toHaveBeenCalled();
  });

  it('saves the whole account once a login field changes', async () => {
    const { dialog, api } = setup();
    dialog.open('user', user);
    dialog.update('name', '王大明');
    await dialog.save(new Event('submit'));
    expect(api.updateUser).toHaveBeenCalledWith(
      'u1',
      expect.objectContaining({ displayName: '王大明', adAccount: 'wang', roleIds: ['member'] }),
    );
  });

  it('requires an identifier and a name for new roles', async () => {
    const { dialog, store } = setup();
    dialog.open('role');
    await dialog.save(new Event('submit'));
    expect(dialog.editorError()).toBe('請輸入識別碼與名稱。');
    expect(store.saved).not.toHaveBeenCalled();
  });
});
