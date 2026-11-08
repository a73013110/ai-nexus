import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ServerClock } from '../browser/server-clock';
import { TickingTime, clockText, compactClockText, spokenDuration } from './ticking-time';

const at = (milliseconds: number) => new Date(milliseconds).toISOString();

describe('clockText', () => {
  it('shows minutes and seconds within an hour, hours within a day and days beyond', () => {
    expect(clockText(0)).toBe('0:00');
    expect(clockText(65_400)).toBe('1:05');
    expect(clockText(2 * 3_600_000 + 4 * 60_000 + 5_000)).toBe('2:04:05');
    expect(clockText(2 * 86_400_000 + 3 * 3_600_000)).toBe('2 天 3 小時');
    expect(clockText(86_400_000)).toBe('1 天');
    expect(clockText(-5_000)).toBe('0:00');
  });
});

describe('compactClockText', () => {
  it('stays within five characters for a ring centre', () => {
    expect(compactClockText(65_400)).toBe('1:05');
    expect(compactClockText(59 * 60_000 + 59_000)).toBe('59:59');
    expect(compactClockText(2 * 3_600_000 + 4 * 60_000 + 5_000)).toBe('2h04');
    expect(compactClockText(3 * 86_400_000 + 5 * 3_600_000)).toBe('3d');
  });
});

describe('spokenDuration', () => {
  it('rounds to a coarse unit so screen readers are not re-announced every second', () => {
    expect(spokenDuration(30_000)).toBe('不到 1 分鐘');
    expect(spokenDuration(5 * 60_000 + 20_000)).toBe('約 5 分鐘');
    expect(spokenDuration(3 * 3_600_000)).toBe('約 3 小時');
  });
});

describe('TickingTime', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('counts down on the server clock and rolls in only the digit that changed', () => {
    const clock = TestBed.inject(ServerClock);
    const base = Date.now() + 10 * 60_000;
    clock.sync(at(base), Date.now());
    const fixture = TestBed.createComponent(TickingTime);
    fixture.componentRef.setInput('to', at(base + 65_500));
    fixture.detectChanges();
    const host = fixture.nativeElement as HTMLElement;
    const glyphs = () => [...host.querySelectorAll('.glyph')];
    expect(
      glyphs()
        .map((glyph) => glyph.textContent)
        .join(''),
    ).toBe('1:05');
    expect(host.querySelector('.sr-only')?.textContent).toBe('剩約 1 分鐘');
    expect(host.getAttribute('data-direction')).toBe('down');
    expect(host.querySelector('.is-new')).toBeNull();

    clock.sync(at(base + 1000), Date.now());
    fixture.detectChanges();
    expect(
      glyphs()
        .map((glyph) => glyph.textContent)
        .join(''),
    ).toBe('1:04');
    expect(glyphs().map((glyph) => glyph.classList.contains('is-new'))).toEqual([
      false,
      false,
      false,
      true,
    ]);
  });

  it('shows elapsed time from a start and announces reaching zero once', () => {
    const clock = TestBed.inject(ServerClock);
    const base = Date.now();
    clock.sync(at(base), Date.now());
    const fixture = TestBed.createComponent(TickingTime);
    const reached = vi.fn();
    fixture.componentInstance.reached.subscribe(reached);
    fixture.componentRef.setInput('to', at(base - 1000));
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('0:00');
    fixture.detectChanges();
    expect(reached).toHaveBeenCalledTimes(1);

    fixture.componentRef.setInput('to', null);
    fixture.componentRef.setInput('from', at(base - 125_500));
    fixture.detectChanges();
    const host = fixture.nativeElement as HTMLElement;
    expect(host.getAttribute('data-direction')).toBe('up');
    expect(host.querySelector('.sr-only')?.textContent).toBe('已經過約 2 分鐘');
  });

  it('shows the unknown text without a time', () => {
    const fixture = TestBed.createComponent(TickingTime);
    fixture.componentRef.setInput('unknown', '未排定');
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('.sr-only')?.textContent).toBe(
      '未排定',
    );
  });
});
