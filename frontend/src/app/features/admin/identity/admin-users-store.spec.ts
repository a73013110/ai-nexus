import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AuthService } from '@core/auth/auth-service';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { ViewScope } from '@shared/browser/view-scope';
import { AccessControlApi } from '@features/admin/access-control/access-control-api';
import { AccessControlStore } from '@features/admin/access-control/access-control-store';
import { IdentityApi } from './identity-api';
import { AdminUsersStore } from './admin-users-store';

const catalog = {
  roles: [
    { id: 'member', name: '成員', enabled: true },
    { id: 'legacy', name: '舊角色', enabled: false },
  ],
  groups: [],
  features: [],
  models: [],
};

const pause = (ms: number) => new Promise((done) => setTimeout(done, ms));
type Filters = { search?: string; enabled?: boolean; role?: string[] };

function setup() {
  const generation = signal(1);
  const access = { catalog: vi.fn(async () => catalog) };
  const api = {
    users: vi.fn(async (filters: Filters, offset: number) => ({
      users: [{ id: `${filters.search ?? ''}-${offset}` }],
      total: 250,
      offset,
    })),
    accountReviews: vi.fn(async () => ({
      users: [{ id: 'u1', items: [{ code: 'attendance.card_held', title: '仍持有卡號' }] }],
      notices: [],
    })),
  };
  const session = {
    auth: { generation },
    load: vi.fn(async () => true),
    has: (feature: string) => feature === 'identity.users',
  };
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
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
    expect(api.users).toHaveBeenCalledWith({}, 0, expect.any(AbortSignal));
    expect(store.users()?.users[0].id).toBe('-0');
    expect(store.roleNames(['member', 'gone'])).toEqual(['成員', 'gone']);
  });

  it('applies conditions from the first page and locks reordering while filtered', async () => {
    const { store, api, settle } = setup();
    await settle();
    store.table.turn(1);
    await settle();
    expect(api.users).toHaveBeenLastCalledWith({}, 100, expect.any(AbortSignal));
    expect(store.reorderable()).toBe(true);
    // 欄位只改草稿，按查詢才讀。
    store.query.set('search', ' 王 ');
    store.query.set('roles', ['member', 'legacy']);
    await settle();
    expect(api.users).toHaveBeenLastCalledWith({}, 100, expect.any(AbortSignal));
    store.query.apply();
    await settle();
    expect(api.users).toHaveBeenLastCalledWith(
      { search: '王', role: ['member', 'legacy'] },
      0,
      expect.any(AbortSignal),
    );
    expect(store.reorderable()).toBe(false);
    expect(store.query.chips().map((chip) => chip.label)).toEqual([
      '搜尋：王',
      '角色：成員、舊角色',
    ]);
    store.query.commit({ status: 'disabled' });
    await settle();
    expect(api.users).toHaveBeenLastCalledWith(
      { search: '王', enabled: false, role: ['member', 'legacy'] },
      0,
      expect.any(AbortSignal),
    );
    // 帳號待辦的捷徑只留帳號條件，人才不會被狀態或角色濾掉。
    store.findAccount('wang');
    await settle();
    expect(api.users).toHaveBeenLastCalledWith({ search: 'wang' }, 0, expect.any(AbortSignal));
    store.query.reset();
    await settle();
    expect(api.users).toHaveBeenLastCalledWith({}, 0, expect.any(AbortSignal));
    expect(store.reorderable()).toBe(true);
  });

  it('offers every role and marks disabled ones', async () => {
    const { store, settle } = setup();
    await settle();
    expect(store.roleOptions().map((option) => [option.value, option.tags?.length ?? 0])).toEqual([
      ['member', 0],
      ['legacy', 1],
    ]);
  });

  it('reads the shown page and the catalog again after a change and reports it', async () => {
    const { store, access, api, session, settle } = setup();
    await settle();
    const reads = api.users.mock.calls.length;
    store.saved('已儲存');
    await settle();
    expect(api.users.mock.calls.length).toBe(reads + 1);
    expect(api.accountReviews).toHaveBeenCalledTimes(2);
    expect(access.catalog).toHaveBeenCalledTimes(2);
    expect(session.load).toHaveBeenCalledWith(true);
    expect(store.toast.toasts()[0]).toMatchObject({ tone: 'success', title: '已儲存' });
  });

  it('reads account reviews without waiting for the catalog and finds them by user', async () => {
    const { store, access, api } = setup();
    // 目錄一直沒讀到：應用不會穩定，只能等一下。
    access.catalog.mockImplementation(() => new Promise(() => undefined));
    await pause(20);
    expect(api.accountReviews).toHaveBeenCalledTimes(1);
    expect(api.users).not.toHaveBeenCalled();
    expect(store.reviewsOf('u1').map((item) => item.code)).toEqual(['attendance.card_held']);
    expect(store.reviewsOf('u2')).toEqual([]);
  });

  it('reads the reviews again with the given page after an account change', async () => {
    const { store, api, settle } = setup();
    await settle();
    store.changed(true);
    await settle();
    expect(api.accountReviews).toHaveBeenCalledTimes(2);
  });
});
