import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AuthService } from '@core/auth/auth-service';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { ViewScope } from '@shared/browser/view-scope';
import { AccessControlApi } from '@features/admin/access-control/access-control-api';
import { AccessControlStore } from '@features/admin/access-control/access-control-store';
import { IdentityApi } from './identity-api';
import { AdminUsersStore } from './admin-users-store';

const catalog = {
  roles: [{ id: 'member', name: '成員' }],
  groups: [],
  features: [],
  models: [],
};

const pause = (ms: number) => new Promise((done) => setTimeout(done, ms));

function setup() {
  const generation = signal(1);
  const access = { catalog: vi.fn(async () => catalog) };
  const api = {
    users: vi.fn(async (search: string, offset: number) => ({
      users: [{ id: `${search}-${offset}` }],
      total: 1,
      offset,
    })),
  };
  const session = {
    auth: { generation },
    load: vi.fn(async () => true),
    has: (feature: string) => feature === 'identity.users',
  };
  TestBed.configureTestingModule({
    providers: [
      AccessControlStore,
      AdminUsersStore,
      ViewScope,
      { provide: AccessControlApi, useValue: access },
      { provide: IdentityApi, useValue: api },
      { provide: WorkspaceSession, useValue: session },
      { provide: AuthService, useValue: { generation, guard: AuthService.prototype.guard } },
    ],
  });
  const settle = () => TestBed.inject(ApplicationRef).whenStable();
  return { store: TestBed.inject(AdminUsersStore), access, api, session, settle };
}

describe('admin users store', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('reads users after the catalog and names roles from it', async () => {
    const { store, api, settle } = setup();
    await settle();
    expect(api.users).toHaveBeenCalledWith('', 0);
    expect(store.users()?.users[0].id).toBe('-0');
    expect(store.roleNames(['member', 'gone'])).toEqual(['成員', 'gone']);
  });

  it('searches after typing pauses and starts again from the first page', async () => {
    const { store, api, settle } = setup();
    await settle();
    store.readUsers(100);
    await settle();
    expect(api.users).toHaveBeenLastCalledWith('', 100);
    store.find('王');
    expect(store.loadingUsers()).toBe(true);
    await pause(280);
    await settle();
    expect(api.users).toHaveBeenLastCalledWith('王', 0);
    expect(store.loadingUsers()).toBe(false);
  });

  it('reads the shown page and the catalog again after a change and reports it', async () => {
    const { store, access, api, session, settle } = setup();
    await settle();
    const reads = api.users.mock.calls.length;
    store.saved('已儲存');
    await settle();
    expect(api.users.mock.calls.length).toBe(reads + 1);
    expect(access.catalog).toHaveBeenCalledTimes(2);
    expect(session.load).toHaveBeenCalledWith(true);
    expect(store.toast.toasts()[0]).toMatchObject({ tone: 'success', title: '已儲存' });
  });
});
