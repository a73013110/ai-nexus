import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it } from 'vitest';
import { ServerClock } from '../browser/server-clock';
import { TimeLanes, timeTicks } from './time-lanes';

const HOUR = 3_600_000;
// 台北 2026-11-08 12:00。
const FROM = Date.parse('2026-11-08T04:00:00Z');
const TO = FROM + 24 * HOUR;

describe('timeTicks', () => {
  it('aligns to workspace hours and writes the date at midnight', () => {
    const ticks = timeTicks(FROM, TO, 3);
    expect(ticks).toHaveLength(9);
    expect(ticks.slice(0, 5).map((tick) => tick.label)).toEqual([
      '12:00',
      '15:00',
      '18:00',
      '21:00',
      '11/9',
    ]);
    expect(ticks[0].position).toBe(0);
    expect(ticks[8].position).toBe(100);
  });

  it('starts at the first whole hour inside the range', () => {
    const ticks = timeTicks(FROM + HOUR / 2, FROM + 4 * HOUR, 3);
    expect(ticks.map((tick) => tick.label)).toEqual(['15:00']);
  });
});

describe('TimeLanes', () => {
  afterEach(() => TestBed.resetTestingModule());

  function render(now: number) {
    TestBed.inject(ServerClock).sync(new Date(now).toISOString(), Date.now());
    const fixture = TestBed.createComponent(TimeLanes);
    fixture.componentRef.setInput('from', FROM);
    fixture.componentRef.setInput('to', TO);
    fixture.componentRef.setInput('label', '時間軸');
    fixture.componentRef.setInput('shades', [{ from: FROM + 12 * HOUR, to: FROM + 18 * HOUR }]);
    fixture.detectChanges();
    return { fixture, host: fixture.nativeElement as HTMLElement };
  }

  it('draws the now line once, positioned through a CSS variable', () => {
    const { host } = render(FROM + 6 * HOUR);
    expect(host.style.getPropertyValue('--time-now')).toBe('25%');
    expect(host.querySelectorAll('.time-now-line')).toHaveLength(1);
    expect(host.querySelector('.time-now')?.textContent).toBe('現在 18:00');
    expect(host.getAttribute('aria-label')).toBe('時間軸');
  });

  it('only moves the now line every thirty seconds', () => {
    const { fixture, host } = render(FROM + 6 * HOUR);
    TestBed.inject(ServerClock).sync(new Date(FROM + 6 * HOUR + 29_000).toISOString(), Date.now());
    fixture.detectChanges();
    expect(host.style.getPropertyValue('--time-now')).toBe('25%');
  });

  it('hides the now line outside the range and places shades by percentage', () => {
    const { host } = render(TO + HOUR);
    expect(host.querySelector('.time-now-line')).toBeNull();
    const shade = host.querySelector<HTMLElement>('.time-shade')!;
    expect(shade.style.width).toBe('25%');
    expect(shade.style.insetInlineStart).toBe('50%');
  });
});
