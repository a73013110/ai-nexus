import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AuthService } from '@core/auth/auth-service';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { publicErrorHints } from '@core/errors/public-error-catalog';
import { AccessControlApi } from './access-control-api';
import { AccessControlStore } from './access-control-store';

const catalog = {
  roles: [{ id: 'member', name: '成員' }],
  groups: [{ id: 'basic', name: '基本' }],
  features: [],
  models: [],
};

function setup(features = ['access-control.roles']) {
  const generation = signal(1);
  const api = { catalog: vi.fn(async () => catalog) };
  const session = {
    auth: { generation },
    load: vi.fn(async () => true),
    has: (feature: string) => features.includes(feature),
  };
  TestBed.configureTestingModule({
    providers: [
      AccessControlStore,
      { provide: AccessControlApi, useValue: api },
      { provide: WorkspaceSession, useValue: session },
      { provide: AuthService, useValue: { generation, guard: AuthService.prototype.guard } },
    ],
  });
  const settle = () => TestBed.inject(ApplicationRef).whenStable();
  return { store: TestBed.inject(AccessControlStore), api, session, settle };
}

describe('access control store', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('names roles and groups from the catalog', async () => {
    const { store, settle } = setup();
    await settle();
    expect(store.catalog()).toEqual(catalog);
    expect(store.roleNames(['member', 'gone'])).toEqual(['成員', 'gone']);
    expect(store.groupNames(['basic'])).toEqual(['基本']);
  });

  it('reads the catalog for the users page as well as for any access control page', async () => {
    const { api, settle } = setup(['identity.users']);
    await settle();
    expect(api.catalog).toHaveBeenCalled();
  });

  it('reads the catalog for the navigation page, which only needs names', async () => {
    const { store, api, session, settle } = setup(['access-control.navigation']);
    await settle();
    expect(api.catalog).toHaveBeenCalledTimes(1);
    // 選單變更只影響側欄：重讀帳號，不重讀目錄。
    store.navigationChanged();
    await settle();
    expect(session.load).toHaveBeenCalledWith(true);
    expect(api.catalog).toHaveBeenCalledTimes(1);
  });

  it('explains a missing grant without reading the catalog', async () => {
    // 平台用量是獨立的授權，不會開放存取目錄。
    const { store, api, settle } = setup(['chat', 'operations.usage']);
    await settle();
    expect(api.catalog).not.toHaveBeenCalled();
    expect(store.error()).toBe(publicErrorHints['feature_forbidden']);
  });

  it('reads the catalog and the account again after a change and reports it', async () => {
    const { store, api, session, settle } = setup();
    await settle();
    store.saved('已儲存');
    await settle();
    expect(api.catalog).toHaveBeenCalledTimes(2);
    expect(session.load).toHaveBeenCalledWith(true);
    expect(store.toast.toasts()[0]).toMatchObject({ tone: 'success', title: '已儲存' });
  });
});
