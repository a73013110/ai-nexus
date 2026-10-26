import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import type { GdFormPeriod } from '@core/api/schema';
import { fullDateLabel, monthGrid } from '@shared/ui/month-calendar';
import { focusSegment, periodDays, periodMonths, periodRules } from './gd-form-presentation';

/**
 * 跨期間假單的各段：上方是規則（同一星期、同一時段合成一條），下方每月一張小月曆，有請假的日子以上緣、
 * 下緣的色條標出上午、下午；從出勤紀錄開啟的那天另加外框並寫出是第幾段。並排四個月，更多時橫向捲動，
 * 不把對話框往下撐。月曆只是圖示：每一格的報讀文字寫出日期與時段，不需要鍵盤逐格移動，所以不用 `MonthCalendar`。
 */
@Component({
  selector: 'nx-gd-form-periods',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './gd-form-periods.scss',
  template: `<div class="period-head">
      <ul class="period-rules" aria-label="請假規則">
        @for (rule of rules(); track rule.key) {
          <li [title]="'共 ' + rule.count + ' 次'">
            <i
              class="period-swatch"
              [attr.data-morning]="rule.morning ? '' : null"
              [attr.data-afternoon]="rule.afternoon ? '' : null"
              aria-hidden="true"
            ></i
            >{{ rule.label }}
          </li>
        }
      </ul>
      <span class="period-legend">上緣＝上午、下緣＝下午</span>
    </div>
    <div class="period-months" role="list" aria-label="請假日期">
      @for (month of months(); track month.key) {
        <section class="period-month" role="listitem" [attr.aria-label]="month.label">
          <h4 aria-hidden="true">{{ month.label }}</h4>
          <div class="period-grid" aria-hidden="true">
            @for (name of weekdays; track name) {
              <span class="period-weekday">{{ name }}</span>
            }
            @for (cell of month.cells; track cell.date) {
              <span
                class="period-cell"
                [class.is-outside]="!cell.inMonth"
                [class.is-weekend]="cell.weekend"
                [class.is-focus]="cell.inMonth && cell.date === focus()"
                [attr.data-morning]="cell.inMonth && cell.period?.morning ? '' : null"
                [attr.data-afternoon]="cell.inMonth && cell.period?.afternoon ? '' : null"
                [title]="cell.inMonth && cell.period ? cell.label : null"
                >{{ cell.day }}</span
              >
            }
          </div>
          <ul class="sr-only">
            @for (cell of month.cells; track cell.date) {
              @if (cell.inMonth && cell.period) {
                <li>{{ cell.label }}</li>
              }
            }
          </ul>
        </section>
      }
    </div>
    @if (focused(); as focused) {
      <p class="period-focus">
        <span
          class="period-cell is-focus"
          [attr.data-morning]="focused.morning ? '' : null"
          [attr.data-afternoon]="focused.afternoon ? '' : null"
          aria-hidden="true"
          >{{ focused.day }}</span
        ><span
          >從出勤紀錄點開的這天：<strong>{{ focused.label }}</strong
          >，{{ focused.position }}</span
        >
      </p>
    }`,
})
export class GdFormPeriods {
  readonly segments = input.required<readonly GdFormPeriod[]>();
  /** 從出勤紀錄開啟的那一天（`YYYY-MM-DD`），在月曆上加框。 */
  readonly focus = input<string | null>(null);
  /** 與共用月曆（`MonthCalendar` 預設）相同，週日起。 */
  readonly weekdays = '日一二三四五六'.split('');
  readonly rules = computed(() => periodRules(this.segments()));
  /** 說明列前面畫一格與月曆上那天相同的格子（日期、上下緣、外框），一看就知道指的是哪一格。 */
  readonly focused = computed(() => {
    const date = this.focus();
    const segment = focusSegment(this.segments(), date);
    const day = date ? periodDays(this.segments()).get(date) : null;
    return segment && date && day ? { ...segment, ...day, day: Number(date.slice(8)) } : null;
  });
  readonly months = computed(() => {
    const days = periodDays(this.segments());
    const keys = periodMonths(this.segments());
    // 每個月都寫年：橫向捲動後看不到上方的起訖，跨年度的單也分得清。
    return keys.map((key) => ({
      key,
      label: `${key.slice(0, 4)} 年 ${Number(key.slice(5))} 月`,
      cells: monthGrid(key)
        .flat()
        .map((cell) => {
          const period = days.get(cell.date) ?? null;
          return {
            ...cell,
            period,
            label: period ? `${fullDateLabel(cell.date)} ${period.times.join('、')}` : '',
          };
        }),
    }));
  });
}
