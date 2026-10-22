import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import type { AttendanceDayType } from '@core/api/schema';
import { ATTENDANCE_DAY_TYPE } from '../attendance-presentation';

/** 公文系統一天的四種組合；`Unknown` 不會出現在 GD 的資料列上。 */
export type HolidayKind = Exclude<AttendanceDayType, 'Unknown'>;

export const holidayKind = (morningOff: boolean, afternoonOff: boolean): HolidayKind =>
  morningOff && afternoonOff
    ? 'DayOff'
    : morningOff
      ? 'MorningOff'
      : afternoonOff
        ? 'AfternoonOff'
        : 'FullDay';

const option = (kind: HolidayKind, hint: string, morningOff: boolean, afternoonOff: boolean) => ({
  kind,
  label: ATTENDANCE_DAY_TYPE[kind],
  hint,
  morningOff,
  afternoonOff,
});
/** 編輯對話框的四個選項；標題沿用出勤共用的日型字樣，說明補上另一半天。 */
export const HOLIDAY_KINDS = [
  option('FullDay', '整天上班', false, false),
  option('MorningOff', '下午照常上班', true, false),
  option('AfternoonOff', '上午照常上班', false, true),
  option('DayOff', '整天放假', true, true),
] as const;

/**
 * 一天的縮圖：上半格是上午、下半格是下午，放假的半天填色。月曆格子的底色、圖例與編輯對話框的選項
 * 用同一種圖形，半天放假在哪裡看到都是同一個樣子。純裝飾，文字由旁邊的標籤提供。
 */
@Component({
  selector: 'nx-holiday-glyph',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { 'aria-hidden': 'true', '[attr.data-kind]': 'kind()' },
  template: `<i></i><i></i>`,
  styles: `
    :host {
      --glyph-fill: color-mix(in srgb, var(--accent) 38%, transparent);
      display: inline-grid;
      grid-template-rows: 1fr 1fr;
      flex: none;
      width: var(--holiday-glyph-size, 0.875rem);
      aspect-ratio: 5 / 6;
      overflow: hidden;
      border: 1px solid color-mix(in srgb, var(--accent) 45%, var(--line));
      border-radius: var(--radius-mark);
      background: var(--surface);
    }
    i + i {
      border-top: 1px dashed color-mix(in srgb, var(--accent) 35%, var(--line));
    }
    :host([data-kind='DayOff']) i,
    :host([data-kind='MorningOff']) i:first-child,
    :host([data-kind='AfternoonOff']) i:last-child {
      background: var(--glyph-fill);
    }
    :host([data-kind='DayOff']) i + i {
      border-top-style: none;
    }
  `,
})
export class HolidayGlyph {
  readonly kind = input.required<HolidayKind>();
}
