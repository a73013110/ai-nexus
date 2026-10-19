import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it } from 'vitest';
import { catalogFeature } from '../api/catalog';
import { WorkspaceSession } from '../auth/workspace-session';
import { NavigationPreferences } from './navigation-preferences';

// 預設的選單：每個功能的群組與位置，與 `/me` 回報的相同。
const MENU: Record<string, [string | null, number]> = {
  dashboard: [null, 5],
  chat: ['work', 10],
  files: ['work', 20],
  sharing: ['collaboration', 50],
  jobs: ['collaboration', 55],
  'identity.users': ['system', 90],
  audit: ['system', 97],
};
const navigationGroups = [
  { id: 'work', parentId: null, name: 'AI 工作台', sortOrder: 10 },
  { id: 'collaboration', parentId: null, name: '協作', sortOrder: 20 },
  { id: 'system', parentId: null, name: '系統管理', sortOrder: 30 },
];
const feature = (id: string) => ({
  id,
  name: id,
  route: catalogFeature(id)?.route ?? '',
  groupId: MENU[id][0],
  sortOrder: MENU[id][1],
  icon: id,
});
const access = (ids: string[]) => ({ features: ids.map(feature), navigationGroups });

function setup(ids: string[]) {
  const me = signal({ id: 'user-1', access: access(ids) });
  TestBed.configureTestingModule({
    providers: [{ provide: WorkspaceSession, useValue: { me } }],
  });
  return { preferences: TestBed.inject(NavigationPreferences), me };
}

describe('navigation preferences', () => {
  afterEach(() => {
    TestBed.resetTestingModule();
    localStorage.clear();
  });
  it('builds the menu tree from the features with a page', () => {
    const { preferences } = setup(['identity.users', 'files', 'dashboard']);
    const tree = preferences.tree();
    expect(tree.features.map((item) => item.id)).toEqual(['dashboard']);
    expect(
      tree.groups.map((item2) => [item2.group.id, item2.features.map((file) => file.id)]),
    ).toEqual([
      ['work', ['files']],
      ['system', ['identity.users']],
    ]);
  });
  it('pins only the default features the account can open', () => {
    const { preferences } = setup([
      'identity.users',
      'files',
      'jobs',
      'chat',
      'sharing',
      'dashboard',
    ]);
    expect(preferences.ids()).toEqual(['chat', 'files']);
    expect(preferences.display()).toBe('icons');
  });
  it('saves per account, stops at five and keeps pins the account cannot open', () => {
    const { preferences, me } = setup([
      'chat',
      'files',
      'jobs',
      'sharing',
      'identity.users',
      'audit',
    ]);
    expect(preferences.ids()).toEqual(['chat', 'files']);
    preferences.toggle('sharing');
    preferences.toggle('jobs');
    preferences.toggle('identity.users');
    expect(preferences.ids()).toEqual(['chat', 'files', 'sharing', 'jobs', 'identity.users']);
    expect(preferences.full()).toBe(true);
    preferences.toggle('audit');
    expect(preferences.has('audit')).toBe(false);
    preferences.setDisplay('list');
    me.set({ id: 'user-1', access: access(['chat', 'jobs', 'identity.users']) });
    expect(preferences.ids()).toEqual(['chat', 'jobs', 'identity.users']);
    preferences.toggle('chat');
    me.set({ id: 'user-1', access: access(['chat', 'files', 'jobs', 'identity.users']) });
    expect(preferences.ids()).toEqual(['files', 'jobs', 'identity.users']);
    expect(JSON.parse(localStorage.getItem('ai-nexus.navigation-display.user-1')!)).toBe('list');
    me.set({ id: 'user-2', access: access(['chat']) });
    expect(preferences.ids()).toEqual(['chat']);
    expect(preferences.display()).toBe('icons');
  });
});
