import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  input,
  output,
  signal,
  viewChild,
  viewChildren,
} from '@angular/core';
import { anchoredPopover } from '../browser/anchored-popover';
import { positionPopover } from '../browser/popover-position';
import { APP_TIME_ZONE, formatDateTimeInput, parseDateTimeInput } from '../browser/format';
import { formatCalendarValue, parseLocalDateTime } from '../browser/date-time';
import { DateTimePicker } from './date-time-picker';
import { Icon } from './icon';

/**
 * A preset (`preset` is its value; `from`/`to` unused) or a custom range (`preset: 'custom'`)
 * of workspace wall-clock values: `YYYY-MM-DDTHH:mm:ss`, or `YYYY-MM-DD` without time.
 */
export interface TimeRange {
  preset: string;
  from: string;
  to: string;
}
/** `duration` reaches back from the query time (ms); without it the preset has no bounds. */
export interface TimeRangePreset {
  value: string;
  label: string;
  duration?: number;
}
export const CUSTOM_RANGE = 'custom';
const hour = 60 * 60_000;
export const hoursAgo = (hours: number) => hours * hour;

/**
 * The UTC bounds to query, or why the range cannot be queried. A preset ends a minute after now
 * (events written while the page loads); a date-only custom range ends at the next midnight.
 */
export function timeRangeBounds(
  range: TimeRange,
  presets: readonly TimeRangePreset[],
  now = Date.now(),
): { from?: string; to?: string } | string {
  if (range.preset !== CUSTOM_RANGE) {
    const preset = presets.find((item) => item.value === range.preset);
    if (!preset) return '請選擇時間範圍。';
    if (preset.duration === undefined) return {};
    const to = now + 60_000;
    return { from: new Date(to - preset.duration).toISOString(), to: new Date(to).toISOString() };
  }
  const from = range.from ? wallClock(range.from, false) : undefined,
    to = range.to ? wallClock(range.to, true) : undefined;
  if (from === null || to === null) return '請輸入有效的起訖時間。';
  if (from && to && from >= to) return '結束時間須晚於起始時間。';
  return { from: from?.toISOString(), to: to?.toISOString() };
}

/** A Taipei wall-clock value as an instant; a date alone is its midnight, or the next one for an end. */
function wallClock(value: string, end: boolean): Date | null {
  if (!parseLocalDateTime(value)) return null;
  if (value.includes('T')) return parseDateTimeInput(value);
  const date = new Date(value + 'T00:00:00+08:00');
  if (end) date.setUTCDate(date.getUTCDate() + 1);
  return date;
}

/** The range as one line: the preset's name, or its custom ends. */
export function timeRangeLabel(range: TimeRange, presets: readonly TimeRangePreset[]): string {
  if (range.preset !== CUSTOM_RANGE)
    return presets.find((item) => item.value === range.preset)?.label ?? '';
  const show = (value: string) => formatCalendarValue(value, 'gregory', value.includes('T'));
  if (range.from && range.to) return `${show(range.from)} — ${show(range.to)}`;
  if (range.from) return `${show(range.from)} 起`;
  if (range.to) return `至 ${show(range.to)}`;
  return '不限時間';
}

let sequence = 0;

/**
 * Preset ranges and a custom range in one popover; the time zone is said once, in it. Choosing a
 * preset or applying the custom ends changes the value (a query's draft), not the query itself.
 * `required` asks for both custom ends; otherwise either may stay open.
 */
@Component({
  selector: 'nx-time-range-picker',
  imports: [DateTimePicker, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './time-range-picker.scss',
  template: `<span class="ui-field" [class.sr-only]="hideLabel()" [id]="id + '-label'">{{
      label()
    }}</span>
    <button
      #trigger
      type="button"
      class="ui-time-range-trigger"
      aria-haspopup="dialog"
      [attr.aria-labelledby]="id + '-label ' + id + '-value'"
      [attr.aria-expanded]="opened()"
      [attr.aria-controls]="id + '-panel'"
      [disabled]="disabled()"
      (click)="toggle()"
    >
      <nx-icon name="clock" /><span [id]="id + '-value'" [title]="summary()">{{ summary() }}</span
      ><nx-icon name="chevron" />
    </button>
    <div
      #panel
      popover="auto"
      role="dialog"
      class="popover-panel ui-time-range-panel ui-density-compact"
      [id]="id + '-panel'"
      [attr.aria-label]="label()"
      (toggle)="popover.sync($event)"
      (keydown.escape)="close($event)"
    >
      <div class="ui-time-range-presets" role="group" aria-label="預設區間">
        @for (preset of presets(); track preset.value) {
          <button
            type="button"
            [attr.aria-pressed]="value().preset === preset.value"
            (click)="choose(preset.value)"
          >
            <span>{{ preset.label }}</span>
            @if (value().preset === preset.value) {
              <nx-icon name="check" />
            }
          </button>
        }
      </div>
      <fieldset class="ui-time-range-custom">
        <legend>自訂區間</legend>
        <nx-date-time-picker
          [label]="withTime() ? '起始時間' : '起始日期'"
          [withTime]="withTime()"
          [required]="required()"
          [value]="from()"
          (valueChange)="from.set($event)"
        />
        <nx-date-time-picker
          [label]="withTime() ? '結束時間' : '結束日期'"
          [withTime]="withTime()"
          [required]="required()"
          [min]="from()"
          [value]="to()"
          (valueChange)="to.set($event)"
        />
        @if (customError()) {
          <p class="ui-date-error" role="alert">{{ customError() }}</p>
        }
      </fieldset>
      <div class="ui-time-range-foot">
        <span>時間以 {{ timezone }} 顯示</span>
        <button
          type="button"
          class="primary-button"
          [disabled]="!canApply()"
          (click)="applyCustom()"
        >
          套用自訂
        </button>
      </div>
    </div>`,
})
export class TimeRangePicker {
  readonly label = input('時間範圍');
  readonly hideLabel = input(false);
  readonly presets = input.required<readonly TimeRangePreset[]>();
  readonly value = input.required<TimeRange>();
  readonly withTime = input(true);
  readonly required = input(true);
  readonly disabled = input(false);
  readonly valueChange = output<TimeRange>();
  readonly id = 'nx-time-range-' + ++sequence;
  readonly timezone = APP_TIME_ZONE;
  /** The custom ends being edited; they reach the value only when applied. */
  readonly from = signal('');
  readonly to = signal('');
  readonly summary = computed(() => timeRangeLabel(this.value(), this.presets()));
  private readonly pickers = viewChildren(DateTimePicker);
  readonly customError = computed(() => {
    const from = this.from(),
      to = this.to();
    if (this.required() && (!from || !to)) return from || to ? '請填寫起訖兩端。' : '';
    const bounds = timeRangeBounds({ preset: CUSTOM_RANGE, from, to }, this.presets());
    return typeof bounds === 'string' ? bounds : '';
  });
  /** A typed end that is not a date (its picker says so) blocks the range instead of leaving it open. */
  readonly canApply = computed(
    () =>
      !this.customError() &&
      (!!this.from() || !!this.to()) &&
      this.pickers().every((picker) => picker.valid()),
  );
  private readonly trigger = viewChild.required<ElementRef<HTMLButtonElement>>('trigger');
  private readonly panel = viewChild.required<ElementRef<HTMLElement>>('panel');
  protected readonly popover = anchoredPopover({
    panel: () => this.panel().nativeElement,
    place: (panel) => positionPopover(this.trigger().nativeElement, panel, 340, 'left', 560),
  });
  readonly opened = this.popover.opened;

  toggle() {
    if (this.opened()) this.close();
    else this.open();
  }
  private open() {
    const range = this.value();
    if (range.preset === CUSTOM_RANGE) {
      this.from.set(range.from);
      this.to.set(range.to);
    } else {
      // Editing starts from the preset's window, so a small change is a small edit.
      const bounds = timeRangeBounds(range, this.presets());
      const local = (iso?: string) =>
        iso ? formatDateTimeInput(new Date(iso)).slice(0, this.withTime() ? 19 : 10) : '';
      this.from.set(typeof bounds === 'string' ? '' : local(bounds.from));
      this.to.set(typeof bounds === 'string' ? '' : local(bounds.to));
    }
    this.popover.show(this.trigger().nativeElement);
    requestAnimationFrame(() =>
      this.panel()
        .nativeElement.querySelector<HTMLButtonElement>(
          '[aria-pressed="true"], .ui-time-range-presets button',
        )
        ?.focus(),
    );
  }
  close(event?: Event) {
    event?.preventDefault();
    event?.stopPropagation();
    this.popover.hide();
    this.trigger().nativeElement.focus({ preventScroll: true });
  }
  choose(preset: string) {
    this.valueChange.emit({ preset, from: '', to: '' });
    this.close();
  }
  applyCustom() {
    if (!this.canApply()) return;
    this.valueChange.emit({ preset: CUSTOM_RANGE, from: this.from(), to: this.to() });
    this.close();
  }
}
