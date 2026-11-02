import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { AdminUserDto } from '@core/api/schema';
import { AuthService } from '@core/auth/auth-service';
import { ApiError, STALE_WRITE_MESSAGE } from '@core/errors/safe-errors';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { ViewScope } from '@shared/browser/view-scope';
import { IdentityApi } from '../identity-api';
import { AdminUsersStore } from '../admin-users-store';
import { AdminUserStore } from './admin-user-store';

const user = {
  id: 'u1',
  displayName: '王小明',
  account: 'wang',
  enabled: true,
  profileVersion: 3,
  roleIds: ['member'],
  authentication: null,
  storage: { personalLimitBytes: null },
} as unknown as AdminUserDto;

function setup() {
  const generation = signal(1);
  let saved = user;
  const api = {
    insights: vi.fn(async () => ({ user: saved, usage: {}, kinds: [], conversations: 0 })),
    updateUser: vi.fn(async (_id: string, body: { displayName: string; enabled: boolean }) => {
      saved = {
        ...saved,
        displayName: body.displayName,
        enabled: body.enabled,
        profileVersion: saved.profileVersion + 1,
      };
    }),
    roles: vi.fn(async (): Promise<void> => {
      saved = { ...saved, profileVersion: saved.profileVersion + 1 };
    }),
    rebindDirectory: vi.fn(async (_id: string, adAccount: string) => {
      if (adAccount === 'taken') throw new Error('ad_binding_taken');
      saved = { ...saved, profileVersion: saved.profileVersion + 1 };
    }),
    unbindDirectory: vi.fn(async () => {
      saved = { ...saved, profileVersion: saved.profileVersion + 1 };
    }),
    email: vi.fn(async (_id: string, email: string | null) => {
      saved = {
        ...saved,
        email,
        emailSource: email ? 'admin' : null,
        emailVerifiedAt: null,
        profileVersion: saved.profileVersion + 1,
      };
    }),
  };
  const admin = { saved: vi.fn(), readUsers: vi.fn() };
  const session = {
    auth: { generation },
    load: vi.fn(async () => true),
    has: () => true,
  };
  TestBed.configureTestingModule({
    providers: [
      AdminUserStore,
      ViewScope,
      { provide: IdentityApi, useValue: api },
      { provide: AdminUsersStore, useValue: admin },
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
      expect.objectContaining({ displayName: '王大明', roleIds: ['member'], profileVersion: 3 }),
    );
    expect(store.dirty().account).toBe(false);
    expect(admin.saved).toHaveBeenCalled();
    await settle();
    expect(store.current()?.displayName).toBe('王大明');
  });

  it('rebinds and unbinds AD at once, reporting failures on the account tab', async () => {
    const { store, api, admin, settle } = setup();
    store.open(user);
    await settle();
    expect(await store.rebind('wang.new')).toBe(true);
    expect(api.rebindDirectory).toHaveBeenCalledWith('u1', 'wang.new', 3);
    expect(admin.saved).toHaveBeenCalledWith('AD 帳號已改綁', expect.any(String));
    await settle();
    expect(await store.rebind('taken')).toBe(false);
    expect(store.errors().account).toBeTruthy();
    await settle();
    expect(await store.unbind()).toBe(true);
    expect(api.unbindDirectory).toHaveBeenCalledWith('u1', 4);
    expect(store.errors().account).toBe('');
  });

  it('sends the email with the raised version when the account endpoint went first', async () => {
    const { store, api, settle } = setup();
    store.open(user);
    await settle();
    store.account.set({ ...store.account(), name: '王大明' });
    store.email.set('wang@corp.test');
    await store.saveAccount();
    expect(api.updateUser).toHaveBeenCalledWith(
      'u1',
      expect.objectContaining({ profileVersion: 3 }),
    );
    expect(api.email).toHaveBeenCalledWith('u1', 'wang@corp.test', 4);
  });

  it('keeps fields read-only until the write is reread, then uses the new version', async () => {
    const { store, api, settle } = setup();
    store.open(user);
    await settle();
    store.roles.set(['member', 'reviewer']);
    await store.saveRoles();
    expect(store.locked()).toBe(true);
    await settle();
    expect(store.locked()).toBe(false);
    store.roles.set(['member']);
    await store.saveRoles();
    expect(api.roles).toHaveBeenLastCalledWith('u1', ['member'], 4);
  });

  it('explains a version conflict, rereads, and keeps an unchanged draft to send again', async () => {
    const { store, api, admin, settle } = setup();
    store.open(user);
    await settle();
    store.roles.set(['member', 'reviewer']);
    api.roles.mockRejectedValueOnce(new ApiError(409, 'admin_write_conflict', undefined, ''));
    const reads = api.insights.mock.calls.length;
    await store.saveRoles();
    expect(store.errors().roles).toBe(STALE_WRITE_MESSAGE);
    expect(admin.readUsers).toHaveBeenCalled();
    await settle();
    expect(api.insights.mock.calls.length).toBeGreaterThan(reads);
    expect(store.roles()).toEqual(['member', 'reviewer']);
  });

  it('saves roles on their own and reverts a draft to the saved values', async () => {
    const { store, api, settle } = setup();
    store.open(user);
    await settle();
    store.roles.set(['member', 'reviewer']);
    await store.saveRoles();
    expect(api.roles).toHaveBeenCalledWith('u1', ['member', 'reviewer'], 3);
    expect(store.dirty().roles).toBe(false);
    store.storage.set('10');
    expect(store.dirty().usage).toBe(true);
    store.revert('usage');
    expect(store.storage()).toBe('');
  });

  it('saves the email with the account, sending only the endpoint that changed', async () => {
    const { store, api, admin, settle } = setup();
    store.open(user);
    await settle();
    expect(store.emailEditable()).toBe(true);
    store.email.set('  wang@corp.test ');
    expect(store.dirty().account).toBe(true);
    await store.saveAccount();
    expect(api.email).toHaveBeenCalledWith('u1', 'wang@corp.test', 3);
    expect(api.updateUser).not.toHaveBeenCalled();
    expect(store.dirty().account).toBe(false);
    expect(admin.saved).toHaveBeenCalled();
    await settle();
    expect(store.current()?.emailSource).toBe('admin');
    store.email.set('');
    await store.saveAccount();
    expect(api.email).toHaveBeenLastCalledWith('u1', null, 4);
  });

  it('enables and disables at once with the saved account, keeping the draft out', async () => {
    const { store, api, settle } = setup();
    store.open(user);
    await settle();
    expect(await store.setEnabled(false)).toBe(true);
    expect(api.updateUser).toHaveBeenCalledWith(
      'u1',
      expect.objectContaining({ displayName: '王小明', enabled: false, roleIds: ['member'] }),
    );
    expect(store.acting()).toBeNull();
    await settle();
    expect(store.current()?.enabled).toBe(false);
    store.account.set({ ...store.account(), name: '王大明' });
    await store.saveAccount();
    expect(api.updateUser).toHaveBeenLastCalledWith(
      'u1',
      expect.objectContaining({ displayName: '王大明', enabled: false }),
    );
  });

  it('treats a directory email as read-only', async () => {
    const { store, settle } = setup();
    store.open({ ...user, email: 'wang@corp.test', emailSource: 'directory' } as AdminUserDto);
    expect(store.emailEditable()).toBe(false);
    expect(store.email()).toBe('wang@corp.test');
    await settle();
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
});
