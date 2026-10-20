import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it } from 'vitest';
import type { NavigationNodeDto } from '@core/api/schema';
import { catalogFeature } from '../api/catalog';
import { WorkspaceSession } from '../auth/workspace-session';
import {
  DEFAULT_COLLAPSE_THRESHOLD,
  NavigationPreferences,
  defaultCollapsed,
} from './navigation-preferences';

const page = (featureId: string, id = featureId): NavigationNodeDto => ({
  id,
  kind: 'page',
  name: featureId,
  icon: featureId,
  featureId,
  route: catalogFeature(featureId)?.route ?? '',
  url: null,
  openInNewTab: false,
  children: [],
});
const group = (id: string, children: NavigationNodeDto[]): NavigationNodeDto => ({
  id,
  kind: 'group',
  name: id,
  icon: null,
  featureId: null,
  route: null,
  url: null,
  openInNewTab: false,
  children,
});
const wiki: NavigationNodeDto = {
  ...page('wiki'),
  kind: 'link',
  featureId: null,
  route: null,
  url: 'https://wiki.example.com/',
};
// 伺服器已修剪的樹：只會有帳號能開啟的頁面（見 `NavigationTree`）。
const access = (ids: string[]) => {
  const has = (id: string) => ids.includes(id);
  const work = ['chat', 'files'].filter(has).map((id) => page(id));
  const collaboration = ['sharing', 'jobs'].filter(has).map((id) => page(id));
  const system = ['identity.users', 'audit'].filter(has).map((id) => page(id));
  return {
    navigation: [
      ...(has('dashboard') ? [page('dashboard')] : []),
      ...(work.length ? [group('work', work)] : []),
      ...(collaboration.length ? [group('collaboration', collaboration)] : []),
      wiki,
      ...(system.length ? [group('system', system)] : []),
    ],
  };
};

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
  it('uses the server tree and offers each page once as a pin candidate, never a link', () => {
    const { preferences, me } = setup(['identity.users', 'files', 'dashboard']);
    expect(preferences.tree().map((node) => node.id)).toEqual([
      'dashboard',
      'work',
      'wiki',
      'system',
    ]);
    me.set({
      id: 'user-1',
      access: {
        navigation: [page('files'), group('more', [page('files', 'more.files'), wiki])],
      },
    });
    expect(preferences.pages().map((item) => [item.id, item.route])).toEqual([
      ['files', '/work/files'],
    ]);
    preferences.toggle('wiki');
    expect(preferences.has('wiki')).toBe(false);
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
    expect(preferences.pinned().map((item) => item.name)).toEqual(['chat', 'files']);
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
  it('remembers collapsed groups per account and expands several at once', () => {
    const { preferences, me } = setup(['chat', 'files', 'sharing', 'jobs']);
    expect(preferences.isCollapsed('work')).toBe(false);
    preferences.setCollapsed('work', true);
    preferences.setCollapsed('collaboration', true);
    expect(JSON.parse(localStorage.getItem('ai-nexus.navigation-collapsed.user-1')!)).toEqual([
      'work',
      'collaboration',
    ]);
    preferences.expand(['collaboration', 'system']);
    expect([...preferences.collapsed()]).toEqual(['work']);
    me.set({ id: 'user-2', access: access(['chat']) });
    expect(preferences.isCollapsed('work')).toBe(false);
    me.set({ id: 'user-1', access: access(['chat']) });
    expect(preferences.isCollapsed('work')).toBe(true);
  });
  it('expands or collapses many groups at once and writes only on change', () => {
    const { preferences } = setup(['chat', 'files', 'sharing', 'jobs']);
    const key = 'ai-nexus.navigation-collapsed.user-1';
    preferences.setAllCollapsed(['work', 'collaboration'], false);
    preferences.expand(['work']);
    preferences.setCollapsed('work', false);
    expect(localStorage.getItem(key)).toBeNull();
    preferences.setAllCollapsed(['work', 'collaboration'], true);
    expect(JSON.parse(localStorage.getItem(key)!)).toEqual(['work', 'collaboration']);
    localStorage.removeItem(key);
    preferences.setAllCollapsed(['collaboration', 'work'], true);
    expect(localStorage.getItem(key)).toBeNull();
    preferences.setAllCollapsed(['work', 'collaboration'], false);
    expect(JSON.parse(localStorage.getItem(key)!)).toEqual([]);
    expect(preferences.isCollapsed('work')).toBe(false);
  });
  it('uses the default only until something is saved, and an empty save stays expanded', () => {
    const { preferences, me } = setup(['chat', 'files', 'sharing', 'jobs']);
    const defaults = signal<ReadonlySet<string>>(new Set(['collaboration']));
    preferences.useDefaultCollapsed(() => defaults());
    expect(preferences.isCollapsed('collaboration')).toBe(true);
    defaults.set(new Set(['work']));
    expect([...preferences.collapsed()]).toEqual(['work']);
    // 第一次操作時，以當下看到的狀態（含預設）存起來。
    preferences.setCollapsed('collaboration', true);
    expect(JSON.parse(localStorage.getItem('ai-nexus.navigation-collapsed.user-1')!)).toEqual([
      'work',
      'collaboration',
    ]);
    preferences.setAllCollapsed(['work', 'collaboration'], false);
    defaults.set(new Set(['work', 'collaboration']));
    expect(preferences.collapsed().size).toBe(0);
    // 另一個帳號沒有儲存過，仍用預設。
    me.set({ id: 'user-2', access: access(['chat']) });
    expect(preferences.isCollapsed('work')).toBe(true);
  });
  it('collapses nested groups off the current branch on narrow screens or past the threshold', () => {
    const nested = (count: number) =>
      Array.from({ length: count }, (_, index) => group(`nested-${index}`, [page(`p${index}`)]));
    const tree = [
      group('work', [page('chat'), group('drafts', [page('files')])]),
      group('system', nested(3)),
    ];
    // 第一層一律展開；目前頁面所在的那一支（system › nested-1）保留展開。
    expect([...defaultCollapsed(tree, ['nested-1', 'system'], true)]).toEqual([
      'drafts',
      'nested-0',
      'nested-2',
    ]);
    expect(defaultCollapsed(tree, [], false).size).toBe(0);
    const many = [group('system', nested(DEFAULT_COLLAPSE_THRESHOLD + 1))];
    expect(defaultCollapsed(many, [], false).size).toBe(DEFAULT_COLLAPSE_THRESHOLD + 1);
    expect(
      defaultCollapsed([group('system', nested(DEFAULT_COLLAPSE_THRESHOLD))], [], false).size,
    ).toBe(0);
  });
});
