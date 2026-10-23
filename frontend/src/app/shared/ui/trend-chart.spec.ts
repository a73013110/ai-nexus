import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it } from 'vitest';
import { TrendChart } from './trend-chart';

const points = [
  { label: '10/01', value: 4 },
  { label: '10/02', value: 9 },
];

describe('TrendChart', () => {
  afterEach(() => TestBed.resetTestingModule());

  function setup(loading: boolean) {
    const fixture = TestBed.createComponent(TrendChart);
    fixture.componentRef.setInput('label', '每日呼叫');
    fixture.componentRef.setInput('points', []);
    fixture.componentRef.setInput('loading', loading);
    fixture.detectChanges();
    const host = fixture.nativeElement as HTMLElement;
    return { fixture, host, figure: host.querySelector('figure')! };
  }

  it('draws a skeleton instead of the empty copy while it loads', () => {
    const { fixture, host, figure } = setup(true);
    expect(figure.getAttribute('aria-busy')).toBe('true');
    expect(host.querySelector('[role="status"]')?.textContent).toContain('正在載入每日呼叫…');
    expect(host.querySelector('.skeleton')).not.toBeNull();
    expect(host.textContent).not.toContain('此期間尚無資料');

    fixture.componentRef.setInput('loading', false);
    fixture.detectChanges();
    expect(figure.hasAttribute('aria-busy')).toBe(false);
    expect(host.textContent).toContain('此期間尚無資料');
  });

  it('keeps the line and dims it while refreshing', () => {
    const { fixture, host, figure } = setup(false);
    fixture.componentRef.setInput('points', points);
    fixture.componentRef.setInput('busy', true);
    fixture.detectChanges();
    expect(host.querySelector('.trend-line')).not.toBeNull();
    expect(host.querySelector('.skeleton')).toBeNull();
    expect(figure.classList).toContain('is-refreshing');
    expect(figure.getAttribute('aria-busy')).toBe('true');
  });
});
