import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { RefreshButton } from './refresh-button';

@Component({
  selector: 'nx-refresh-host',
  imports: [RefreshButton],
  template: `<button
    nxRefresh
    aria-label="重新讀取目錄"
    [busy]="busy()"
    (refresh)="reads = reads + 1"
  ></button>`,
})
class Host {
  readonly busy = signal(false);
  reads = 0;
}

function setup() {
  const fixture = TestBed.createComponent(Host);
  fixture.detectChanges();
  const button = (fixture.nativeElement as HTMLElement).querySelector('button')!;
  const icon = () => button.querySelector('nx-icon')!;
  return { fixture, host: fixture.componentInstance, button, icon };
}

describe('RefreshButton', () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => {
    vi.useRealTimers();
    TestBed.resetTestingModule();
  });

  it('names itself from aria-label and turns for at least one spin even when the read is quick', () => {
    const { fixture, host, button, icon } = setup();
    expect(button.type).toBe('button');
    expect(button.title).toBe('重新讀取目錄');
    expect(button.getAttribute('aria-disabled')).toBeNull();

    button.click();
    fixture.detectChanges();
    expect(host.reads).toBe(1);
    expect(button.getAttribute('aria-busy')).toBe('true');
    expect(icon().classList).toContain('is-spinning');

    // 讀取已經結束，仍轉完一圈才停。
    vi.advanceTimersByTime(500);
    fixture.detectChanges();
    expect(icon().classList).toContain('is-spinning');
    vi.advanceTimersByTime(500);
    fixture.detectChanges();
    expect(icon().classList).not.toContain('is-spinning');
    expect(button.getAttribute('aria-disabled')).toBeNull();
  });

  it('ignores presses while the read is running but keeps the focus on the button', () => {
    const { fixture, host, button } = setup();
    host.busy.set(true);
    fixture.detectChanges();
    expect(button.getAttribute('aria-disabled')).toBe('true');
    expect(button.disabled).toBe(false);
    button.click();
    expect(host.reads).toBe(0);

    host.busy.set(false);
    fixture.detectChanges();
    button.click();
    expect(host.reads).toBe(1);
  });
});
