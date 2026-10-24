import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { Select } from './select';

const options = ['Information', 'Warning', 'Error'].map((value) => ({ value, label: value }));

describe('Select multiple', () => {
  afterEach(() => TestBed.resetTestingModule());

  function setup(values: string[]) {
    const fixture = TestBed.createComponent(Select);
    fixture.componentRef.setInput('label', '等級');
    fixture.componentRef.setInput('placeholder', '全部等級');
    fixture.componentRef.setInput('options', options);
    fixture.componentRef.setInput('multiple', true);
    fixture.componentRef.setInput('values', values);
    fixture.detectChanges();
    return fixture;
  }

  it('names one choice, counts several and shows the placeholder for none', () => {
    const fixture = setup([]);
    const trigger = () => (fixture.nativeElement as HTMLElement).querySelector('.select-trigger')!;
    expect(trigger().textContent).toContain('全部等級');
    fixture.componentRef.setInput('values', ['Error']);
    fixture.detectChanges();
    expect(trigger().textContent).toContain('Error');
    fixture.componentRef.setInput('values', ['Error', 'Warning']);
    fixture.detectChanges();
    expect(trigger().textContent).toContain('已選 2 項');
  });

  it('names the trigger by its label and every choice, and keeps focus off the options', () => {
    const fixture = setup(['Error', 'Warning']);
    const host = fixture.nativeElement as HTMLElement;
    const trigger = host.querySelector('.select-trigger')!;
    const name = trigger
      .getAttribute('aria-labelledby')!
      .split(' ')
      .map((id) => host.querySelector('#' + id)!.textContent!.trim())
      .join(' ');
    expect(name).toBe('等級 Warning、Error');
    expect(trigger.hasAttribute('aria-label')).toBe(false);
    // 滑鼠點選項時焦點留在 trigger，方向鍵才繼續有效。
    const option = host.querySelector('[role="option"]')!;
    const press = new MouseEvent('mousedown', { bubbles: true, cancelable: true });
    option.dispatchEvent(press);
    expect(press.defaultPrevented).toBe(true);
  });

  it('toggles a choice in option order and marks the listbox multiselectable', () => {
    const fixture = setup(['Error']);
    const emitted: string[][] = [];
    fixture.componentInstance.valuesChange.subscribe((values) => emitted.push(values));
    fixture.componentInstance.choose(0);
    fixture.componentInstance.choose(2);
    expect(emitted).toEqual([['Information', 'Error'], []]);
    const listbox = (fixture.nativeElement as HTMLElement).querySelector('[role="listbox"]')!;
    expect(listbox.getAttribute('aria-multiselectable')).toBe('true');
  });
});

describe('Select loading', () => {
  beforeEach(() => {
    HTMLElement.prototype.showPopover = vi.fn();
    HTMLElement.prototype.hidePopover = vi.fn();
  });
  afterEach(() => {
    delete (HTMLElement.prototype as Partial<HTMLElement>).showPopover;
    delete (HTMLElement.prototype as Partial<HTMLElement>).hidePopover;
    TestBed.resetTestingModule();
  });

  function setup(value: string) {
    const fixture = TestBed.createComponent(Select);
    fixture.componentRef.setInput('label', '模型');
    fixture.componentRef.setInput('placeholder', '請選擇模型');
    fixture.componentRef.setInput('options', []);
    fixture.componentRef.setInput('value', value);
    fixture.componentRef.setInput('loading', true);
    fixture.detectChanges();
    const host = fixture.nativeElement as HTMLElement;
    const trigger = host.querySelector<HTMLButtonElement>('.select-trigger')!;
    return { fixture, host, trigger };
  }

  it('spins and says it is loading instead of falling back to the placeholder', () => {
    const { fixture, trigger } = setup('Warning');
    expect(trigger.getAttribute('aria-busy')).toBe('true');
    expect(trigger.disabled).toBe(false);
    expect(trigger.textContent).toContain('正在載入…');
    expect(trigger.textContent).not.toContain('請選擇模型');
    expect(trigger.querySelector('nx-icon')!.classList).toContain('is-spinning');

    fixture.componentRef.setInput('options', options);
    fixture.componentRef.setInput('loading', false);
    fixture.detectChanges();
    expect(trigger.hasAttribute('aria-busy')).toBe(false);
    expect(trigger.textContent).toContain('Warning');
    expect(trigger.querySelector('nx-icon')!.classList).not.toContain('is-spinning');
  });

  it('opens while loading and tells that the options are on their way', () => {
    const { fixture, host, trigger } = setup('');
    trigger.click();
    fixture.detectChanges();
    expect(HTMLElement.prototype.showPopover).toHaveBeenCalled();
    const status = host.querySelector('.select-empty')!;
    expect(status.getAttribute('role')).toBe('status');
    expect(status.textContent).toContain('正在載入選項…');
    expect(host.textContent).not.toContain('沒有符合的選項');

    fixture.componentRef.setInput('loading', false);
    fixture.detectChanges();
    expect(host.querySelector('.select-empty')!.textContent).toContain('沒有符合的選項');
    expect(trigger.disabled).toBe(true);
  });
});

describe('Select tree', () => {
  afterEach(() => TestBed.resetTestingModule());

  const tree = [
    { value: '', label: '所有單位' },
    { value: 'c', label: '星河科技', level: 1, path: [] },
    { value: 'r', label: '研發處', level: 2, path: ['星河科技'] },
    { value: 'p', label: '平台部', level: 3, path: ['星河科技', '研發處'] },
  ];

  function setup(value: string) {
    const fixture = TestBed.createComponent(Select);
    fixture.componentRef.setInput('label', '單位');
    fixture.componentRef.setInput('options', tree);
    fixture.componentRef.setInput('searchable', true);
    fixture.componentRef.setInput('value', value);
    fixture.detectChanges();
    const host = fixture.nativeElement as HTMLElement;
    return { fixture, host };
  }

  it('indents nested options by level and names only the node itself', () => {
    const { host } = setup('');
    const items = [...host.querySelectorAll<HTMLElement>('[role="option"]')];
    expect(items.map((item) => item.classList.contains('is-tree'))).toEqual([
      false,
      true,
      true,
      true,
    ]);
    expect(items.map((item) => item.classList.contains('is-nested'))).toEqual([
      false,
      false,
      true,
      true,
    ]);
    expect(items[3].style.getPropertyValue('--select-level')).toBe('3');
    expect(items[3].querySelector('strong')!.textContent).toBe('平台部');
    expect(host.querySelector('.select-path')).toBeNull();
  });

  it('flattens while searching and shows where each match sits', () => {
    const { fixture, host } = setup('');
    fixture.componentInstance.filter('平台');
    fixture.detectChanges();
    const items = host.querySelectorAll<HTMLElement>('[role="option"]');
    expect(items).toHaveLength(1);
    expect(items[0].classList.contains('is-nested')).toBe(false);
    expect(items[0].querySelector('.select-path')!.textContent).toBe('星河科技 › 研發處');
  });

  it('shows the chosen node by name and its full path as the trigger title', () => {
    const { host } = setup('p');
    const trigger = host.querySelector('.select-trigger')!;
    expect(trigger.textContent).toContain('平台部');
    expect(trigger.textContent).not.toContain('研發處');
    expect(trigger.querySelector('[title]')!.getAttribute('title')).toBe(
      '星河科技 › 研發處 › 平台部',
    );
  });
});
