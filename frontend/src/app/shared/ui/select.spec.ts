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
