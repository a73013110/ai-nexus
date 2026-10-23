import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it } from 'vitest';
import { HeatmapCalendar } from './heatmap-calendar';

describe('HeatmapCalendar', () => {
  afterEach(() => TestBed.resetTestingModule());

  function setup() {
    const fixture = TestBed.createComponent(HeatmapCalendar);
    fixture.componentRef.setInput('label', '每日呼叫');
    fixture.componentRef.setInput('from', '2026-10-01');
    fixture.componentRef.setInput('through', '2026-10-14');
    fixture.componentRef.setInput('points', []);
    fixture.componentRef.setInput('loading', true);
    fixture.detectChanges();
    const host = fixture.nativeElement as HTMLElement;
    return { fixture, host, figure: host.querySelector('figure')! };
  }

  it('holds the grid with a skeleton and never says there is no data while loading', () => {
    const { fixture, host, figure } = setup();
    expect(figure.getAttribute('aria-busy')).toBe('true');
    expect(host.querySelector('.heatmap-skeleton')?.getAttribute('role')).toBe('status');
    expect(host.querySelector('.heatmap-grid')).toBeNull();
    expect(host.textContent).not.toContain('此期間尚無資料');

    fixture.componentRef.setInput('loading', false);
    fixture.detectChanges();
    expect(host.querySelector('.heatmap-grid')).not.toBeNull();
    expect(host.textContent).toContain('此期間尚無資料');
  });

  it('keeps the days and dims them while refreshing', () => {
    const { fixture, host, figure } = setup();
    fixture.componentRef.setInput('loading', false);
    fixture.componentRef.setInput('points', [{ date: '2026-10-05', value: 3 }]);
    fixture.componentRef.setInput('busy', true);
    fixture.detectChanges();
    expect(host.querySelector('.heatmap-grid')).not.toBeNull();
    expect(figure.classList).toContain('is-refreshing');
    expect(figure.getAttribute('aria-busy')).toBe('true');
  });
});
