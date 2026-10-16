import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { afterEach, describe, expect, it } from 'vitest';
import { QueryState, queryState, type QueryStateOptions } from './query-state';

interface Sample {
  text: string;
  tags: string[];
}
const options: QueryStateOptions<Sample> = {
  defaults: () => ({ text: '', tags: [] }),
  toParams: (value) => ({
    q: value.text || undefined,
    tag: value.tags.length ? value.tags : undefined,
  }),
  fromParams: (params, defaults) => ({
    text: params.get('q') ?? defaults.text,
    tags: params.getAll('tag'),
  }),
  chips: (value) =>
    value.text ? [{ id: 'text', label: '文字：' + value.text, clear: { text: '' } }] : [],
  validate: (value) => (value.text === 'bad' ? '不可查詢' : ''),
};

describe('QueryState', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('keeps the draft apart from the applied query until it is applied', () => {
    const state = new QueryState(options);
    state.set('text', 'timeout');
    expect(state.dirty()).toBe(true);
    expect(state.applied().text).toBe('');
    expect(state.chips()).toEqual([]);
    const before = state.applied();
    expect(state.apply()).toBe(true);
    expect(state.dirty()).toBe(false);
    expect(state.applied().text).toBe('timeout');
    expect(state.chips().map((chip) => chip.label)).toEqual(['文字：timeout']);
    // Applying the same conditions reads again.
    state.apply();
    expect(state.applied()).not.toBe(before);
  });

  it('refuses invalid conditions and keeps the applied ones', () => {
    const state = new QueryState(options);
    state.set('text', 'bad');
    expect(state.apply()).toBe(false);
    expect(state.error()).toBe('不可查詢');
    expect(state.applied().text).toBe('');
  });

  it('commits and removes one condition while other edits stay pending', () => {
    const state = new QueryState(options);
    state.set('text', 'timeout');
    state.apply();
    state.set('tags', ['a']);
    state.remove(state.chips()[0]);
    expect(state.applied()).toEqual({ text: '', tags: [] });
    expect(state.draft()).toEqual({ text: '', tags: ['a'] });
    expect(state.dirty()).toBe(true);
    state.reset();
    expect(state.draft()).toEqual({ text: '', tags: [] });
    expect(state.dirty()).toBe(false);
  });

  it('reads a link and writes applied conditions to the URL', async () => {
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    const router = TestBed.inject(Router);
    await router.navigateByUrl('/?q=trace&tag=a&tag=b');
    const state = TestBed.runInInjectionContext(() => queryState(options));
    expect(state.applied()).toEqual({ text: 'trace', tags: ['a', 'b'] });
    state.set('text', 'next');
    state.apply();
    await TestBed.inject(Router).navigated;
    await new Promise((resolve) => setTimeout(resolve));
    expect(router.url).toBe('/?q=next&tag=a&tag=b');
    // A navigation elsewhere on the page applies its parameters.
    await router.navigateByUrl('/?q=other');
    expect(state.applied()).toEqual({ text: 'other', tags: [] });
    expect(state.draft()).toEqual({ text: 'other', tags: [] });
  });
});
