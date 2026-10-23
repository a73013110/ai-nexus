import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it } from 'vitest';
import { Sparkline } from './sparkline';

describe('Sparkline', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('holds its height with a skeleton while loading and dims the line while refreshing', () => {
    const fixture = TestBed.createComponent(Sparkline);
    fixture.componentRef.setInput('values', []);
    fixture.componentRef.setInput('label', '每週趨勢');
    fixture.componentRef.setInput('loading', true);
    fixture.detectChanges();
    const host = fixture.nativeElement as HTMLElement;
    expect(host.querySelector('.skeleton')?.getAttribute('aria-label')).toBe('正在載入每週趨勢');
    expect(host.querySelector('svg')).toBeNull();
    expect(host.getAttribute('aria-busy')).toBe('true');

    fixture.componentRef.setInput('loading', false);
    fixture.componentRef.setInput('values', [3, 5, 4]);
    fixture.componentRef.setInput('busy', true);
    fixture.detectChanges();
    expect(host.querySelector('.skeleton')).toBeNull();
    expect(host.querySelector('svg')).not.toBeNull();
    expect(host.classList).toContain('is-refreshing');

    fixture.componentRef.setInput('busy', false);
    fixture.detectChanges();
    expect(host.classList).not.toContain('is-refreshing');
    expect(host.hasAttribute('aria-busy')).toBe(false);
  });
});
