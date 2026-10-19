import { uniqueId } from '../browser/unique-id';
import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/**
 * 字詞大小的趨勢線，使用 `currentColor`，由左至右繪出。它是裝飾：
 * 所附的數字才承載意義，因此只有單獨出現時才給 `label`。
 */
@Component({
  selector: 'nx-sparkline',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'sparkline' },
  styles: `
    :host {
      display: block;
      width: 100%;
      height: var(--sparkline-height, 2.25rem);
      color: var(--sparkline-color, var(--accent));
    }
    svg {
      display: block;
      width: 100%;
      height: 100%;
      overflow: visible;
    }
    .line {
      fill: none;
      stroke: currentColor;
      stroke-width: var(--sparkline-stroke, 1.75);
      stroke-linecap: round;
      stroke-linejoin: round;
      vector-effect: non-scaling-stroke;
      animation: sparkline-draw 1.1s var(--ease) both;
    }
    .end {
      fill: currentColor;
    }
  `,
  template: `<svg
    viewBox="0 0 100 32"
    preserveAspectRatio="none"
    [attr.role]="label() ? 'img' : null"
    [attr.aria-label]="label() || null"
    [attr.aria-hidden]="label() ? null : 'true'"
  >
    @if (area()) {
      <defs>
        <linearGradient [id]="id" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stop-color="currentColor" stop-opacity="0.28" />
          <stop offset="100%" stop-color="currentColor" stop-opacity="0" />
        </linearGradient>
      </defs>
      <path [attr.d]="fill()" [attr.fill]="'url(#' + id + ')'" />
    }
    <path class="line" [attr.d]="path()" />
    @if (points().length) {
      <circle class="end" r="1.6" [attr.cx]="last().x" [attr.cy]="last().y" />
    }
  </svg>`,
})
export class Sparkline {
  readonly values = input.required<readonly number[]>();
  readonly area = input(true);
  readonly label = input('');
  readonly id = uniqueId('nx-sparkline');
  readonly points = computed(() => {
    const values = this.values();
    if (!values.length) return [];
    const max = Math.max(...values),
      min = Math.min(0, ...values);
    const span = max - min || 1;
    return values.map((value, index) => ({
      x: values.length === 1 ? 100 : (index * 100) / (values.length - 1),
      y: 30 - ((value - min) / span) * 28,
    }));
  });
  readonly last = computed(() => this.points()[this.points().length - 1]);
  readonly path = computed(() => {
    const points = this.points();
    if (points.length === 1) return `M0 ${points[0].y} L100 ${points[0].y}`;
    // 輕量的 Catmull-Rom 平滑，讓線條在資料點上保持誠實。
    return points
      .map((part, index2) => {
        if (!index2) return `M${part.x} ${part.y}`;
        const first = points[index2 - 2] ?? points[index2 - 1],
          second = points[index2 - 1],
          card = points[index2 + 1] ?? part;
        const c1x = second.x + (part.x - first.x) / 6,
          c1y = second.y + (part.y - first.y) / 6,
          c2x = part.x - (card.x - second.x) / 6,
          c2y = part.y - (card.y - second.y) / 6;
        return `C${c1x} ${c1y} ${c2x} ${c2y} ${part.x} ${part.y}`;
      })
      .join(' ');
  });
  readonly fill = computed(() => (this.points().length ? `${this.path()} L100 32 L0 32 Z` : ''));
}
