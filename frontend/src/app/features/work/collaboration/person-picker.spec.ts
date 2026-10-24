import { computed, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import type { DirectoryUserDto } from '@core/api/schema';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { DirectorySearch } from './directory-search';
import { PersonPicker } from './person-picker';

const users: DirectoryUserDto[] = [
  { id: 'u1', account: 'wang.xm', displayName: '王小明' },
  { id: 'u2', account: 'wang.xh', displayName: '王小華' },
];

/** 只留選擇器讀到的部分；查詢結果由測試直接給。 */
function fakeSearch(results: DirectoryUserDto[]) {
  const text = signal('');
  return {
    text,
    searchable: computed(() => text().trim().length >= 2),
    results: signal(results),
    loading: signal(false),
    error: signal(null),
    find: (value: string) => text.set(value),
    clear: () => text.set(''),
  } as unknown as DirectorySearch;
}

describe('PersonPicker', () => {
  beforeEach(() => {
    // jsdom 沒有 popover API；只需要記錄開關。
    HTMLElement.prototype.showPopover ??= function () {};
    HTMLElement.prototype.hidePopover ??= function () {};
  });
  afterEach(() => TestBed.resetTestingModule());

  function setup(results: DirectoryUserDto[], nameHint = '') {
    const fixture = TestBed.createComponent(PersonPicker);
    fixture.componentRef.setInput('label', '尋找持有人');
    fixture.componentRef.setInput('search', fakeSearch(results));
    fixture.componentRef.setInput('nameHint', nameHint);
    fixture.detectChanges();
    const host = fixture.nativeElement as HTMLElement;
    const input = host.querySelector<HTMLInputElement>('input[role="combobox"]')!;
    const type = (value: string) => {
      input.dispatchEvent(new Event('focus'));
      input.value = value;
      input.dispatchEvent(new Event('input'));
      fixture.detectChanges();
    };
    const key = (name: string) => {
      const event = new KeyboardEvent('keydown', { key: name, cancelable: true, bubbles: true });
      input.dispatchEvent(event);
      fixture.detectChanges();
      return event;
    };
    return { fixture, host, input, type, key };
  }

  it('opens below the field from two characters and picks with the arrow keys and Enter', () => {
    const { fixture, input, type, key } = setup(users);
    const picked = vi.fn();
    fixture.componentInstance.picked.subscribe(picked);
    type('王');
    expect(input.getAttribute('aria-expanded')).toBe('false');
    type('王小');
    expect(input.getAttribute('aria-expanded')).toBe('true');
    expect(input.getAttribute('aria-activedescendant')).toMatch(/-0$/);
    key('ArrowDown');
    expect(key('Enter').defaultPrevented).toBe(true);
    expect(picked).toHaveBeenCalledWith(users[1]);
  });

  it('offers the typed name as the last option when nothing matches', () => {
    const { fixture, host, type, key } = setup([], '尚無帳號時只記姓名');
    const named = vi.fn();
    fixture.componentInstance.named.subscribe(named);
    type(' 新人甲 ');
    const options = host.querySelectorAll('[role="option"]');
    expect(options).toHaveLength(1);
    expect(options[0].textContent).toContain('使用姓名「新人甲」');
    key('Enter');
    expect(named).toHaveBeenCalledWith('新人甲');
  });

  it('keeps Escape inside the field so a surrounding dialog stays open', () => {
    const { input, type, key } = setup(users);
    type('王小');
    const escape = key('Escape');
    expect(escape.defaultPrevented).toBe(true);
    expect(input.getAttribute('aria-expanded')).toBe('false');
    key('ArrowDown');
    expect(input.getAttribute('aria-expanded')).toBe('true');
  });
});
