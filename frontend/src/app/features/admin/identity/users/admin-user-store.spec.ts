import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { AdminUserDto } from '@core/api/schema';
import { AuthService } from '@core/auth/auth-service';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { ViewScope } from '@shared/browser/view-scope';
import { AdministrationApi } from '@features/admin/access-control/administration-api';
import { AdministrationStore } from '@features/admin/access-control/administration-store';
import { AdminUserStore } from './admin-user-store';

const user = {
  id: 'u1',
  displayName: '王小明',
  account: 'CORP\\wang',
  enabled: true,
  roleIds: ['member'],
  authentication: null,
  storage: { personalLimitBytes: null },
} as unknown as AdminUserDto;

function setup(testing = false) {
  const generation = signal(1);
  let saved = user;
  const api = {
    insights: vi.fn(async () => ({ user: saved, usage: {}, kinds: [], conversations: 0 })),
    updateUser: vi.fn(async (_id: string, body: { displayName: string }) => {
      saved = { ...saved, displayName: body.displayName };
    }),
    roles: vi.fn(async () => undefined),
  };
  const admin = { saved: vi.fn(), readUsers: vi.fn() };
  const session = {
    auth: { generation },
    testing: signal(testing),
    load: vi.fn(async () => true),
    has: () => true,
  };
  TestBed.configureTestingModule({
    providers: [
      AdminUserStore,
      ViewScope,
      { provide: AdministrationApi, useValue: api },
      { provide: AdministrationStore, useValue: admin },
      { provide: WorkspaceSession, useValue: session },
      { provide: AuthService, useValue: { generation, guard: AuthService.prototype.guard } },
    ],
  });
  const settle = () => TestBed.inject(ApplicationRef).whenStable();
  return { store: TestBed.inject(AdminUserStore), api, admin, settle };
}

describe('admin user store', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('saves the account with the saved roles and is clean at once', async () => {
    const { store, api, admin, settle } = setup();
    store.open(user);
    await settle();
    store.account.set({ ...store.account(), name: '王大明' });
    store.roles.set(['member', 'reviewer']);
    expect(store.dirty()).toMatchObject({ account: true, roles: true });
    await store.saveAccount();
    expect(api.updateUser).toHaveBeenCalledWith(
      'u1',
      expect.objectContaining({ displayName: '王大明', roleIds: ['member'] }),
    );
    expect(store.dirty().account).toBe(false);
    expect(admin.saved).toHaveBeenCalled();
    await settle();
    expect(store.current()?.displayName).toBe('王大明');
  });

  it('saves roles on their own and reverts a draft to the saved values', async () => {
    const { store, api, settle } = setup();
    store.open(user);
    await settle();
    store.roles.set(['member', 'reviewer']);
    await store.saveRoles();
    expect(api.roles).toHaveBeenCalledWith('u1', ['member', 'reviewer']);
    expect(store.dirty().roles).toBe(false);
    store.storage.set('10');
    expect(store.dirty().storage).toBe(true);
    store.revert('storage');
    expect(store.storage()).toBe('');
  });

  it('starts every opening from the saved values on the account tab', async () => {
    const { store, settle } = setup();
    store.open(user);
    await settle();
    store.select('roles');
    store.account.set({ ...store.account(), name: '草稿' });
    store.open(user);
    expect(store.tab()).toBe('account');
    expect(store.account().name).toBe('王小明');
  });

  it('refuses management writes while testing another identity', async () => {
    const { store, api, settle } = setup(true);
    store.open(user);
    await settle();
    store.roles.set([]);
    await store.saveRoles();
    expect(store.locked()).toBe(true);
    expect(api.roles).not.toHaveBeenCalled();
  });
});
