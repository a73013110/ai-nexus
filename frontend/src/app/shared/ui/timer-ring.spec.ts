import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it } from 'vitest';
import { ServerClock } from '../browser/server-clock';
import { TimerRing, remainingRatio } from './timer-ring';

const at = (milliseconds: number) => new Date(milliseconds).toISOString();

describe('remainingRatio', () => {
  it('is the share of the window still ahead, clamped, and null for a window it cannot use', () => {
    expect(remainingRatio(at(0), at(100_000), 25_000)).toBeCloseTo(0.75);
    expect(remainingRatio(at(0), at(100_000), 200_000)).toBe(0);
    expect(remainingRatio(at(0), at(100_000), -5_000)).toBe(1);
    expect(remainingRatio(null, at(100_000), 0)).toBeNull();
    expect(remainingRatio(at(100_000), at(0), 0)).toBeNull();
  });
});

describe('TimerRing', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('depletes while counting down and spins while running without progress', () => {
    const clock = TestBed.inject(ServerClock);
    const base = Date.now();
    clock.sync(at(base), Date.now());
    const fixture = TestBed.createComponent(TimerRing);
    fixture.componentRef.setInput('from', at(base - 30_000));
    fixture.componentRef.setInput('to', at(base + 90_000));
    fixture.detectChanges();
    const host = fixture.nativeElement as HTMLElement;
    const offset = () =>
      Number.parseFloat((host.querySelector('.arc') as SVGElement).style.strokeDashoffset);
    expect(offset()).toBeGreaterThan(24);
    expect(offset()).toBeLessThan(26);
    expect(host.getAttribute('aria-hidden')).toBe('true');

    fixture.componentRef.setInput('mode', 'running');
    fixture.detectChanges();
    expect(host.classList).toContain('is-indeterminate');

    fixture.componentRef.setInput('progress', 0.4);
    fixture.detectChanges();
    expect(host.classList).not.toContain('is-indeterminate');
    expect(offset()).toBeCloseTo(60);

    fixture.componentRef.setInput('mode', 'paused');
    fixture.detectChanges();
    expect(host.getAttribute('data-tone')).toBe('warning');
  });
});
