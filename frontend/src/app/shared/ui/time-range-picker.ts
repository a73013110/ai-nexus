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
 * 預設區間（`preset` 為其值，`from`／`to` 不用）或自訂區間（`preset: 'custom'`）。
 * 自訂的起訖是工作區時區（台北）的牆上時間：`YYYY-MM-DDTHH:mm:ss`，或不含時間的 `YYYY-MM-DD`。
 */
export interface TimeRange {
  preset: string;
  from: string;
  to: string;
}
/** `duration` 是從查詢當下往回推的毫秒數；沒有 `duration` 表示不限時間。 */
export interface TimeRangePreset {
  value: string;
  label: string;
  duration?: number;
}
export const CUSTOM_RANGE = 'custom';
const hour = 60 * 60_000;
export const hoursAgo = (hours: number) => hours * hour;

/**
 * 要查詢的 UTC 起訖，或無法查詢的原因。
 * - 預設區間的結束時間是「現在＋1 分鐘」，涵蓋頁面載入期間剛寫入的事件。
 * - 只有日期的自訂結束日取隔天 00:00（不含），整天都會被包含。
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

/** 台北牆上時間轉成時間點；只有日期時取當天 00:00，結束端取隔天 00:00。 */
function wallClock(value: string, end: boolean): Date | null {
  if (!parseLocalDateTime(value)) return null;
  if (value.includes('T')) return parseDateTimeInput(value);
  const date = new Date(value + 'T00:00:00+08:00');
  if (end) date.setUTCDate(date.getUTCDate() + 1);
  return date;
}

/** 區間的一行摘要：預設區間的名稱，或自訂的起訖。 */
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
 * 預設區間與自訂區間合在一個 popover，時區只在這裡說明一次。
 * 選預設或按「套用自訂」只改 value（查詢草稿），不會直接查詢。
 * `required` 時自訂起訖都必填；否則可以只填一端（開放區間）。
 */
@Component({
  selector: 'nx-time-range-picker',
  imports: [DateTimePicker, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './time-range-picker.scss',
  template: `<span class="ui-field" [id]="id + '-label'">{{ label() }}</span>
    <button
      #trigger
      type="button"
      class="ui-time-range-trigger"
      aria-haspopup="dialog"
      [attr.aria-labelledby]="id + '-label ' + id + '-value'"
      [attr.aria-expanded]="opened()"
      [attr.aria-controls]="id + '-panel'"
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
          [attr.aria-disabled]="!canApply() || null"
          (click)="applyCustom()"
        >
          套用自訂
        </button>
      </div>
    </div>`,
})
export class TimeRangePicker {
  readonly label = input('時間範圍');
  readonly presets = input.required<readonly TimeRangePreset[]>();
  readonly value = input.required<TimeRange>();
  readonly withTime = input(true);
  readonly required = input(true);
  readonly valueChange = output<TimeRange>();
  readonly id = 'nx-time-range-' + ++sequence;
  readonly timezone = APP_TIME_ZONE;
  /** 正在編輯的自訂起訖；按「套用自訂」才寫回 value。 */
  readonly from = signal('');
  readonly to = signal('');
  readonly summary = computed(() => timeRangeLabel(this.value(), this.presets()));
  private readonly pickers = viewChildren(DateTimePicker);
  /** 必填時只填了一端。輸入到一半不算錯，按「套用自訂」後才提示。 */
  private readonly incomplete = computed(() => this.required() && !this.from() !== !this.to());
  private readonly attempted = signal(false);
  readonly customError = computed(() => {
    if (this.incomplete()) return this.attempted() ? '請填寫起訖兩端。' : '';
    const bounds = timeRangeBounds(
      { preset: CUSTOM_RANGE, from: this.from(), to: this.to() },
      this.presets(),
    );
    return typeof bounds === 'string' ? bounds : '';
  });
  /** 輸入了但不是有效日期的一端要擋下，不能當成留空而變成開放區間。 */
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
    this.attempted.set(false);
    const range = this.value();
    if (range.preset === CUSTOM_RANGE) {
      this.from.set(range.from);
      this.to.set(range.to);
    } else {
      // 從目前預設區間的實際起訖開始編輯，微調時不必從空白重填。
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
  /** 按鈕用 `aria-disabled`：真的停用的話，按下後才出現錯誤會讓焦點掉回 body。 */
  applyCustom() {
    if (this.incomplete()) this.attempted.set(true);
    if (!this.canApply() || this.incomplete()) return;
    this.valueChange.emit({ preset: CUSTOM_RANGE, from: this.from(), to: this.to() });
    this.close();
  }
}
