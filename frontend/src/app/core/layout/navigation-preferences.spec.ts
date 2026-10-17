import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it } from 'vitest';
import { WorkspaceSession } from '../auth/workspace-session';
import { NavigationPreferences } from './navigation-preferences';

// 預設的選單：每個功能的群組與位置，與 `/me` 回報的相同。
const MENU: Record<string, [string | null, number]> = {
  dashboard: [null, 5],
  chat: ['work', 10],
  files: ['work', 20],
  shared: ['collaboration', 50],
  tasks: ['collaboration', 55],
  admin: ['system', 90],
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
  route: '/' + id,
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
    const { preferences } = setup(['admin', 'files', 'dashboard']);
    const tree = preferences.tree();
    expect(tree.features.map((item) => item.id)).toEqual(['dashboard']);
    expect(
      tree.groups.map((item2) => [item2.group.id, item2.features.map((file) => file.id)]),
    ).toEqual([
      ['work', ['files']],
      ['system', ['admin']],
    ]);
  });
  it('pins the first four available features in preference order', () => {
    const { preferences } = setup(['admin', 'files', 'tasks', 'chat', 'shared', 'dashboard']);
    expect(preferences.ids()).toEqual(['dashboard', 'chat', 'files', 'shared']);
    expect(preferences.display()).toBe('icons');
  });
  it('saves per account, stops at five and keeps pins the account cannot open', () => {
    const { preferences, me } = setup(['chat', 'files', 'tasks', 'shared', 'admin', 'audit']);
    expect(preferences.ids()).toEqual(['chat', 'files', 'shared', 'tasks']);
    preferences.toggle('admin');
    expect(preferences.ids()).toEqual(['chat', 'files', 'shared', 'tasks', 'admin']);
    expect(preferences.full()).toBe(true);
    preferences.toggle('audit');
    expect(preferences.has('audit')).toBe(false);
    preferences.setDisplay('list');
    me.set({ id: 'user-1', access: access(['chat', 'tasks', 'admin']) });
    expect(preferences.ids()).toEqual(['chat', 'tasks', 'admin']);
    preferences.toggle('chat');
    me.set({ id: 'user-1', access: access(['chat', 'files', 'tasks', 'admin']) });
    expect(preferences.ids()).toEqual(['files', 'tasks', 'admin']);
    expect(JSON.parse(localStorage.getItem('nexus.navigation-display.user-1')!)).toBe('list');
    me.set({ id: 'user-2', access: access(['chat']) });
    expect(preferences.ids()).toEqual(['chat']);
    expect(preferences.display()).toBe('icons');
  });
});
