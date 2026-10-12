import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

let sequence = 0;

/**
 * A word-sized trend line in `currentColor`, drawn in from left to right. It is decorative: the
 * figure it accompanies carries the meaning, so give it a `label` only when it stands alone.
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
      stroke-dasharray: 1;
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
    <path class="line" pathLength="1" [attr.d]="path()" />
    @if (points().length) {
      <circle class="end" r="1.6" [attr.cx]="last().x" [attr.cy]="last().y" />
    }
  </svg>`,
})
export class Sparkline {
  readonly values = input.required<readonly number[]>();
  readonly area = input(true);
  readonly label = input('');
  readonly id = 'nx-sparkline-' + ++sequence;
  readonly points = computed(() => {
    const values = this.values();
    if (!values.length) return [];
    const max = Math.max(...values),
      min = Math.min(0, ...values);
    const span = max - min || 1;
    return values.map((value, i) => ({
      x: values.length === 1 ? 100 : (i * 100) / (values.length - 1),
      y: 30 - ((value - min) / span) * 28,
    }));
  });
  readonly last = computed(() => this.points()[this.points().length - 1]);
  readonly path = computed(() => {
    const points = this.points();
    if (points.length === 1) return `M0 ${points[0].y} L100 ${points[0].y}`;
    // A light Catmull-Rom smoothing keeps the line honest at the data points.
    return points
      .map((p, i) => {
        if (!i) return `M${p.x} ${p.y}`;
        const a = points[i - 2] ?? points[i - 1],
          b = points[i - 1],
          c = points[i + 1] ?? p;
        const c1x = b.x + (p.x - a.x) / 6,
          c1y = b.y + (p.y - a.y) / 6,
          c2x = p.x - (c.x - b.x) / 6,
          c2y = p.y - (c.y - b.y) / 6;
        return `C${c1x} ${c1y} ${c2x} ${c2y} ${p.x} ${p.y}`;
      })
      .join(' ');
  });
  readonly fill = computed(() => (this.points().length ? `${this.path()} L100 32 L0 32 Z` : ''));
}
