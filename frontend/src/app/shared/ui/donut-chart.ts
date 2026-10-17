import { ChangeDetectionStrategy, Component, computed, input, signal } from '@angular/core';
import { formatNumber } from '../browser/format';

export interface DonutSegment {
  key: string;
  label: string;
  value: number;
  /** CSS 顏色；預設使用分類系列的色盤。 */
  color?: string;
}

/** 分類系列的顏色，兩種主題下都能分辨；總覽的圖例也會重用。 */
export const SERIES_COLORS = [
  'var(--series-1)',
  'var(--series-2)',
  'var(--series-3)',
  'var(--series-4)',
  'var(--series-5)',
  'var(--series-6)',
  'var(--series-7)',
];

/**
 * 中間顯示總數（或被 hover 的部分）的環狀圖，圖例同時是可讀的數值列表。區段順時針展開。
 */
@Component({
  selector: 'nx-donut-chart',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './donut-chart.scss',
  template: `<figure class="donut" [class.is-empty]="!total()">
    <div class="donut-ring">
      <svg viewBox="0 0 42 42" aria-hidden="true">
        <circle class="donut-track" cx="21" cy="21" r="15.915" />
        @for (part of parts(); track part.key) {
          <circle
            class="donut-part"
            cx="21"
            cy="21"
            r="15.915"
            pathLength="100"
            [class.is-dim]="active() !== null && active() !== part.key"
            [style.stroke]="part.color"
            [style.stroke-dasharray]="part.dash"
            [style.stroke-dashoffset]="part.offset"
            [style.animation-delay.ms]="$index * 80"
            (pointerenter)="active.set(part.key)"
            (pointerleave)="active.set(null)"
          />
        }
      </svg>
      <div class="donut-center">
        <strong>{{ center().value }}</strong>
        <span>{{ center().label }}</span>
      </div>
    </div>
    <figcaption>
      <span class="sr-only">{{ label() }}</span>
      <ul class="donut-legend">
        @for (part of parts(); track part.key) {
          <li
            tabindex="0"
            [class.is-active]="active() === part.key"
            (focus)="active.set(part.key)"
            (blur)="active.set(null)"
            (pointerenter)="active.set(part.key)"
            (pointerleave)="active.set(null)"
          >
            <i [style.background]="part.color"></i><span>{{ part.label }}</span
            ><b>{{ format()(part.value) }}</b
            ><small>{{ part.percent }}%</small>
          </li>
        }
      </ul>
      @if (!total()) {
        <p class="donut-empty">{{ empty() }}</p>
      }
    </figcaption>
  </figure>`,
})
export class DonutChart {
  readonly segments = input.required<readonly DonutSegment[]>();
  readonly label = input.required<string>();
  /** 中間總數下方的說明文字。 */
  readonly totalLabel = input('合計');
  readonly format = input<(value: number) => string>(formatNumber);
  readonly empty = input('此期間尚無資料');
  readonly active = signal<string | null>(null);
  readonly total = computed(() =>
    this.segments().reduce((sum, item2) => sum + Math.max(0, item2.value), 0),
  );
  readonly parts = computed(() => {
    const total = this.total() || 1;
    let start = 0;
    return this.segments()
      .filter((item) => item.value > 0)
      .map((segment, index) => {
        const share = (segment.value / total) * 100;
        // 細縫把相鄰的區段分開，但不會遮住小的部分。
        const length = Math.max(0.0001, share - (this.segments().length > 1 ? 0.6 : 0));
        const part = {
          ...segment,
          color: segment.color ?? SERIES_COLORS[index % SERIES_COLORS.length],
          dash: `${length} ${100 - length}`,
          offset: String(25 - start),
          percent: share >= 10 ? Math.round(share) : Math.round(share * 10) / 10,
        };
        start += share;
        return part;
      });
  });
  readonly center = computed(() => {
    const active = this.parts().find((item) => item.key === this.active());
    return active
      ? { value: this.format()(active.value), label: active.label }
      : { value: this.format()(this.total()), label: this.totalLabel() };
  });
}
