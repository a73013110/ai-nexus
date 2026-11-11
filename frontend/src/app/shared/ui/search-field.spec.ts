import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it } from 'vitest';
import { SearchField } from './search-field';

describe('SearchField', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('swaps the magnifier for a spinner while busy and keeps the field typeable', () => {
    const fixture = TestBed.createComponent(SearchField);
    fixture.componentRef.setInput('label', '搜尋專案');
    fixture.detectChanges();
    const host = fixture.nativeElement as HTMLElement;
    const field = host.querySelector('input')!;
    const icon = () => host.querySelector('.search-field > nx-icon')!;
    expect(field.hasAttribute('aria-busy')).toBe(false);
    expect(icon().classList).not.toContain('is-spinning');

    fixture.componentRef.setInput('busy', true);
    fixture.detectChanges();
    expect(field.getAttribute('aria-busy')).toBe('true');
    expect(field.disabled).toBe(false);
    expect(field.readOnly).toBe(false);
    expect(icon().classList).toContain('is-spinning');

    const typed: string[] = [];
    fixture.componentInstance.valueChange.subscribe((value) => typed.push(value));
    field.value = '星河';
    field.dispatchEvent(new Event('input'));
    expect(typed).toEqual(['星河']);
  });

  it('marks the host with its appearance so the frame comes from one place', () => {
    const fixture = TestBed.createComponent(SearchField);
    fixture.componentRef.setInput('label', '搜尋動作');
    fixture.detectChanges();
    const host = fixture.nativeElement as HTMLElement;
    expect(host.className).toBe('');

    fixture.componentRef.setInput('appearance', 'filled');
    fixture.detectChanges();
    expect(host.classList).toContain('search-filled');
    expect(host.classList).not.toContain('search-bare');

    fixture.componentRef.setInput('appearance', 'bare');
    fixture.detectChanges();
    expect(host.classList).toContain('search-bare');
    expect(host.classList).not.toContain('search-filled');
  });

  it('becomes a combobox for the list it filters and keeps the clear button out of the tab order', () => {
    const fixture = TestBed.createComponent(SearchField);
    fixture.componentRef.setInput('label', '搜尋人員');
    fixture.detectChanges();
    const host = fixture.nativeElement as HTMLElement;
    const field = host.querySelector('input')!;
    expect(field.hasAttribute('role')).toBe(false);
    expect(field.hasAttribute('aria-controls')).toBe(false);

    fixture.componentRef.setInput('controls', 'people');
    fixture.componentRef.setInput('popup', 'tree');
    fixture.componentRef.setInput('activeDescendant', 'people-2');
    fixture.componentRef.setInput('value', '林');
    fixture.detectChanges();
    expect(field.getAttribute('role')).toBe('combobox');
    expect(field.getAttribute('aria-controls')).toBe('people');
    expect(field.getAttribute('aria-haspopup')).toBe('tree');
    expect(field.getAttribute('aria-activedescendant')).toBe('people-2');
    expect(field.getAttribute('autocomplete')).toBe('off');
    const clear = host.querySelector<HTMLButtonElement>('button')!;
    expect(clear.tabIndex).toBe(-1);

    const cleared: string[] = [];
    fixture.componentInstance.valueChange.subscribe((value) => cleared.push(value));
    clear.click();
    expect(cleared).toEqual(['']);
    expect(document.activeElement).toBe(field);
  });
});
