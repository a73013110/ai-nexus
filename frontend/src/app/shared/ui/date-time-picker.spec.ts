import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';
import { DateTimePicker } from './date-time-picker';

function setup() {
  const fixture = TestBed.createComponent(DateTimePicker);
  fixture.componentRef.setInput('label', '起日');
  fixture.componentRef.setInput('withTime', false);
  fixture.detectChanges();
  const root = fixture.nativeElement as HTMLElement;
  const month = () => root.querySelector('[aria-live="polite"]')!.textContent!.trim();
  const wheel = (deltaY: number, timeStamp: number) => {
    const event = new WheelEvent('wheel', { deltaY, cancelable: true, bubbles: true });
    Object.defineProperty(event, 'timeStamp', { value: timeStamp });
    root.querySelector('.ui-calendar')!.dispatchEvent(event);
    fixture.detectChanges();
    return event;
  };
  return { fixture, root, month, wheel };
}

describe('DateTimePicker', () => {
  it('turns the month with the wheel, one notch per month, without scrolling the panel', () => {
    const { fixture, month, wheel } = setup();
    fixture.componentInstance.view.set({
      year: 2026,
      month: 3,
      day: 1,
      hour: 0,
      minute: 0,
      second: 0,
    });
    fixture.detectChanges();
    expect(month()).toBe('2026 年 3 月');

    expect(wheel(100, 1000).defaultPrevented).toBe(true);
    expect(month()).toBe('2026 年 4 月');
    // 換月後 150ms 內的捲動（觸控板的慣性）不再換。
    wheel(100, 1050);
    expect(month()).toBe('2026 年 4 月');
    wheel(-100, 1300);
    expect(month()).toBe('2026 年 3 月');
    // 觸控板的小幅捲動累積到門檻才換。
    wheel(-30, 1600);
    expect(month()).toBe('2026 年 3 月');
    wheel(-40, 1620);
    expect(month()).toBe('2026 年 2 月');
  });

  it('marks the weekend columns and the days outside the month', () => {
    const { fixture, root } = setup();
    fixture.componentInstance.view.set({
      year: 2026,
      month: 10,
      day: 1,
      hour: 0,
      minute: 0,
      second: 0,
    });
    fixture.detectChanges();
    const heads = [...root.querySelectorAll('.ui-calendar thead th')];
    expect(
      heads.filter((th) => th.classList.contains('is-weekend')).map((th) => th.textContent!.trim()),
    ).toEqual(['日', '六']);
    const first = root.querySelector('.ui-calendar tbody tr')!;
    // 2026 年 10 月 1 日是星期四：第一週前四天是九月。
    expect(
      [...first.querySelectorAll('button.is-outside')].map((button) => button.textContent!.trim()),
    ).toEqual(['27', '28', '29', '30']);
    expect(first.querySelector('td:first-child')!.classList).toContain('is-weekend');
    expect(first.querySelector('td:last-child')!.classList).toContain('is-weekend');
  });
});
