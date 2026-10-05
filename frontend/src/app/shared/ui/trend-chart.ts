import { ChangeDetectionStrategy, Component, computed, input, signal } from '@angular/core';

export interface TrendPoint { label: string; value: number }
let sequence = 0;
@Component({
  selector: 'nx-trend-chart', changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './trend-chart.scss',
  template: `<figure class="trend-chart">
    <figcaption>{{ label() }}</figcaption>
    @if (points().length) {
      <div class="trend-plot" (pointermove)="inspect($event)" (pointerleave)="active.set(null)">
        <svg viewBox="0 0 800 180" preserveAspectRatio="none" role="img" [attr.aria-label]="label() + '，最高 ' + format(max())">
          <defs><linearGradient [id]="id" x1="0" y1="0" x2="0" y2="1"><stop offset="0%" stop-color="currentColor" stop-opacity="0.2" /><stop offset="100%" stop-color="currentColor" stop-opacity="0" /></linearGradient></defs>
          <path d="M 0 40 H 800 M 0 100 H 800 M 0 160 H 800" class="trend-grid" />
          <path [attr.d]="area()" [attr.fill]="'url(#' + id + ')'" />
          <polyline [attr.points]="line()" fill="none" class="trend-line" />
          @if (selected(); as point) { <circle [attr.cx]="x(active()!)" [attr.cy]="y(point.value)" r="5" class="trend-dot" /> }
        </svg>
        @if (selected(); as point) { <output class="trend-tooltip" [style.left.%]="Math.max(12, Math.min(88, x(active()!) / 8))">{{ point.label }}<strong>{{ format(point.value) }}</strong></output> }
      </div>
      <div class="trend-axis"><span>{{ points()[0].label }}</span><span>{{ points()[points().length - 1].label }}</span></div>
      <label class="sr-only" [for]="id + '-range'">逐日檢視{{ label() }}</label>
      <input class="trend-scrubber" type="range" [id]="id + '-range'" min="0" [max]="points().length - 1" [value]="active() ?? 0" (input)="active.set(+$any($event.target).value)" (focus)="active.set(0)" (blur)="active.set(null)" [attr.aria-valuetext]="selected() ? selected()!.label + ' ' + format(selected()!.value) : ''" />
      <details class="trend-data"><summary>檢視圖表數據</summary><table><caption class="sr-only">{{ label() }}</caption><thead><tr><th scope="col">日期</th><th scope="col">數值</th></tr></thead><tbody>@for (point of points(); track point.label) { <tr><th scope="row">{{ point.label }}</th><td>{{ format(point.value) }}</td></tr> }</tbody></table></details>
    } @else { <div class="chart-empty">此期間尚無資料</div> }
  </figure>`,
})
export class TrendChart {
  readonly points = input.required<TrendPoint[]>(); readonly label = input.required<string>(); readonly currency = input('');
  readonly id = 'nx-trend-' + ++sequence; readonly Math = Math; readonly active = signal<number | null>(null);
  readonly max = computed(() => Math.max(1e-8, ...this.points().map(x => x.value)));
  readonly selected = computed(() => this.active() === null ? null : this.points()[this.active()!] ?? null);
  x(index: number) { return this.points().length <= 1 ? 400 : 12 + index * 776 / (this.points().length - 1); }
  y(value: number) { return 160 - value / this.max() * 136; }
  readonly line = computed(() => this.points().map((x, i) => `${this.x(i)},${this.y(x.value)}`).join(' '));
  readonly area = computed(() => this.points().length ? `M${this.x(0)},176 L${this.line().replaceAll(' ', ' L')} L${this.x(this.points().length - 1)},176 Z` : '');
  inspect(event: PointerEvent) { const bounds = (event.currentTarget as HTMLElement).getBoundingClientRect(); const portion = Math.max(0, Math.min(1, (event.clientX - bounds.left) / bounds.width)); this.active.set(Math.round(portion * (this.points().length - 1))); }
  format(value: number) { return this.currency() ? new Intl.NumberFormat('zh-TW', { style: 'currency', currency: this.currency(), currencyDisplay: 'code', maximumFractionDigits: 8 }).format(value) : value.toLocaleString('zh-TW', { maximumFractionDigits: 2 }); }
}
