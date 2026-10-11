import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it } from 'vitest';
import { WorkspaceSession } from '../auth/workspace-session';
import { NavigationPreferences } from './navigation-preferences';

const feature = (id: string) => ({ id, name: id, route: '/' + id });

function setup(ids: string[]) {
  const me = signal({ id: 'user-1', access: { features: ids.map(feature) } });
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
    me.set({ id: 'user-1', access: { features: ['chat', 'tasks', 'admin'].map(feature) } });
    expect(preferences.ids()).toEqual(['chat', 'tasks', 'admin']);
    preferences.toggle('chat');
    me.set({
      id: 'user-1',
      access: { features: ['chat', 'files', 'tasks', 'admin'].map(feature) },
    });
    expect(preferences.ids()).toEqual(['files', 'tasks', 'admin']);
    expect(JSON.parse(localStorage.getItem('nexus.navigation-display.user-1')!)).toBe('list');
    me.set({ id: 'user-2', access: { features: ['chat'].map(feature) } });
    expect(preferences.ids()).toEqual(['chat']);
    expect(preferences.display()).toBe('icons');
  });
});
