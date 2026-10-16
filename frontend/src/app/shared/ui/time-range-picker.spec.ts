import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
  CUSTOM_RANGE,
  TimeRangePicker,
  hoursAgo,
  timeRangeBounds,
  timeRangeLabel,
  type TimeRange,
} from './time-range-picker';

const presets = [
  { value: 'all', label: '不限時間' },
  { value: '1h', label: '最近 1 小時', duration: hoursAgo(1) },
];
const now = Date.parse('2026-10-16T00:00:00Z');

describe('time range', () => {
  it('ends a preset a minute after now and leaves an unbounded preset open', () => {
    expect(timeRangeBounds({ preset: '1h', from: '', to: '' }, presets, now)).toEqual({
      from: '2026-10-15T23:01:00.000Z',
      to: '2026-10-16T00:01:00.000Z',
    });
    expect(timeRangeBounds({ preset: 'all', from: '', to: '' }, presets, now)).toEqual({});
    expect(timeRangeBounds({ preset: 'gone', from: '', to: '' }, presets, now)).toBe(
      '請選擇時間範圍。',
    );
  });

  it('reads custom ends as Taipei wall-clock, a date alone to its next midnight', () => {
    expect(
      timeRangeBounds(
        { preset: CUSTOM_RANGE, from: '2026-10-01T08:00:00', to: '2026-10-01T09:30:00' },
        presets,
      ),
    ).toEqual({ from: '2026-10-01T00:00:00.000Z', to: '2026-10-01T01:30:00.000Z' });
    expect(
      timeRangeBounds({ preset: CUSTOM_RANGE, from: '2026-10-01', to: '2026-10-01' }, presets),
    ).toEqual({ from: '2026-09-30T16:00:00.000Z', to: '2026-10-01T16:00:00.000Z' });
    expect(timeRangeBounds({ preset: CUSTOM_RANGE, from: '', to: '2026-10-01' }, presets)).toEqual({
      from: undefined,
      to: '2026-10-01T16:00:00.000Z',
    });
  });

  it('rejects inverted or malformed custom ends', () => {
    expect(
      timeRangeBounds(
        { preset: CUSTOM_RANGE, from: '2026-10-02T00:00:00', to: '2026-10-01T00:00:00' },
        presets,
      ),
    ).toBe('結束時間須晚於起始時間。');
    expect(timeRangeBounds({ preset: CUSTOM_RANGE, from: '2026-13-01', to: '' }, presets)).toBe(
      '請輸入有效的起訖時間。',
    );
  });

  it('labels presets and custom ranges', () => {
    expect(timeRangeLabel({ preset: '1h', from: '', to: '' }, presets)).toBe('最近 1 小時');
    expect(
      timeRangeLabel({ preset: CUSTOM_RANGE, from: '2026-10-01', to: '2026-10-03' }, presets),
    ).toBe('2026/10/01 — 10/03');
    expect(
      timeRangeLabel(
        { preset: CUSTOM_RANGE, from: '2025-12-31T08:00:00', to: '2026-01-01T09:30:00' },
        presets,
      ),
    ).toBe('2025/12/31 08:00:00 — 2026/01/01 09:30:00');
    expect(timeRangeLabel({ preset: CUSTOM_RANGE, from: '2026-10-01', to: '' }, presets)).toBe(
      '2026/10/01 起',
    );
  });
});

describe('TimeRangePicker', () => {
  beforeEach(() => {
    HTMLElement.prototype.showPopover = vi.fn();
    HTMLElement.prototype.hidePopover = vi.fn();
  });
  afterEach(() => {
    TestBed.resetTestingModule();
    delete (HTMLElement.prototype as Partial<HTMLElement>).showPopover;
    delete (HTMLElement.prototype as Partial<HTMLElement>).hidePopover;
  });

  function setup() {
    const fixture = TestBed.createComponent(TimeRangePicker);
    const emitted: TimeRange[] = [];
    fixture.componentInstance.valueChange.subscribe((value) => emitted.push(value));
    fixture.componentRef.setInput('presets', presets);
    fixture.componentRef.setInput('value', { preset: 'all', from: '', to: '' });
    fixture.componentRef.setInput('withTime', false);
    fixture.detectChanges();
    const host = fixture.nativeElement as HTMLElement;
    const trigger = host.querySelector<HTMLButtonElement>('.ui-time-range-trigger')!;
    trigger.click();
    fixture.detectChanges();
    const apply = [...host.querySelectorAll<HTMLButtonElement>('button')].find(
      (button) => button.textContent?.trim() === '套用自訂',
    )!;
    return { fixture, emitted, host, trigger, apply };
  }

  it('waits for 套用自訂 before asking for the other end, without disabling the button', () => {
    const { fixture, emitted, host, apply } = setup();
    const picker = fixture.componentInstance;
    picker.from.set('2026-10-01');
    fixture.detectChanges();
    // 輸入到一半不報錯。
    expect(host.querySelector('[role="alert"]')).toBeNull();
    apply.click();
    fixture.detectChanges();
    expect(host.querySelector('[role="alert"]')?.textContent).toContain('請填寫起訖兩端');
    expect(apply.disabled).toBe(false);
    // 沒有真的停用，按下後焦點仍留在按鈕上（jsdom 無法聚焦未開啟的 popover，這裡只驗屬性）。
    expect(apply.getAttribute('aria-disabled')).toBe('true');
    expect(emitted).toEqual([]);

    picker.to.set('2026-10-03');
    fixture.detectChanges();
    expect(host.querySelector('[role="alert"]')).toBeNull();
    apply.click();
    expect(emitted).toEqual([{ preset: CUSTOM_RANGE, from: '2026-10-01', to: '2026-10-03' }]);
  });

  it('closes with Escape and returns focus to the trigger', () => {
    const { fixture, host, trigger } = setup();
    expect(trigger.getAttribute('aria-expanded')).toBe('true');
    host
      .querySelector('[role="dialog"]')!
      .dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    fixture.detectChanges();
    expect(trigger.getAttribute('aria-expanded')).toBe('false');
    expect(document.activeElement).toBe(trigger);
  });

  it('chooses a preset from the panel', () => {
    const { emitted, host } = setup();
    [...host.querySelectorAll<HTMLButtonElement>('.ui-time-range-presets button')]
      .find((button) => button.textContent?.includes('最近 1 小時'))!
      .click();
    expect(emitted).toEqual([{ preset: '1h', from: '', to: '' }]);
  });
});
