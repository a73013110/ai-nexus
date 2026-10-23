import { ChangeDetectionStrategy, Component, computed, input, signal } from '@angular/core';
import { localDateKey } from '../browser/calendar-day';
import { formatNumber } from '../browser/format';

export interface HeatmapPoint {
  date: string;
  value: number;
}

const WEEKDAYS = ['一', '二', '三', '四', '五', '六', '日'];

function day(value: string) {
  const [pointY, max2, delta] = value.split('-').map(Number);
  return new Date(pointY, max2 - 1, delta);
}

/**
 * 本地日期的日曆，每週一欄（週一在前），依最忙的那一天分為五級上色。範圍外的日子留白；
 * 移到某一天或使其聚焦，即可讀取它的數值。首次載入（`loading`）在格子的位置畫同尺寸的骨架，
 * 已有資料時重讀（`busy`）淡化；讀取中不顯示「此期間尚無資料」。
 */
@Component({
  selector: 'nx-heatmap-calendar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './heatmap-calendar.scss',
  template: `<figure
    class="heatmap"
    [style.--weeks]="weeks().length"
    [class.is-refreshing]="refreshing()"
    [attr.aria-busy]="busy() || loading() || null"
  >
    <figcaption class="sr-only">{{ label() }}：{{ summary() }}</figcaption>
    <div class="heatmap-days" aria-hidden="true">
      @for (name of weekdays; track name) {
        <span>{{ $index % 2 ? '' : name }}</span>
      }
    </div>
    @if (placeholder()) {
      <span class="skeleton heatmap-skeleton" role="status" [attr.aria-label]="summary()"></span>
    } @else {
      <div class="heatmap-grid" role="list" [attr.aria-label]="label()">
        @for (week of weeks(); track $index) {
          @for (cell of week; track cell.key) {
            @if (cell.inRange) {
              <span
                role="listitem"
                tabindex="0"
                class="heatmap-cell"
                [attr.data-level]="cell.level"
                [style.animation-delay.ms]="cell.delay"
                [attr.aria-label]="cell.key + ' ' + format()(cell.value) + unit()"
                (pointerenter)="active.set(cell)"
                (focus)="active.set(cell)"
                (pointerleave)="active.set(null)"
                (blur)="active.set(null)"
              ></span>
            } @else {
              <span class="heatmap-cell is-outside" aria-hidden="true"></span>
            }
          }
        }
      </div>
    }
    <div class="heatmap-foot" aria-hidden="true">
      <output>
        @if (active(); as cell) {
          {{ cell.key }} · <b>{{ format()(cell.value) }}</b> {{ unit() }}
        } @else {
          {{ summary() }}
        }
      </output>
      <span class="heatmap-scale"
        >少<i data-level="0"></i><i data-level="1"></i><i data-level="2"></i><i data-level="3"></i
        ><i data-level="4"></i>多</span
      >
    </div>
  </figure>`,
})
export class HeatmapCalendar {
  readonly points = input.required<readonly HeatmapPoint[]>();
  /** 第一個與最後一個本地日期，格式為 `YYYY-MM-DD`。 */
  readonly from = input.required<string>();
  readonly through = input.required<string>();
  readonly label = input.required<string>();
  readonly unit = input('');
  readonly format = input<(value: number) => string>(formatNumber);
  /** 首次載入：還沒有資料，畫骨架。 */
  readonly loading = input(false);
  /** 重新讀取：已有資料時淡化；沒有資料時同首次載入。 */
  readonly busy = input(false);
  readonly weekdays = WEEKDAYS;
  readonly active = signal<{ key: string; value: number } | null>(null);
  readonly weeks = computed(() => {
    const values = new Map<string, number>();
    for (const point of this.points())
      values.set(point.date, (values.get(point.date) ?? 0) + point.value);
    const max = Math.max(0, ...values.values());
    const first = day(this.from()),
      last = day(this.through());
    const start = new Date(first);
    start.setDate(start.getDate() - ((start.getDay() + 6) % 7));
    const weeks: {
      key: string;
      value: number;
      level: number;
      inRange: boolean;
      delay: number;
    }[][] = [];
    for (let cursor = new Date(start), index = 0; cursor <= last && index < 60 * 7; index++) {
      if (index % 7 === 0) weeks.push([]);
      const id = localDateKey(cursor),
        value = values.get(id) ?? 0;
      weeks[weeks.length - 1].push({
        key: id,
        value,
        level: value > 0 && max > 0 ? Math.max(1, Math.ceil((value / max) * 4)) : 0,
        inRange: cursor >= first,
        delay: Math.min(600, weeks.length * 18 + (index % 7) * 10),
      });
      cursor.setDate(cursor.getDate() + 1);
    }
    return weeks;
  });
  private readonly total = computed(() =>
    this.points().reduce((sum, point) => sum + point.value, 0),
  );
  protected readonly placeholder = computed(() => this.loading() || (this.busy() && !this.total()));
  protected readonly refreshing = computed(() => this.busy() && !this.placeholder());
  readonly summary = computed(() => {
    if (this.placeholder()) return `正在載入${this.label()}…`;
    const cells = this.weeks()
      .flat()
      .filter((item) => item.inRange);
    const total = cells.reduce((sum, item2) => sum + item2.value, 0);
    const busiest = cells.reduce(
      (first2, second) => (second.value > first2.value ? second : first2),
      cells[0] ?? { key: '', value: 0 },
    );
    return total
      ? `共 ${this.format()(total)}${this.unit()}，最多在 ${busiest.key}（${this.format()(busiest.value)}${this.unit()}）`
      : '此期間尚無資料';
  });
}
