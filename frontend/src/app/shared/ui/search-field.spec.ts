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
});
