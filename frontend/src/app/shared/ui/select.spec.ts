import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it } from 'vitest';
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
