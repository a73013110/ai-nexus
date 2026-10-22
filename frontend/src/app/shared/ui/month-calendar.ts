import { NgTemplateOutlet } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  Directive,
  ElementRef,
  TemplateRef,
  afterRenderEffect,
  booleanAttribute,
  computed,
  contentChild,
  inject,
  input,
  linkedSignal,
  model,
  output,
} from '@angular/core';
import { currentDay, localDateKey } from '../browser/calendar-day';
import type { BadgeTone } from './count-badge';
import { Icon } from './icon';

/** 每日的小標記（圓點＋選用的數量），例如儀表板的「當日出缺勤」；`label` 併入該日的報讀文字。 */
export interface CalendarMark {
  label: string;
  tone?: BadgeTone | 'accent';
  count?: number | null;
}

/** 呼叫端提供的某一天；沒有提供的日期照樣顯示，只是沒有內容。 */
export interface CalendarDay {
  /** `YYYY-MM-DD` */
  date: string;
  marks?: readonly CalendarMark[];
  /** 這一天的報讀與提示文字；格子內容只是視覺，螢幕閱讀器讀的是日期加上它。 */
  description?: string;
  /** 不能點選（仍可聚焦、讀出，`aria-disabled`）。 */
  disabled?: boolean;
}

/** 月曆的一格；`inMonth` 為 false 的是前後月補齊整週的日期。 */
export interface CalendarCell {
  date: string;
  day: number;
  /** 0＝星期日 … 6＝星期六 */
  weekday: number;
  inMonth: boolean;
  weekend: boolean;
}

export type WeekStart = 0 | 1;

const DAY = 86_400_000;
const WEEKDAY_NAMES = '日一二三四五六';
const MONTH_KEY = /^\d{4}-(0[1-9]|1[0-2])$/;

// 日期一律當成 UTC 的日曆日計算：沒有時區，也不受夏令時間影響。
const timeOf = (date: string) => {
  const [year, month, day] = date.split('-').map(Number);
  return Date.UTC(year, month - 1, day);
};
const keyOf = (time: number) => new Date(time).toISOString().slice(0, 10);

/** `date` 加減天數。 */
export const addDays = (date: string, days: number) => keyOf(timeOf(date) + days * DAY);
/** `date` 加減月數；目標月份沒有這一天時停在月底（1/31 → 2/28）。 */
export function addMonths(date: string, months: number) {
  const [year, month, day] = date.split('-').map(Number);
  const last = new Date(Date.UTC(year, month - 1 + months + 1, 0)).getUTCDate();
  return keyOf(Date.UTC(year, month - 1 + months, Math.min(day, last)));
}
export const weekdayOf = (date: string) => new Date(timeOf(date)).getUTCDay();
/** 「2026年10月5日 星期一」：格子的可存取名稱，不靠表頭也聽得出完整日期。 */
export const fullDateLabel = (date: string) =>
  `${Number(date.slice(0, 4))}年${Number(date.slice(5, 7))}月${Number(date.slice(8))}日 星期${WEEKDAY_NAMES[weekdayOf(date)]}`;

/**
 * `YYYY-MM` 的月曆格子，每列一週，從 `weekStart` 開始；前後補上相鄰月份的日期湊滿整週。
 * 預設只排到這個月需要的週數（4～6 列），`fixedWeeks` 固定 6 列讓換月時高度不變。
 */
export function monthGrid(month: string, weekStart: WeekStart = 0, fixedWeeks = false) {
  const first = `${month}-01`;
  const length = new Date(Date.UTC(+month.slice(0, 4), +month.slice(5, 7), 0)).getUTCDate();
  const lead = (weekdayOf(first) - weekStart + 7) % 7;
  const weeks = fixedWeeks ? 6 : Math.ceil((lead + length) / 7);
  const start = addDays(first, -lead);
  const grid: CalendarCell[][] = [];
  for (let week = 0; week < weeks; week++) {
    const row: CalendarCell[] = [];
    for (let index = 0; index < 7; index++) {
      const date = addDays(start, week * 7 + index);
      const weekday = weekdayOf(date);
      row.push({
        date,
        day: Number(date.slice(8)),
        weekday,
        inMonth: date.startsWith(month),
        weekend: weekday === 0 || weekday === 6,
      });
    }
    grid.push(row);
  }
  return grid;
}

/** 方向鍵、Home／End、PageUp／PageDown 從 `date` 移到的日期；不是移動鍵時回傳 null。 */
export function calendarKeyTarget(
  key: string,
  date: string,
  { shift = false, weekStart = 0 as WeekStart } = {},
): string | null {
  const offset = (weekdayOf(date) - weekStart + 7) % 7;
  switch (key) {
    case 'ArrowLeft':
      return addDays(date, -1);
    case 'ArrowRight':
      return addDays(date, 1);
    case 'ArrowUp':
      return addDays(date, -7);
    case 'ArrowDown':
      return addDays(date, 7);
    case 'Home':
      return addDays(date, -offset);
    case 'End':
      return addDays(date, 6 - offset);
    case 'PageUp':
      return addMonths(date, shift ? -12 : -1);
    case 'PageDown':
      return addMonths(date, shift ? 12 : 1);
    default:
      return null;
  }
}

/** 範本收到的一格：日期資訊、呼叫端的資料與目前狀態。 */
export interface CalendarDayContext<T extends CalendarDay = CalendarDay> {
  $implicit: CalendarCell;
  day: T | undefined;
  today: boolean;
  selected: boolean;
}

/**
 * 每日格子的內容範本：`<ng-template [nxCalendarDay]="days" let-cell let-day="day">`。
 * 綁上與 `days` 相同的陣列只為了推導 `day` 的型別。內容是視覺（`aria-hidden`），
 * 要讓報讀器讀到的寫進 `CalendarDay.description`；只放文字與圖形，不放按鈕。
 */
@Directive({ selector: 'ng-template[nxCalendarDay]' })
export class CalendarDayTemplate<T extends CalendarDay = CalendarDay> {
  readonly nxCalendarDay = input<readonly T[] | ''>('');
  readonly template = inject<TemplateRef<CalendarDayContext<T>>>(TemplateRef);
  static ngTemplateContextGuard<T extends CalendarDay>(
    _directive: CalendarDayTemplate<T>,
    _context: unknown,
  ): _context is CalendarDayContext<T> {
    return true;
  }
}

/**
 * 月檢視日曆：一個月的格子、今天與選取標示、週末與前後月淡化，內容由呼叫端提供
 * （`days` 的 `marks` 圓點，或 `nxCalendarDay` 範本）。不含任何業務規則。
 * 鍵盤依 grid 模式：方向鍵移動、Home／End 到週首尾、PageUp／PageDown 換月（Shift 換年）、
 * Enter／Space 選取；移出本月會改 `month`（`monthChange`）並把焦點留在新月份的那一天。
 * 宿主是 `month-calendar` 容器，呼叫端可用 `@container month-calendar (width < 36rem)` 精簡自己的範本。
 */
@Component({
  selector: 'nx-month-calendar',
  imports: [Icon, NgTemplateOutlet],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './month-calendar.scss',
  host: { '[class.is-interactive]': 'interactive()' },
  template: `@if (header()) {
      <div class="calendar-head">
        <span class="calendar-title" aria-live="polite">{{ title() }}</span>
        <ng-content select="[calendar-actions]" />
        <span class="calendar-nav">
          <button type="button" class="quiet-button" (click)="goToday()">本月</button>
          <button
            type="button"
            class="icon-button"
            aria-label="上個月"
            title="上個月"
            (click)="shift(-1)"
          >
            <nx-icon name="left" />
          </button>
          <button
            type="button"
            class="icon-button"
            aria-label="下個月"
            title="下個月"
            (click)="shift(1)"
          >
            <nx-icon name="chevron" />
          </button>
        </span>
      </div>
    }
    <div
      class="calendar-grid"
      role="grid"
      [attr.aria-label]="label() + '，' + title()"
      [attr.aria-readonly]="interactive() ? null : true"
      [attr.aria-busy]="busy() || null"
    >
      <div class="calendar-weekdays" role="row">
        @for (weekday of weekdays(); track weekday.index) {
          <div
            role="columnheader"
            [class.is-weekend]="weekday.weekend"
            [attr.aria-label]="'星期' + weekday.name"
          >
            {{ weekday.name }}
          </div>
        }
      </div>
      @for (shown of [validMonth()]; track shown) {
        <div class="calendar-weeks" [attr.data-turn]="turn() || null">
          @for (week of weeks(); track week[0].date) {
            <div class="calendar-week" role="row">
              @for (cell of week; track cell.date) {
                <div
                  role="gridcell"
                  class="calendar-day"
                  [class.is-outside]="!cell.inMonth"
                  [class.is-weekend]="cell.weekend"
                  [class.is-today]="cell.today"
                  [class.is-selected]="cell.selected"
                  [class.is-disabled]="cell.disabled"
                  [attr.data-date]="cell.date"
                  [attr.tabindex]="cell.date === focusDate() ? 0 : -1"
                  [attr.aria-label]="cell.label"
                  [attr.aria-current]="cell.today ? 'date' : null"
                  [attr.aria-selected]="interactive() && cell.inMonth ? cell.selected : null"
                  [attr.aria-disabled]="cell.disabled || null"
                  [attr.title]="cell.data?.description || null"
                  (click)="pick(cell.date)"
                  (keydown)="key($event, cell.date)"
                  (focus)="focusDate.set(cell.date)"
                >
                  <span class="calendar-date" aria-hidden="true">{{ cell.day }}</span>
                  @if (cell.inMonth) {
                    <div class="calendar-content" aria-hidden="true">
                      @if (dayTemplate(); as custom) {
                        <ng-container
                          *ngTemplateOutlet="
                            custom.template;
                            context: {
                              $implicit: cell,
                              day: cell.data,
                              today: cell.today,
                              selected: cell.selected,
                            }
                          "
                        />
                      } @else if (cell.data?.marks?.length) {
                        <span class="calendar-marks">
                          @for (mark of cell.data.marks; track $index) {
                            <span class="calendar-mark" [attr.data-tone]="mark.tone ?? 'neutral'"
                              ><i></i>
                              @if (mark.count != null) {
                                <b>{{ mark.count }}</b>
                              }
                            </span>
                          }
                        </span>
                      }
                    </div>
                  }
                </div>
              }
            </div>
          }
        </div>
      }
    </div>`,
})
export class MonthCalendar {
  /** 顯示的月份 `YYYY-MM`；鍵盤或標題列換月時會更新並送出 `monthChange`。 */
  readonly month = model.required<string>();
  /** grid 的可存取名稱（例：「假日行事曆」），報讀時接上月份。 */
  readonly label = input.required<string>();
  readonly days = input<readonly CalendarDay[]>([]);
  /** 選取的日期；點選或 Enter／Space 會更新並送出 `selectedChange`。 */
  readonly selected = model<string | null>(null);
  /** 今天的 `YYYY-MM-DD`；不給就用瀏覽器本地日期（業務日以台北為準時由呼叫端傳入）。 */
  readonly today = input<string | null>(null);
  /** 一週的第一天：預設週日（0），與台灣常見的月曆、公文系統的行事曆一致；需要 ISO 週時傳 1。 */
  readonly weekStart = input<WeekStart>(0);
  readonly fixedWeeks = input(false, { transform: booleanAttribute });
  /** 標題列（月份、本月、上／下個月）；頁面自己有月份切換時關掉。 */
  readonly header = input(true, { transform: booleanAttribute });
  /** false 時只能瀏覽：不選取、不送出 `dayActivate`。 */
  readonly interactive = input(true, { transform: booleanAttribute });
  readonly busy = input(false, { transform: booleanAttribute });
  /** 點選或以 Enter／Space 選了本月一個可用的日期。 */
  readonly dayActivate = output<string>();

  readonly dayTemplate = contentChild(CalendarDayTemplate);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly localDay = currentDay();
  private readonly currentToday = computed(
    () => this.today() ?? localDateKey(new Date(this.localDay())),
  );
  /** 不合法的月份退回今天所在的月，避免網址參數讓格子算不出來。 */
  protected readonly validMonth = computed(() =>
    MONTH_KEY.test(this.month()) ? this.month() : this.currentToday().slice(0, 7),
  );
  readonly title = computed(
    () => `${this.validMonth().slice(0, 4)} 年 ${Number(this.validMonth().slice(5))} 月`,
  );
  readonly weekdays = computed(() =>
    Array.from({ length: 7 }, (_, offset) => {
      const index = (this.weekStart() + offset) % 7;
      return { index, name: WEEKDAY_NAMES[index], weekend: index === 0 || index === 6 };
    }),
  );
  private readonly dayMap = computed(
    () => new Map(this.days().map((day) => [day.date, day] as const)),
  );
  readonly weeks = computed(() => {
    const today = this.currentToday();
    const selected = this.selected();
    const days = this.dayMap();
    return monthGrid(this.validMonth(), this.weekStart(), this.fixedWeeks()).map((week) =>
      week.map((cell) => {
        const data = cell.inMonth ? days.get(cell.date) : undefined;
        const parts = [fullDateLabel(cell.date)];
        if (cell.date === today) parts.push('今天');
        if (data?.description) parts.push(data.description);
        else for (const mark of data?.marks ?? []) parts.push(markLabel(mark));
        return {
          ...cell,
          data,
          today: cell.date === today,
          selected: cell.inMonth && cell.date === selected,
          disabled: cell.inMonth && !!data?.disabled,
          label: parts.join('，'),
        };
      }),
    );
  });
  /** 換月的方向（-1、1）；第一次顯示是 0，不播換月動效。 */
  readonly turn = linkedSignal<string, number>({
    source: this.validMonth,
    computation: (month, previous) =>
      previous ? Math.sign(month.localeCompare(previous.source)) : 0,
  });
  /** roving tabindex 的那一天：留在本月時保留，換月時改為選取日、今天或月初。 */
  readonly focusDate = linkedSignal<string, string>({
    source: this.validMonth,
    computation: (month, previous) => {
      if (previous?.value.startsWith(month)) return previous.value;
      const selected = this.selected();
      if (selected?.startsWith(month)) return selected;
      const today = this.currentToday();
      return today.startsWith(month) ? today : `${month}-01`;
    },
  });
  /** 鍵盤移動後要把焦點交給哪一天；換月時格子重建，焦點要在渲染後補上。 */
  private pendingFocus = false;

  constructor() {
    afterRenderEffect(() => {
      const date = this.focusDate();
      this.weeks();
      if (!this.pendingFocus) return;
      this.pendingFocus = false;
      this.host.nativeElement
        .querySelector<HTMLElement>(`.calendar-day[data-date="${date}"]`)
        ?.focus();
    });
  }

  shift(delta: number) {
    this.month.set(addMonths(`${this.validMonth()}-01`, delta).slice(0, 7));
  }
  goToday() {
    const today = this.currentToday();
    this.month.set(today.slice(0, 7));
    this.focusDate.set(today);
  }
  pick(date: string) {
    if (!date.startsWith(this.validMonth())) {
      this.moveTo(date);
      return;
    }
    this.focusDate.set(date);
    if (!this.interactive() || this.dayMap().get(date)?.disabled) return;
    this.selected.set(date);
    this.dayActivate.emit(date);
  }
  key(event: KeyboardEvent, date: string) {
    if (event.altKey || event.ctrlKey || event.metaKey) return;
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      this.pick(date);
      return;
    }
    const target = calendarKeyTarget(event.key, date, {
      shift: event.shiftKey,
      weekStart: this.weekStart(),
    });
    if (!target) return;
    event.preventDefault();
    this.moveTo(target);
  }
  private moveTo(date: string) {
    this.pendingFocus = true;
    if (!date.startsWith(this.validMonth())) this.month.set(date.slice(0, 7));
    this.focusDate.set(date);
  }
}

const markLabel = (mark: CalendarMark) =>
  mark.count == null ? mark.label : `${mark.label} ${mark.count}`;
