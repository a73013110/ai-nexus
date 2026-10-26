import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import type { GdFormDetailDto } from '@core/api/schema';
import { Icon } from '@shared/ui/icon';
import { GdFormPeriods } from './gd-form-periods';
import { dateStamp, hoursSummary, taipeiParts } from './gd-form-presentation';

/**
 * 表單的期間摘要：開始 → 結束，右側以細線隔出時數。日期一律帶年（單據可能跨年度），一般單據接著寫到分鐘，
 * 欄寬不夠時時間換到下一行；跨期間假單只寫起訖日期，時數說明改寫段數，框的下半接 {@link GdFormPeriods} 的規則與月曆。
 */
@Component({
  selector: 'nx-gd-form-span',
  imports: [GdFormPeriods, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    role: 'group',
    '[attr.aria-label]': "form().kind === 'Travel' ? '出差期間' : '請假期間'",
  },
  styleUrl: './gd-form-span.scss',
  template: `<div class="span-summary">
      <div class="span-point">
        <span class="span-label">{{ span().range ? '自' : '開始' }}</span>
        <time [attr.datetime]="form().startsAt"
          ><span>{{ span().starts.date }}</span>
          @if (span().starts.time) {
            <span>{{ span().starts.time }}</span>
          }
        </time>
      </div>
      <nx-icon class="span-arrow" name="forward" aria-hidden="true" />
      <span class="sr-only">至</span>
      <div class="span-point">
        <span class="span-label">{{ span().range ? '至' : '結束' }}</span>
        <time [attr.datetime]="form().endsAt"
          ><span>{{ span().ends.date }}</span>
          @if (span().ends.time) {
            <span>{{ span().ends.time }}</span>
          }
        </time>
      </div>
      @if (span().hours; as hours) {
        <div class="span-point span-hours">
          <span class="span-label">時數</span>
          <strong>{{ hours.value }}</strong>
          @if (hours.detail) {
            <small>{{ hours.detail }}</small>
          }
        </div>
      }
    </div>
    @if (span().range) {
      <nx-gd-form-periods class="span-periods" [segments]="form().segments" [focus]="focus()" />
    }`,
})
export class GdFormSpan {
  readonly form = input.required<GdFormDetailDto>();
  /** 從出勤紀錄開啟的那一天，跨期間的月曆會標出來。 */
  readonly focus = input<string | null>(null);
  readonly span = computed(() => {
    const form = this.form();
    const range = form.segments.length > 0;
    return {
      range,
      starts: point(form.startsAt, range),
      ends: point(form.endsAt, range),
      hours: hoursSummary(form.hours, form.segments.length),
    };
  });
}

const point = (value: string, range: boolean) => ({
  date: dateStamp(value),
  time: range ? '' : taipeiParts(value).time,
});
