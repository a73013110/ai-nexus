import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it } from 'vitest';
import {
  CalendarDayTemplate,
  MonthCalendar,
  addMonths,
  calendarKeyTarget,
  fullDateLabel,
  monthGrid,
  type CalendarDay,
  type WeekStart,
} from './month-calendar';

describe('monthGrid', () => {
  it('pads whole weeks from the chosen first weekday', () => {
    // 2026-10-01 是星期四。
    const monday = monthGrid('2026-10', 1);
    expect(monday).toHaveLength(5);
    expect(monday[0][0].date).toBe('2026-09-28');
    expect(monday[0][0].inMonth).toBe(false);
    expect(monday[0][3]).toMatchObject({ date: '2026-10-01', day: 1, weekday: 4, inMonth: true });
    expect(monday[4][6].date).toBe('2026-11-01');
    const sunday = monthGrid('2026-10', 0);
    expect(sunday[0][0].date).toBe('2026-09-27');
    expect(sunday[0][0].weekday).toBe(0);
    expect(sunday[0][0].weekend).toBe(true);
  });

  it('uses only the weeks the month needs unless the height is fixed', () => {
    // 2026-02-01 是星期日：週日起始剛好四週，週一起始要五週。
    expect(monthGrid('2026-02', 0)).toHaveLength(4);
    expect(monthGrid('2026-02', 1)).toHaveLength(5);
    expect(monthGrid('2026-02', 0, true)).toHaveLength(6);
    expect(monthGrid('2026-02', 0, true)[5][6].date).toBe('2026-03-14');
  });
});

describe('calendar keys', () => {
  it('moves by day, week, week edge and month', () => {
    expect(calendarKeyTarget('ArrowRight', '2026-10-31')).toBe('2026-11-01');
    expect(calendarKeyTarget('ArrowUp', '2026-10-05')).toBe('2026-09-28');
    // 2026-10-07 是星期三。
    expect(calendarKeyTarget('Home', '2026-10-07')).toBe('2026-10-04');
    expect(calendarKeyTarget('End', '2026-10-07')).toBe('2026-10-10');
    expect(calendarKeyTarget('Home', '2026-10-07', { weekStart: 1 })).toBe('2026-10-05');
    expect(calendarKeyTarget('PageUp', '2026-03-31')).toBe('2026-02-28');
    expect(calendarKeyTarget('PageDown', '2024-02-29', { shift: true })).toBe('2025-02-28');
    expect(calendarKeyTarget('Tab', '2026-10-07')).toBeNull();
    expect(addMonths('2026-12-15', 1)).toBe('2027-01-15');
  });

  it('names the whole date in Traditional Chinese', () => {
    expect(fullDateLabel('2026-10-05')).toBe('2026年10月5日 星期一');
  });
});

@Component({
  selector: 'nx-calendar-host',
  imports: [MonthCalendar, CalendarDayTemplate],
  template: `<nx-month-calendar
    label="示範行事曆"
    today="2026-10-22"
    [(month)]="month"
    [(selected)]="selected"
    [days]="days()"
    [weekStart]="weekStart()"
    [interactive]="interactive()"
    (dayActivate)="activated.push($event)"
  >
    @if (custom()) {
      <ng-template [nxCalendarDay]="days()" let-cell let-day="day" let-today="today">
        <em class="custom">{{ cell.day }}{{ today ? '今' : '' }}{{ day?.description }}</em>
      </ng-template>
    }
  </nx-month-calendar>`,
})
class Host {
  readonly month = signal('2026-10');
  readonly selected = signal<string | null>(null);
  readonly weekStart = signal<WeekStart>(1);
  readonly interactive = signal(true);
  readonly custom = signal(false);
  readonly days = signal<CalendarDay[]>([
    {
      date: '2026-10-10',
      description: '國慶日',
      marks: [{ label: '放假', tone: 'accent' }],
    },
    { date: '2026-10-12', marks: [{ label: '出勤', tone: 'success', count: 12 }] },
    { date: '2026-10-13', disabled: true, description: '未建立' },
  ]);
  readonly activated: string[] = [];
}

function setup() {
  const fixture = TestBed.createComponent(Host);
  fixture.detectChanges();
  const root = fixture.nativeElement as HTMLElement;
  const cell = (date: string) =>
    root.querySelector<HTMLElement>(`.calendar-day[data-date="${date}"]`)!;
  const press = async (key: string, init: KeyboardEventInit = {}) => {
    const target = root.querySelector<HTMLElement>('.calendar-day[tabindex="0"]')!;
    target.dispatchEvent(new KeyboardEvent('keydown', { key, bubbles: true, ...init }));
    await fixture.whenStable();
  };
  return { fixture, host: fixture.componentInstance, root, cell, press };
}

describe('MonthCalendar', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('lays out a labelled grid with zh-TW weekdays, today and outside days', () => {
    const { root, cell } = setup();
    const grid = root.querySelector('[role="grid"]')!;
    expect(grid.getAttribute('aria-label')).toBe('示範行事曆，2026 年 10 月');
    const headers = [...root.querySelectorAll('[role="columnheader"]')].map((item) =>
      item.textContent?.trim(),
    );
    expect(headers).toEqual(['一', '二', '三', '四', '五', '六', '日']);
    expect(root.querySelectorAll('[role="row"]')).toHaveLength(6);
    expect(cell('2026-10-22').getAttribute('aria-current')).toBe('date');
    expect(cell('2026-10-22').getAttribute('aria-label')).toBe('2026年10月22日 星期四，今天');
    // 只有今天在 Tab 順序裡（roving tabindex）。
    expect(root.querySelectorAll('.calendar-day[tabindex="0"]')).toHaveLength(1);
    expect(cell('2026-10-22').tabIndex).toBe(0);
    expect(cell('2026-09-28').classList).toContain('is-outside');
    expect(cell('2026-09-28').querySelector('.calendar-content')).toBeNull();
  });

  it('starts the week on Sunday when asked', async () => {
    const { fixture, host, root } = setup();
    host.weekStart.set(0);
    await fixture.whenStable();
    expect(root.querySelector('[role="columnheader"]')?.textContent?.trim()).toBe('日');
    expect(root.querySelector('.calendar-day')?.getAttribute('data-date')).toBe('2026-09-27');
  });

  it('renders the day marks and reads them in the cell label', () => {
    const { cell } = setup();
    expect(cell('2026-10-10').getAttribute('aria-label')).toBe('2026年10月10日 星期六，國慶日');
    expect(cell('2026-10-10').querySelector('.calendar-mark')?.getAttribute('data-tone')).toBe(
      'accent',
    );
    expect(cell('2026-10-12').getAttribute('aria-label')).toContain('出勤 12');
    expect(cell('2026-10-12').querySelector('.calendar-mark b')?.textContent).toBe('12');
  });

  it('moves focus with the keyboard and turns the month at its edges', async () => {
    const { host, cell, press } = setup();
    cell('2026-10-22').focus();
    await press('ArrowRight');
    expect(document.activeElement).toBe(cell('2026-10-23'));
    await press('ArrowDown');
    expect(document.activeElement).toBe(cell('2026-10-30'));
    await press('ArrowDown');
    expect(host.month()).toBe('2026-11');
    expect(document.activeElement).toBe(cell('2026-11-06'));
    expect(cell('2026-11-06').tabIndex).toBe(0);
    await press('PageUp');
    expect(host.month()).toBe('2026-10');
    expect(document.activeElement).toBe(cell('2026-10-06'));
  });

  it('selects with Enter, Space or a click but not a disabled day', async () => {
    const { fixture, host, cell, press } = setup();
    cell('2026-10-22').focus();
    await press('Enter');
    expect(host.selected()).toBe('2026-10-22');
    expect(cell('2026-10-22').getAttribute('aria-selected')).toBe('true');
    await press('ArrowLeft');
    await press(' ');
    cell('2026-10-13').click();
    await fixture.whenStable();
    expect(host.activated).toEqual(['2026-10-22', '2026-10-21']);
    expect(cell('2026-10-13').getAttribute('aria-disabled')).toBe('true');
    expect(cell('2026-10-13').tabIndex).toBe(0);
  });

  it('turns to the month of an outside day instead of selecting it', async () => {
    const { fixture, host, cell } = setup();
    cell('2026-09-28').click();
    await fixture.whenStable();
    expect(host.month()).toBe('2026-09');
    expect(host.activated).toEqual([]);
    expect(document.activeElement).toBe(cell('2026-09-28'));
  });

  it('only browses when not interactive', async () => {
    const { fixture, host, root, cell } = setup();
    host.interactive.set(false);
    await fixture.whenStable();
    cell('2026-10-12').click();
    await fixture.whenStable();
    expect(host.activated).toEqual([]);
    expect(host.selected()).toBeNull();
    expect(root.querySelector('[role="grid"]')?.getAttribute('aria-readonly')).toBe('true');
    expect(cell('2026-10-12').hasAttribute('aria-selected')).toBe(false);
  });

  it('hands each day of the month to a custom template', async () => {
    const { fixture, host, cell } = setup();
    host.custom.set(true);
    await fixture.whenStable();
    expect(cell('2026-10-10').querySelector('.custom')?.textContent).toBe('10國慶日');
    expect(cell('2026-10-22').querySelector('.custom')?.textContent).toBe('22今');
    expect(cell('2026-10-10').querySelector('.calendar-mark')).toBeNull();
  });

  it('changes month from its header', async () => {
    const { fixture, host, root } = setup();
    const [today, previous] = root.querySelectorAll<HTMLButtonElement>('.calendar-nav button');
    previous.click();
    await fixture.whenStable();
    expect(host.month()).toBe('2026-09');
    expect(root.querySelector('.calendar-title')?.textContent).toBe('2026 年 9 月');
    today.click();
    await fixture.whenStable();
    expect(host.month()).toBe('2026-10');
  });
});
