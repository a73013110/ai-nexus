import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AuthService } from '@core/auth/auth-service';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { ViewScope } from '@shared/browser/view-scope';
import { ApiError, STALE_WRITE_MESSAGE } from '@core/errors/safe-errors';
import { OrganizationApi } from './organization-api';
import { OrganizationStore } from './organization-store';

const units = [
  {
    id: 'company',
    rowVersion: 11,
    parentId: null,
    name: '總公司',
    enabled: true,
    sortOrder: 10,
    memberCount: 0,
  },
  {
    id: 'it',
    rowVersion: 12,
    parentId: 'company',
    name: '資訊處',
    enabled: true,
    sortOrder: 10,
    memberCount: 1,
  },
  {
    id: 'hr',
    rowVersion: 13,
    parentId: 'company',
    name: '人資處',
    enabled: true,
    sortOrder: 20,
    memberCount: 0,
  },
];
const member = {
  userId: 'bob',
  account: 'bob',
  displayName: 'Bob',
  active: true,
  isPrimary: false,
  isManager: false,
};

function setup() {
  const generation = signal(1);
  const api = {
    units: vi.fn(async () => units.map((unit) => ({ ...unit }))),
    members: vi.fn(async (id: string) => (id === 'it' ? [{ ...member }] : [])),
    orderUnits: vi.fn(async () => undefined),
    saveMember: vi.fn(async () => undefined),
    removeMember: vi.fn(async () => undefined),
    deleteUnit: vi.fn(async (): Promise<void> => {
      throw new Error('unexpected');
    }),
  };
  const session = {
    auth: { generation },
    load: vi.fn(async () => true),
    has: (feature: string) => feature === 'organization',
  };
  TestBed.configureTestingModule({
    providers: [
      OrganizationStore,
      ViewScope,
      { provide: OrganizationApi, useValue: api },
      { provide: WorkspaceSession, useValue: session },
      { provide: AuthService, useValue: { generation, guard: AuthService.prototype.guard } },
    ],
  });
  const settle = () => TestBed.inject(ApplicationRef).whenStable();
  return { store: TestBed.inject(OrganizationStore), api, settle };
}

describe('organization store', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('reads the members of the selected unit only', async () => {
    const { store, api, settle } = setup();
    await settle();
    expect(store.members()).toBeNull();
    store.select(store.units()[1]);
    await settle();
    expect(store.members()?.map((item) => item.userId)).toEqual(['bob']);
    store.select(store.units()[2]);
    expect(store.members()).toBeNull();
    await settle();
    expect(store.members()).toEqual([]);
    expect(api.members).toHaveBeenCalledTimes(2);
  });

  it('sends the whole level when moving a unit and rereads the tree', async () => {
    const { store, api, settle } = setup();
    await settle();
    expect(await store.move(store.units()[2], -1)).toBe(true);
    expect(api.orderUnits).toHaveBeenCalledWith({ parentId: 'company', unitIds: ['hr', 'it'] });
    expect(await store.move(store.units()[0], 1)).toBe(false);
    await settle();
    expect(api.units).toHaveBeenCalledTimes(2);
  });

  it('saves members against the selected unit and keeps failures as the action error', async () => {
    const { store, api, settle } = setup();
    await settle();
    store.select(store.units()[1]);
    await settle();
    expect(await store.saveMember('bob', true, false)).toBe(true);
    expect(api.saveMember).toHaveBeenCalledWith('it', 'bob', { isPrimary: true, isManager: false });
    await settle();
    expect(await store.remove(store.units()[1])).toBe(false);
    expect(store.error()).not.toBe('');
  });

  it('keeps writes off until the tree is reread, so the next write has the new versions', async () => {
    const { store, settle } = setup();
    await settle();
    expect(await store.move(store.units()[2], -1)).toBe(true);
    expect(store.busy()).toBe(true);
    expect(await store.move(store.units()[2], -1)).toBe(false);
    await settle();
    expect(store.busy()).toBe(false);
  });

  it('deletes with the version it read and rereads when someone else changed the unit first', async () => {
    const { store, api, settle } = setup();
    await settle();
    api.deleteUnit.mockRejectedValueOnce(new ApiError(409, 'admin_write_conflict', undefined, ''));
    const reads = api.units.mock.calls.length;
    const unit = store.units()[1];
    expect(await store.remove(unit)).toBe(false);
    expect(api.deleteUnit).toHaveBeenCalledWith('it', 12);
    expect(store.error()).toBe(STALE_WRITE_MESSAGE);
    await settle();
    expect(api.units.mock.calls.length).toBeGreaterThan(reads);
  });

  it('clears the conflict notice once a dialog saves', async () => {
    const { store, settle } = setup();
    await settle();
    store.stale();
    expect(store.error()).toBe(STALE_WRITE_MESSAGE);
    await settle();
    store.changed();
    expect(store.error()).toBe('');
  });
});
