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
    // 套用相同的條件會重新讀取。
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

  it('clears dependent conditions when their parent changes to another value', () => {
    const state = new QueryState<{ unit: string; person: string; text: string }>({
      defaults: () => ({ unit: '', person: '', text: '' }),
      dependents: { unit: ['person'] },
    });
    state.set('person', 'amy');
    state.set('unit', 'it');
    expect(state.draft()).toEqual({ unit: 'it', person: '', text: '' });
    state.set('person', 'bob');
    // 同一個值、其他欄位、清回預設（範圍變大）都不影響下層。
    state.set('unit', 'it');
    state.set('text', 'x');
    state.set('unit', '');
    expect(state.draft().person).toBe('bob');
    state.apply();
    // 立即套用的上層同樣清掉已套用與草稿裡的下層；一起指定的下層保留。
    state.commit({ unit: 'hr' });
    expect(state.applied().person).toBe('');
    expect(state.draft().person).toBe('');
    state.commit({ unit: 'it', person: 'amy' });
    expect(state.applied()).toEqual({ unit: 'it', person: 'amy', text: 'x' });
  });

  it('commits and removes one condition while other edits stay pending', () => {
    const state = new QueryState(options);
    state.set('text', 'timeout');
    state.apply();
    state.set('tags', ['a']);
    state.commit(state.chips()[0].clear);
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
    // 頁面其他地方的導覽會套用它的參數。
    await router.navigateByUrl('/?q=other');
    expect(state.applied()).toEqual({ text: 'other', tags: [] });
    expect(state.draft()).toEqual({ text: 'other', tags: [] });
  });

  it('keeps an invalid link in the draft with its reason and shows the defaults', async () => {
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    const router = TestBed.inject(Router);
    await router.navigateByUrl('/?q=bad&tag=a');
    const state = TestBed.runInInjectionContext(() => queryState(options));
    expect(state.applied()).toEqual({ text: '', tags: [] });
    expect(state.draft()).toEqual({ text: 'bad', tags: ['a'] });
    expect(state.error()).toContain('不可查詢');
    expect(state.dirty()).toBe(true);
    // 同一頁導覽到另一個無效連結也一樣；有效的連結清掉錯誤。
    await router.navigateByUrl('/?q=bad');
    expect(state.draft()).toEqual({ text: 'bad', tags: [] });
    expect(state.error()).toContain('不可查詢');
    await router.navigateByUrl('/?q=good');
    expect(state.applied()).toEqual({ text: 'good', tags: [] });
    expect(state.error()).toBe('');
  });

  it('ends on the last of two writes made before the first navigation settles', async () => {
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    const router = TestBed.inject(Router);
    await router.navigateByUrl('/');
    const state = TestBed.runInInjectionContext(() => queryState(options));
    state.set('text', 'one');
    state.apply();
    state.set('text', 'two');
    state.apply();
    await new Promise((resolve) => setTimeout(resolve));
    await new Promise((resolve) => setTimeout(resolve));
    expect(router.url).toBe('/?q=two');
    expect(state.applied()).toEqual({ text: 'two', tags: [] });
    expect(state.draft()).toEqual({ text: 'two', tags: [] });
  });
});
