import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AuthService } from '@core/auth/auth-service';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { AccessControlApi } from './access-control-api';
import { AccessControlStore } from './access-control-store';
import { NavigationStore } from './navigation-store';

const items = [
  {
    id: 'work.chat',
    parentId: 'work',
    kind: 'page',
    featureId: 'chat',
    name: null,
    url: null,
    openInNewTab: true,
    icon: null,
    sortOrder: 10,
    audienceGroupIds: [],
  },
  {
    id: 'work.retired',
    parentId: 'work',
    kind: 'page',
    featureId: 'retired',
    name: null,
    url: null,
    openInNewTab: true,
    icon: null,
    sortOrder: 20,
    audienceGroupIds: [],
  },
  {
    id: 'work',
    parentId: null,
    kind: 'group',
    featureId: null,
    name: 'AI 工作台',
    url: null,
    openInNewTab: true,
    icon: null,
    sortOrder: 10,
    audienceGroupIds: [],
  },
];

function setup() {
  const generation = signal(1);
  const api = {
    catalog: vi.fn(async () => ({
      roles: [],
      groups: [],
      models: [],
      features: [{ id: 'chat', name: '聊天', enabled: false, route: '', icon: 'chat' }],
    })),
    // 每次回傳新的陣列，與真的伺服器回應相同。
    navigationItems: vi.fn(async () => items.map((item) => ({ ...item }))),
  };
  const session = {
    auth: { generation },
    load: vi.fn(async () => true),
    has: (feature: string) => feature === 'access-control.navigation',
  };
  TestBed.configureTestingModule({
    providers: [
      AccessControlStore,
      NavigationStore,
      { provide: AccessControlApi, useValue: api },
      { provide: WorkspaceSession, useValue: session },
      { provide: AuthService, useValue: { generation, guard: AuthService.prototype.guard } },
    ],
  });
  const settle = () => TestBed.inject(ApplicationRef).whenStable();
  return { store: TestBed.inject(NavigationStore), api, session, settle };
}

describe('navigation store', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('names pages by the effective feature name and flags disabled features', async () => {
    const { store, settle } = setup();
    await settle();
    expect(store.ready()).toBe(true);
    expect(store.items().map((item) => store.name(item))).toEqual(['聊天', 'retired', 'AI 工作台']);
    expect(store.disabled(store.items()[0])).toBe(true);
    expect(store.disabled(store.items()[2])).toBe(false);
  });

  it('reads the items and the account again after a change', async () => {
    const { store, api, session, settle } = setup();
    await settle();
    store.replace([]);
    expect(store.items()).toEqual([]);
    store.changed();
    await settle();
    expect(api.navigationItems).toHaveBeenCalledTimes(2);
    expect(session.load).toHaveBeenCalledWith(true);
    expect(store.items()).toHaveLength(3);
  });
});
