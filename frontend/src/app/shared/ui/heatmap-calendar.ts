import { ChangeDetectionStrategy, Component, computed, input, signal } from '@angular/core';
import { formatNumber } from '../browser/format';

export interface HeatmapPoint {
  date: string;
  value: number;
}

const WEEKDAYS = ['一', '二', '三', '四', '五', '六', '日'];

function day(value: string) {
  const [y, m, d] = value.split('-').map(Number);
  return new Date(y, m - 1, d);
}
function key(date: Date) {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;
}

/**
 * A calendar of local days, one column per week (Monday first), shaded in five steps relative to
 * the busiest day. Days outside the range stay blank; hover or focus a day to read its value.
 */
@Component({
  selector: 'nx-heatmap-calendar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './heatmap-calendar.scss',
  template: `<figure class="heatmap" [style.--weeks]="weeks().length">
    <figcaption class="sr-only">{{ label() }}：{{ summary() }}</figcaption>
    <div class="heatmap-days" aria-hidden="true">
      @for (name of weekdays; track name) {
        <span>{{ $index % 2 ? '' : name }}</span>
      }
    </div>
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
  /** First and last local day, `YYYY-MM-DD`. */
  readonly from = input.required<string>();
  readonly through = input.required<string>();
  readonly label = input.required<string>();
  readonly unit = input('');
  readonly format = input<(value: number) => string>(formatNumber);
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
    for (let cursor = new Date(start), i = 0; cursor <= last && i < 60 * 7; i++) {
      if (i % 7 === 0) weeks.push([]);
      const id = key(cursor),
        value = values.get(id) ?? 0;
      weeks[weeks.length - 1].push({
        key: id,
        value,
        level: value > 0 && max > 0 ? Math.max(1, Math.ceil((value / max) * 4)) : 0,
        inRange: cursor >= first,
        delay: Math.min(600, weeks.length * 18 + (i % 7) * 10),
      });
      cursor.setDate(cursor.getDate() + 1);
    }
    return weeks;
  });
  readonly summary = computed(() => {
    const cells = this.weeks()
      .flat()
      .filter((x) => x.inRange);
    const total = cells.reduce((sum, x) => sum + x.value, 0);
    const busiest = cells.reduce(
      (a, b) => (b.value > a.value ? b : a),
      cells[0] ?? { key: '', value: 0 },
    );
    return total
      ? `共 ${this.format()(total)}${this.unit()}，最多在 ${busiest.key}（${this.format()(busiest.value)}${this.unit()}）`
      : '此期間尚無資料';
  });
}
