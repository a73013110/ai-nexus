import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AuthService } from '../../core/auth/auth-service';
import { WorkspaceSession } from '../../core/auth/workspace-session';
import { ViewScope } from '../../shared/browser/view-scope';
import { AdminApi } from './admin-api';
import { AdminStore } from './admin-store';

const catalog = {
  roles: [{ id: 'member', name: '成員' }],
  groups: [{ id: 'basic', name: '基本' }],
  features: [],
  models: [],
};

const pause = (ms: number) => new Promise((done) => setTimeout(done, ms));

function setup(features = ['admin']) {
  const generation = signal(1);
  const api = {
    catalog: vi.fn(async () => catalog),
    users: vi.fn(async (search: string, offset: number) => ({
      users: [{ id: `${search}-${offset}` }],
      total: 1,
      offset,
    })),
  };
  const session = {
    auth: { generation },
    load: vi.fn(async () => true),
    has: (feature: string) => features.includes(feature),
  };
  TestBed.configureTestingModule({
    providers: [
      AdminStore,
      ViewScope,
      { provide: AdminApi, useValue: api },
      { provide: WorkspaceSession, useValue: session },
      { provide: AuthService, useValue: { generation } },
    ],
  });
  const settle = () => TestBed.inject(ApplicationRef).whenStable();
  return { store: TestBed.inject(AdminStore), api, session, settle };
}

describe('admin store', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('reads users after the catalog and names roles and groups from it', async () => {
    const { store, api, settle } = setup();
    await settle();
    expect(store.catalog()).toEqual(catalog);
    expect(api.users).toHaveBeenCalledWith('', 0);
    expect(store.users()?.users[0].id).toBe('-0');
    expect(store.roleNames(['member', 'gone'])).toEqual(['成員', 'gone']);
    expect(store.groupNames(['basic'])).toEqual(['基本']);
  });

  it('explains a missing management grant without reading the catalog', async () => {
    const { store, api, settle } = setup(['chat']);
    await settle();
    expect(api.catalog).not.toHaveBeenCalled();
    expect(store.error()).toBe('你的帳號目前沒有平台管理權限。');
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

  it('reads the shown page again after a change and reports it', async () => {
    const { store, api, session, settle } = setup();
    await settle();
    const reads = api.users.mock.calls.length;
    store.saved('已儲存。');
    await settle();
    expect(api.users.mock.calls.length).toBe(reads + 1);
    expect(api.catalog).toHaveBeenCalledTimes(2);
    expect(session.load).toHaveBeenCalledWith(true);
    expect(store.notice()).toBe('已儲存。');
  });
});
