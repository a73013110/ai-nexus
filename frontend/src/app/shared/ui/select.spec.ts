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
    const status = host.querySelector('.option-empty')!;
    expect(status.getAttribute('role')).toBe('status');
    expect(status.textContent).toContain('正在載入選項…');
    expect(host.textContent).not.toContain('沒有符合的選項');

    fixture.componentRef.setInput('loading', false);
    fixture.detectChanges();
    expect(host.querySelector('.option-empty')!.textContent).toContain('沒有符合的選項');
    expect(trigger.disabled).toBe(true);
  });
});

describe('Select tags', () => {
  afterEach(() => TestBed.resetTestingModule());

  const people = [
    { value: 'a', label: '林小安', description: 'lin', tags: [{ label: '已隱藏' }] },
    { value: 'b', label: '陳大文', description: 'chen' },
  ];

  function setup(value: string) {
    const fixture = TestBed.createComponent(Select);
    fixture.componentRef.setInput('label', '人員');
    fixture.componentRef.setInput('options', people);
    fixture.componentRef.setInput('searchable', true);
    fixture.componentRef.setInput('value', value);
    fixture.detectChanges();
    return { fixture, host: fixture.nativeElement as HTMLElement };
  }

  it('shows tags beside the option and the chosen value, and finds options by tag', () => {
    const { fixture, host } = setup('a');
    const tags = (root: Element) =>
      [...root.querySelectorAll('.option-tag')].map((tag) => tag.textContent!.trim());
    const options = host.querySelectorAll('[role="option"]');
    expect(tags(options[0])).toEqual(['已隱藏']);
    expect(tags(options[1])).toEqual([]);
    expect(options[0].querySelector('small')!.textContent).toBe('lin');
    expect(tags(host.querySelector('.select-trigger')!)).toEqual(['已隱藏']);

    fixture.componentInstance.filter('隱藏');
    expect(fixture.componentInstance.visibleOptions().map(({ option }) => option.value)).toEqual([
      'a',
    ]);
  });
});

describe('Select groups', () => {
  afterEach(() => TestBed.resetTestingModule());

  const actions = [
    { value: '', label: '所有動作' },
    { value: 'chat.message.sent', label: '送出訊息', group: '對話', keywords: 'chat.message.sent' },
    {
      value: 'chat.run.cancelled',
      label: '停止生成',
      group: '對話',
      keywords: 'chat.run.cancelled',
    },
    {
      value: 'files.file.deleted',
      label: '刪除檔案',
      group: '檔案庫',
      keywords: 'files.file.deleted',
    },
  ];

  function setup() {
    const fixture = TestBed.createComponent(Select);
    fixture.componentRef.setInput('label', '動作');
    fixture.componentRef.setInput('options', actions);
    fixture.componentRef.setInput('searchable', true);
    fixture.detectChanges();
    return { fixture, host: fixture.nativeElement as HTMLElement };
  }
  const visible = (select: Select) => select.visibleOptions().map(({ option }) => option.value);

  it('puts adjacent options of a group under one labelled heading', () => {
    const { host } = setup();
    const listbox = host.querySelector('[role="listbox"]')!;
    const groups = [...listbox.querySelectorAll('[role="group"]')];
    expect(
      groups.map(
        (group) => host.querySelector('#' + group.getAttribute('aria-labelledby'))!.textContent,
      ),
    ).toEqual(['對話', '檔案庫']);
    expect(groups.map((group) => group.querySelectorAll('[role="option"]').length)).toEqual([2, 1]);
    // 沒有分組的選項（所有動作）直接放在 listbox 底下，標題本身不是選項。
    expect(listbox.querySelectorAll(':scope > [role="option"]').length).toBe(1);
    expect(listbox.querySelectorAll('[role="option"]').length).toBe(4);
  });

  it('finds options by group name or hidden keywords and keeps ids for arrow keys', () => {
    const { fixture, host } = setup();
    const select = fixture.componentInstance;
    select.filter('對話');
    expect(visible(select)).toEqual(['chat.message.sent', 'chat.run.cancelled']);
    select.filter('files.file');
    fixture.detectChanges();
    expect(visible(select)).toEqual(['files.file.deleted']);
    expect(host.querySelector('[role="option"]')!.id).toBe(select.id + '-3');
    expect(host.textContent).not.toContain('files.file.deleted');
    expect(select.active()).toBe(3);
  });
});
