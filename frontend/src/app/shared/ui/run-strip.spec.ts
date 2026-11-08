import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it } from 'vitest';
import { RunStrip, type RunStripItem, barHeights, stripSummary } from './run-strip';

const start = Date.parse('2026-11-08T06:00:00Z');
const run = (id: string, status: string, seconds: number | null): RunStripItem => ({
  id,
  status,
  startedAt: new Date(start).toISOString(),
  finishedAt: seconds === null ? null : new Date(start + seconds * 1000).toISOString(),
});

describe('barHeights', () => {
  it('keeps short runs visible next to long ones and tops out at the longest', () => {
    const [short, long] = barHeights([1_000, 600_000]);
    expect(long).toBe(1);
    expect(short).toBeGreaterThan(0.25);
    expect(barHeights([0, 0])).toEqual([0.25, 0.25]);
  });
});

describe('stripSummary', () => {
  it('counts outcomes, most frequent first', () => {
    expect(
      stripSummary([{ status: 'failed' }, { status: 'succeeded' }, { status: 'succeeded' }]),
    ).toBe('最近 3 次：成功 2、失敗 1');
    expect(stripSummary([])).toBe('還沒有執行紀錄');
  });
});

describe('RunStrip', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('pads to a fixed width and grows in only runs that arrive later', () => {
    const fixture = TestBed.createComponent(RunStrip);
    fixture.componentRef.setInput('slots', 4);
    fixture.componentRef.setInput('runs', [run('a', 'succeeded', 5), run('b', 'failed', 60)]);
    fixture.detectChanges();
    const host = fixture.nativeElement as HTMLElement;
    expect(host.querySelectorAll('.slot')).toHaveLength(2);
    expect(host.querySelectorAll('.bar')).toHaveLength(2);
    expect(host.querySelector('.is-new')).toBeNull();
    expect(host.getAttribute('aria-label')).toBe('最近 2 次：成功 1、失敗 1');

    fixture.componentRef.setInput('runs', [
      run('a', 'succeeded', 5),
      run('b', 'failed', 60),
      run('c', 'running', null),
    ]);
    fixture.detectChanges();
    const bars = [...host.querySelectorAll('.bar')];
    expect(bars.map((bar) => bar.classList.contains('is-new'))).toEqual([false, false, true]);
    expect(bars[2].classList).toContain('is-running');
    expect(host.querySelectorAll('.slot')).toHaveLength(1);
  });
});
