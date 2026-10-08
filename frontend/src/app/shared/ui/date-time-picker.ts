import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { Icon } from './icon';
import { Field } from './field';
import { Select } from './select';
import { positionPopover } from '../browser/popover-position';
import { formatDateTimeInput } from '../browser/format';
import {
  calendarDate,
  formatCalendarValue,
  parseCalendarText,
  parseLocalDateTime,
  serializeLocalDateTime,
  shiftCalendarMonth,
  type CalendarSystem,
} from '../browser/date-time';

let sequence = 0;
/** ISO Gregorian wall-clock values at the boundary; calendar/locale affect display only. */
@Component({
  selector: 'nx-date-time-picker',
  imports: [Icon, Field, Select],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './date-time-picker.html',
  styleUrl: './date-time-picker.scss',
})
export class DateTimePicker {
  readonly label = input.required<string>();
  readonly value = input('');
  readonly calendarSystem = input<CalendarSystem>('gregory');
  readonly withTime = input(true);
  readonly disabled = input(false);
  readonly required = input(true);
  readonly min = input('');
  readonly max = input('');
  readonly valueChange = output<string>();
  readonly id = 'nx-date-' + ++sequence;
  readonly opened = signal(false);
  readonly text = signal<string | null>(null);
  readonly touched = signal(false);
  readonly draft = signal(formatDateTimeInput(new Date()));
  readonly view = signal(parseLocalDateTime(this.draft())!);
  readonly focused = signal(this.draft().slice(0, 10));
  readonly display = computed(
    () => this.text() ?? formatCalendarValue(this.value(), this.calendarSystem(), this.withTime()),
  );
  readonly formatHint = computed(
    () =>
      (this.calendarSystem() === 'roc' ? '民國 yyy/MM/dd' : 'yyyy/MM/dd') +
      (this.withTime() ? ' HH:mm:ss' : ''),
  );
  private readonly parsed = computed(() =>
    parseCalendarText(this.display(), this.calendarSystem(), this.withTime()),
  );
  readonly valid = computed(() => {
    if (!this.display().trim()) return !this.required();
    const value = this.parsed();
    return !!value && this.inBounds(value);
  });
  readonly error = computed(() => {
    if (!this.touched() || this.valid()) return '';
    return !this.parsed() ? '請輸入有效日期，格式：' + this.formatHint() : '日期超出可選範圍。';
  });
  readonly parts = computed(() => parseLocalDateTime(this.draft())!);
  readonly offset = computed(() => (this.calendarSystem() === 'roc' ? 1911 : 0));
  readonly year = computed(() => this.view().year - this.offset());
  readonly monthLabel = computed(
    () =>
      (this.calendarSystem() === 'roc' ? '民國 ' : '') +
      this.year() +
      ' 年 ' +
      this.view().month +
      ' 月',
  );
  readonly months = Array.from({ length: 12 }, (_, i) => i + 1);
  readonly monthOptions = this.months.map((month) => ({
    value: String(month),
    label: `${month} 月`,
  }));
  readonly weekdays = ['日', '一', '二', '三', '四', '五', '六'];
  readonly today = () => formatDateTimeInput(new Date()).slice(0, 10);
  readonly weeks = computed(() => {
    const first = calendarDate({ ...this.view(), day: 1, hour: 0, minute: 0, second: 0 });
    first.setUTCDate(1 - first.getUTCDay());
    const days = Array.from({ length: 42 }, (_, index) => {
      const date = new Date(first);
      date.setUTCDate(date.getUTCDate() + index);
      const value = date.toISOString().slice(0, 10);
      return {
        value,
        day: date.getUTCDate(),
        outside: date.getUTCMonth() + 1 !== this.view().month,
        disabled: !this.dayAvailable(value),
      };
    });
    return Array.from({ length: 6 }, (_, i) => days.slice(i * 7, i * 7 + 7));
  });
  readonly invalidTimeFields = signal<string[]>([]);
  readonly draftValid = computed(
    () => !this.invalidTimeFields().length && this.inBounds(this.draft()),
  );
  private readonly control = viewChild.required<ElementRef<HTMLElement>>('control');
  private readonly editor = viewChild.required<ElementRef<HTMLInputElement>>('editor');
  private readonly panel = viewChild.required<ElementRef<HTMLElement>>('panel');
  private returnFocus: HTMLElement | null = null;
  private lastEmitted = '';
  constructor() {
    effect(() => {
      const value = this.value();
      if (value !== this.lastEmitted)
        untracked(() => {
          this.text.set(null);
          this.touched.set(false);
        });
      this.lastEmitted = value;
    });
    const reposition = () => {
      if (this.opened()) this.position();
    };
    window.addEventListener('resize', reposition);
    window.addEventListener('scroll', reposition, true);
    inject(DestroyRef).onDestroy(() => {
      window.removeEventListener('resize', reposition);
      window.removeEventListener('scroll', reposition, true);
    });
  }
  edit(text: string) {
    this.text.set(text);
    const value = this.parsed();
    this.lastEmitted = value && this.inBounds(value) ? value : '';
    this.valueChange.emit(this.lastEmitted);
  }
  blur() {
    this.touched.set(true);
    if (!this.error()) this.text.set(null);
  }
  inputKey(event: KeyboardEvent) {
    if (event.key === 'ArrowDown' && event.altKey) {
      event.preventDefault();
      this.open();
    }
  }
  toggle() {
    this.opened() ? this.close() : this.open();
  }
  open() {
    if (this.disabled()) return;
    this.returnFocus =
      document.activeElement instanceof HTMLElement ? document.activeElement : null;
    let value = this.value() || formatDateTimeInput(new Date());
    if (this.min() && value < this.min()) value = this.min();
    if (this.max() && value > this.max()) value = this.max();
    if (this.calendarSystem() === 'roc' && Number(value.slice(0, 4)) < 1912)
      value = '1912-01-01T00:00:00';
    this.invalidTimeFields.set([]);
    this.draft.set(
      serializeLocalDateTime(
        parseLocalDateTime(value) ?? parseLocalDateTime(formatDateTimeInput(new Date()))!,
        this.withTime(),
      ),
    );
    this.view.set(this.parts());
    this.focused.set(value.slice(0, 10));
    this.panel().nativeElement.showPopover();
    this.opened.set(true);
    this.position();
    this.focusDay();
  }
  private position() {
    positionPopover(this.control().nativeElement, this.panel().nativeElement, 320, 'left', 520);
  }
  close() {
    this.panel().nativeElement.hidePopover();
    this.opened.set(false);
    if (this.returnFocus?.isConnected) this.returnFocus.focus({ preventScroll: true });
  }
  toggled(event: Event) {
    this.opened.set((event as ToggleEvent).newState === 'open');
  }
  panelKey(event: KeyboardEvent) {
    if (event.key === 'Enter' && event.target instanceof HTMLInputElement) {
      event.preventDefault();
      if (event.target.id === this.id + '-year') {
        this.changeYear(event.target.value);
        this.focusDay();
      } else this.apply();
      return;
    }
    if (event.key === 'Escape') {
      event.preventDefault();
      event.stopPropagation();
      this.close();
    }
  }
  dayLabel(value: string) {
    return formatCalendarValue(value, this.calendarSystem(), false);
  }
  private inBounds(value: string) {
    const parts = parseLocalDateTime(value);
    return (
      !!parts &&
      (this.calendarSystem() !== 'roc' || parts.year >= 1912) &&
      (!this.min() || value >= this.min()) &&
      (!this.max() || value <= this.max())
    );
  }
  dayAvailable(value: string) {
    return (
      !!parseLocalDateTime(value) &&
      (this.calendarSystem() !== 'roc' || Number(value.slice(0, 4)) >= 1912) &&
      (!this.min() || value >= this.min().slice(0, 10)) &&
      (!this.max() || value <= this.max().slice(0, 10))
    );
  }
  chooseDay(value: string) {
    if (!this.dayAvailable(value)) return;
    let selected = value + (this.withTime() ? this.draft().slice(10) : '');
    if (this.min() && selected < this.min()) selected = this.min();
    if (this.max() && selected > this.max()) selected = this.max();
    this.draft.set(selected);
    this.view.set(this.parts());
    this.focused.set(value);
    if (!this.withTime()) this.apply();
  }
  shiftMonth(amount: number) {
    const next = shiftCalendarMonth(this.view(), amount);
    if (next.year < Math.max(1, this.offset() + 1) || next.year > 9999) return;
    this.view.set(next);
    this.focused.set(serializeLocalDateTime(next, false));
  }
  changeMonth(value: string) {
    this.shiftMonth(Number(value) - this.view().month);
  }
  changeYear(value: string) {
    const year = Number(value) + this.offset();
    if (!Number.isInteger(year) || year <= this.offset() || year > 9999) return;
    this.shiftMonth((year - this.view().year) * 12);
  }
  setTime(field: 'hour' | 'minute' | 'second', value: string) {
    const number = Number(value);
    const valid =
      !!value && Number.isInteger(number) && number >= 0 && number <= (field === 'hour' ? 23 : 59);
    this.invalidTimeFields.update((fields) => [
      ...fields.filter((key) => key !== field),
      ...(valid ? [] : [field]),
    ]);
    if (!valid) return;
    this.draft.set(serializeLocalDateTime({ ...this.parts(), [field]: number }));
  }
  useToday() {
    this.chooseDay(this.today());
    this.focusDay();
  }
  apply() {
    if (!this.draftValid()) return;
    this.text.set(null);
    this.touched.set(false);
    this.lastEmitted = this.draft();
    this.valueChange.emit(this.draft());
    this.close();
    this.editor().nativeElement.focus({ preventScroll: true });
  }
  calendarKey(event: KeyboardEvent) {
    if (event.isComposing) return;
    const current = parseLocalDateTime(this.focused())!,
      date = calendarDate(current),
      key = event.key;
    if (
      [
        'PageUp',
        'PageDown',
        'ArrowLeft',
        'ArrowRight',
        'ArrowUp',
        'ArrowDown',
        'Home',
        'End',
      ].includes(key)
    )
      event.preventDefault();
    if (['PageUp', 'PageDown'].includes(key)) {
      const next = shiftCalendarMonth(
        current,
        (key === 'PageUp' ? -1 : 1) * (event.shiftKey ? 12 : 1),
      );
      if (!this.dayAvailable(serializeLocalDateTime(next, false))) return;
      this.view.set(next);
      this.focused.set(serializeLocalDateTime(next, false));
    } else {
      const movement: Record<string, number> = {
        ArrowLeft: -1,
        ArrowRight: 1,
        ArrowUp: -7,
        ArrowDown: 7,
        Home: -date.getUTCDay(),
        End: 6 - date.getUTCDay(),
      };
      if (!(key in movement)) return;
      date.setUTCDate(date.getUTCDate() + movement[key]);
      const next = date.toISOString().slice(0, 10);
      if (!this.dayAvailable(next)) return;
      this.focused.set(next);
      this.view.set(parseLocalDateTime(next)!);
    }
    event.preventDefault();
    this.focusDay();
  }
  private focusDay() {
    requestAnimationFrame(() =>
      this.panel()
        .nativeElement.querySelector<HTMLButtonElement>('[data-date="' + this.focused() + '"]')
        ?.focus({ preventScroll: true }),
    );
  }
}
