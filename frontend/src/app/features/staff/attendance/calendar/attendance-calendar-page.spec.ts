import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { afterEach, beforeAll, describe, expect, it, vi } from 'vitest';
import type { CalendarDayDto } from '@core/api/schema';
import { AuthService } from '@core/auth/auth-service';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { ApiError } from '@core/errors/safe-errors';
import { tourSteps, type ProductTourDefinition } from '@shared/ui/product-tour';
import { ToastService } from '@shared/ui/toast-service';
import { AttendanceApi } from '../attendance-api';
import { AttendanceCalendarPage } from './attendance-calendar-page';
import { CalendarDayDialog } from './calendar-day-dialog';

const ISSUE = 'NX-' + 'A'.repeat(32);
/** GD 只有月初三天：其餘日期顯示「未建立」。 */
const gdDays = (from: string): CalendarDayDto[] => [
  { date: from, morningOff: true, afternoonOff: true, remark: '國慶日' },
  { date: from.slice(0, 8) + '02', morningOff: false, afternoonOff: true, remark: null },
  { date: from.slice(0, 8) + '03', morningOff: false, afternoonOff: false, remark: null },
];

function setup({
  read = async (from: string): Promise<CalendarDayDto[]> => gdDays(from),
  update = async (_date: string, _body: Omit<CalendarDayDto, 'date'>): Promise<CalendarDayDto> => {
    throw new ApiError(503, 'attendance_gd_read_only', undefined, ISSUE);
  },
  maintain = true,
} = {}) {
  vi.stubGlobal('matchMedia', (query: string) => ({
    matches: false,
    media: query,
    addEventListener() {},
    removeEventListener() {},
  }));
  const generation = signal(1);
  const session = {
    auth: { generation },
    load: vi.fn(async () => true),
    me: signal({ id: 'u1' }),
    has: (feature: string) =>
      feature === 'attendance.calendar' || (maintain && feature === 'attendance.calendar.maintain'),
    featureName: () => '假日設定',
    testing: signal(false),
    testingNote: signal(''),
  };
  const api = {
    calendarDays: vi.fn((from: string, _to: string) => read(from)),
    updateCalendarDay: vi.fn(update),
  };
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      { provide: WorkspaceSession, useValue: session },
      { provide: AuthService, useValue: { generation, guard: () => () => true } },
      { provide: AttendanceApi, useValue: api },
      { provide: ToastService, useValue: { success: vi.fn() } },
    ],
  });
  const fixture = TestBed.createComponent(AttendanceCalendarPage);
  const root = fixture.nativeElement as HTMLElement;
  const dialog = () =>
    fixture.debugElement.query((item) => item.componentInstance instanceof CalendarDayDialog)
      .componentInstance as CalendarDayDialog;
  /** 本月第 `day` 天的月曆格子。 */
  const cell = (day: number) => {
    const month = fixture.componentInstance.month();
    return root.querySelector<HTMLElement>(
      `.calendar-day[data-date="${month}-${String(day).padStart(2, '0')}"]`,
    )!;
  };
  const showList = async () => {
    root.querySelectorAll<HTMLButtonElement>('.calendar-view-switch button')[1].click();
    await fixture.whenStable();
  };
  return { fixture, root, api, dialog, cell, showList };
}

beforeAll(() => {
  HTMLDialogElement.prototype.showModal ??= function (this: HTMLDialogElement) {
    this.setAttribute('open', '');
  };
  HTMLDialogElement.prototype.close ??= function (this: HTMLDialogElement) {
    this.removeAttribute('open');
  };
});

describe('AttendanceCalendarPage', () => {
  afterEach(() => {
    TestBed.resetTestingModule();
    vi.unstubAllGlobals();
  });

  it('shows the month as a calendar with whole and half days off', async () => {
    const { fixture, root, api, cell } = setup();
    await fixture.whenStable();
    const [from, to] = api.calendarDays.mock.calls[0];
    expect(from).toMatch(/-01$/);
    expect(root.querySelectorAll('.calendar-day:not(.is-outside)')).toHaveLength(
      Number(to.slice(8)),
    );
    expect(cell(1).querySelector('.holiday-shade')?.getAttribute('data-kind')).toBe('DayOff');
    expect(cell(1).textContent).toContain('國慶日');
    expect(cell(1).getAttribute('aria-label')).toContain('放假，國慶日');
    expect(cell(2).querySelector('.holiday-shade')?.getAttribute('data-kind')).toBe('AfternoonOff');
    expect(cell(2).textContent).toContain('下午放假');
    expect(cell(3).getAttribute('aria-label')).toContain('上班日');
    expect(cell(4).textContent).toContain('未建立');
    expect(cell(4).getAttribute('aria-disabled')).toBe('true');
    expect(cell(1).hasAttribute('aria-disabled')).toBe(false);
    expect(root.querySelector('.calendar-off-days')?.textContent).toContain('放假 2 天');
  });

  it('keeps the day-by-day list as a second view of the same month', async () => {
    const { fixture, root, api, showList } = setup();
    await fixture.whenStable();
    await showList();
    const [, to] = api.calendarDays.mock.calls[0];
    const rows = root.querySelectorAll('.calendar-table tbody tr');
    expect(rows).toHaveLength(Number(to.slice(8)));
    expect(rows[0].textContent).toContain('放假');
    expect(rows[0].textContent).toContain('國慶日');
    expect(rows[1].textContent).toContain('下午放假');
    expect(rows[3].textContent).toContain('未建立');
    expect(rows[0].querySelector('.calendar-edit')).not.toBeNull();
    expect(rows[3].querySelector('.calendar-edit')).toBeNull();
    expect(root.querySelector('nx-month-calendar')).toBeNull();
    expect(api.calendarDays).toHaveBeenCalledOnce();
  });

  it('explains an unavailable document system instead of showing an empty month', async () => {
    const { fixture, root, cell, showList } = setup({
      read: async () => {
        throw new ApiError(503, 'attendance_gd_unavailable', undefined, ISSUE);
      },
    });
    await fixture.whenStable();
    const notice = root.querySelector('.calendar-error')!;
    expect(notice.textContent).toContain('公文系統暫時無法連線');
    expect(notice.textContent).toContain(ISSUE);
    expect(cell(4).textContent).not.toContain('未建立');
    await showList();
    const state = root.querySelector('nx-table-state')!;
    expect(state.textContent).toContain('公文系統暫時無法連線');
    expect(root.querySelectorAll('.calendar-table tbody tr')).toHaveLength(0);
  });

  it('shows why a read-only document system refuses the change and stops offering edits', async () => {
    const { fixture, root, api, dialog, cell } = setup();
    await fixture.whenStable();
    cell(1).click();
    await fixture.whenStable();
    expect(cell(1).getAttribute('aria-selected')).toBe('true');
    const editor = dialog();
    expect(editor.title()).toMatch(/^編輯假日：\d{4} 年 \d+ 月 1 日$/);
    editor.update('remark', '補假');
    await editor.save(new Event('submit'));
    await fixture.whenStable();
    expect(api.updateCalendarDay).toHaveBeenCalledWith(
      expect.stringMatching(/-01$/),
      expect.objectContaining({ remark: '補假' }),
    );
    expect(editor.error()).toContain('公文系統的連線設為唯讀');
    expect(root.querySelector('.calendar-day-error')?.textContent).toContain('唯讀');
    expect(root.querySelector('.calendar-read-only')).not.toBeNull();
    expect(root.querySelector('[role="grid"]')?.getAttribute('aria-readonly')).toBe('true');
  });

  it('writes the saved day back into the month without rereading it', async () => {
    const { fixture, root, api, dialog, cell, showList } = setup({
      update: async (date: string, body: Omit<CalendarDayDto, 'date'>) => ({ date, ...body }),
    });
    await fixture.whenStable();
    cell(1).click();
    dialog().choose('FullDay');
    dialog().update('remark', ' 改為上班 ');
    await dialog().save(new Event('submit'));
    await fixture.whenStable();
    expect(api.updateCalendarDay.mock.calls[0][1]).toEqual({
      morningOff: false,
      afternoonOff: false,
      remark: '改為上班',
    });
    expect(cell(1).getAttribute('aria-label')).toContain('上班日，改為上班');
    expect(cell(1).querySelector('.holiday-shade')?.getAttribute('data-kind')).toBe('FullDay');
    await showList();
    const first = root.querySelector('.calendar-table tbody tr')!;
    expect(first.textContent).toContain('改為上班');
    expect(first.textContent).toContain('上班日');
    expect(api.calendarDays).toHaveBeenCalledOnce();
  });

  it('finds every tour target in both views and in the day dialog', async () => {
    const { fixture, root, dialog, cell, showList } = setup();
    await fixture.whenStable();
    const page = fixture.componentInstance;
    const expectTargets = (definition: ProductTourDefinition) => {
      for (const step of tourSteps(definition))
        expect(
          root.querySelector(step.target.replace(/^nx-attendance-calendar-page /, '')),
          step.target,
        ).not.toBeNull();
    };
    expect(root.querySelector('nx-feature-page nx-product-tour-button')).not.toBeNull();
    expectTargets(page.tour());
    cell(1).click();
    await fixture.whenStable();
    expect(root.querySelector('nx-calendar-day-dialog nx-product-tour-button')).not.toBeNull();
    expectTargets(dialog().tour);
    await showList();
    expect(tourSteps(page.tour()).map((step) => step.title)).toContain('逐日清單');
    expectTargets(page.tour());
  });

  it('offers no edits without the maintain action', async () => {
    const { fixture, root, dialog, cell, showList } = setup({ maintain: false });
    await fixture.whenStable();
    expect(root.querySelector('[role="grid"]')?.getAttribute('aria-readonly')).toBe('true');
    cell(1).click();
    await fixture.whenStable();
    expect(dialog().day()).toBeNull();
    await showList();
    expect(root.querySelector('.calendar-edit')).toBeNull();
  });
});

describe('CalendarDayDialog', () => {
  afterEach(() => {
    TestBed.resetTestingModule();
    vi.unstubAllGlobals();
  });

  it('offers the four combinations as one choice and tracks what changed', async () => {
    const { fixture, root, dialog, cell } = setup();
    await fixture.whenStable();
    cell(2).click();
    await fixture.whenStable();
    const editor = dialog();
    const options = [...root.querySelectorAll<HTMLInputElement>('.day-kind input[type="radio"]')];
    expect(options.map((option) => option.value)).toEqual([
      'FullDay',
      'MorningOff',
      'AfternoonOff',
      'DayOff',
    ]);
    expect(options.find((option) => option.checked)?.value).toBe('AfternoonOff');
    expect(root.querySelector('.day-kind legend small')).toBeNull();
    options[3].click();
    await fixture.whenStable();
    expect(editor.draft()).toMatchObject({ morningOff: true, afternoonOff: true });
    expect(editor.dirty()).toBe(true);
    expect(root.querySelector('.day-kind legend small')?.textContent).toContain('原為「下午放假」');
    editor.revert();
    await fixture.whenStable();
    expect(editor.dirty()).toBe(false);
    expect(editor.kind()).toBe('AfternoonOff');
  });
});
