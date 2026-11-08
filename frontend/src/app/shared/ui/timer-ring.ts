import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { ServerClock } from '../browser/server-clock';

/** `countdown` 倒數到 `to`；`running` 執行中；`due` 時間到了、等待開始；`paused` 暫停；`off` 不會執行。 */
export type TimerRingMode = 'countdown' | 'running' | 'due' | 'paused' | 'off';

/** 倒數剩下的比例（1 → 0）；時間窗不合理時回傳 null，圓環畫成滿的。 */
export function remainingRatio(from: string | null, to: string | null, now: number) {
  if (!from || !to) return null;
  const start = Date.parse(from),
    end = Date.parse(to);
  if (Number.isNaN(start) || Number.isNaN(end) || end <= start) return null;
  return Math.min(1, Math.max(0, (end - now) / (end - start)));
}

/**
 * 圓環計時器：倒數時圓弧隨共用的 `ServerClock` 逐秒線性消退，像沙漏；執行中有進度時圓弧以彈簧填滿，沒有進度時一段圓弧繞行；
 * 時間到了還沒開始（`due`）時整圈呼吸；暫停是虛線、`off` 只剩底圈。中間可放圖示或文字（`ng-content`），文字沿用圓環的顏色與小字，呼叫端不必另外設定。
 * 這是裝飾：旁邊的文字（例如 `TickingTime`）才承載時間，所以整個元件對輔助技術隱藏。大小用 `--timer-ring-size`。
 */
@Component({
  selector: 'nx-timer-ring',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'timer-ring',
    'aria-hidden': 'true',
    '[attr.data-mode]': 'mode()',
    '[attr.data-tone]': 'tone()',
    '[class.is-indeterminate]': 'indeterminate()',
  },
  styles: `
    :host {
      --timer-ring-color: var(--accent);
      position: relative;
      display: inline-grid;
      place-items: center;
      flex: none;
      width: var(--timer-ring-size, 2.25rem);
      height: var(--timer-ring-size, 2.25rem);
      color: var(--timer-ring-color);
    }
    :host([data-tone='success']) {
      --timer-ring-color: var(--success);
    }
    :host([data-tone='warning']) {
      --timer-ring-color: var(--warning);
    }
    :host([data-tone='danger']) {
      --timer-ring-color: var(--danger);
    }
    :host([data-tone='neutral']) {
      --timer-ring-color: var(--muted);
    }
    svg {
      position: absolute;
      inset: 0;
      width: 100%;
      height: 100%;
      rotate: -90deg;
      overflow: visible;
    }
    circle {
      fill: none;
      stroke-width: 3;
      stroke-linecap: round;
    }
    .track {
      stroke: color-mix(in srgb, var(--timer-ring-color) 16%, transparent);
      transition: stroke var(--motion-panel) var(--ease);
    }
    .arc {
      stroke: currentColor;
      stroke-dasharray: 100 100;
      /* 每秒更新一次，以一秒的線性過渡接起來，圓弧看起來是連續消退。 */
      transition:
        stroke-dashoffset 1s linear,
        stroke var(--motion-panel) var(--ease),
        opacity var(--motion-panel) var(--ease);
    }
    :host([data-mode='running']) .arc {
      transition:
        stroke-dashoffset var(--motion-morph) var(--ease-spring),
        stroke var(--motion-panel) var(--ease);
    }
    :host(.is-indeterminate) svg {
      animation: orbit-turn var(--motion-spin) linear infinite;
    }
    :host(.is-indeterminate) .arc {
      stroke-dasharray: 28 100;
      stroke-dashoffset: 0;
      transition: none;
    }
    :host([data-mode='due']) .arc {
      animation: live-breathe var(--motion-signal) var(--ease) infinite;
    }
    :host([data-mode='paused']) .track {
      stroke: color-mix(in srgb, var(--timer-ring-color) 45%, transparent);
      stroke-dasharray: 4 5.4;
    }
    :host([data-mode='paused']) .arc,
    :host([data-mode='off']) .arc {
      opacity: 0;
    }
    .center {
      position: relative;
      display: inline-grid;
      place-items: center;
      font-size: var(--p-font-xs);
      font-weight: 600;
      line-height: 1;
    }
    @media (forced-colors: active) {
      .arc {
        stroke: CanvasText;
      }
    }
  `,
  template: `<svg viewBox="0 0 36 36">
      <circle class="track" cx="18" cy="18" r="15.9155" />
      <circle
        class="arc"
        cx="18"
        cy="18"
        r="15.9155"
        pathLength="100"
        [style.stroke-dashoffset]="offset()"
      />
    </svg>
    <span class="center"><ng-content /></span>`,
})
export class TimerRing {
  readonly mode = input<TimerRingMode>('countdown');
  /** 倒數時間窗的起點（ISO），例如上一輪結束的時間。 */
  readonly from = input<string | null>(null);
  /** 倒數的終點（ISO），例如下次執行時間。 */
  readonly to = input<string | null>(null);
  /** 執行中的進度（0–1）；null 時圓弧繞行。 */
  readonly progress = input<number | null>(null);

  private readonly clock = inject(ServerClock);
  readonly indeterminate = computed(() => this.mode() === 'running' && this.progress() === null);
  readonly tone = computed(() => {
    switch (this.mode()) {
      case 'paused':
        return 'warning';
      case 'off':
        return 'neutral';
      default:
        return 'info';
    }
  });
  /** 圓弧缺口（0 = 滿圈、100 = 空）。 */
  readonly offset = computed(() => {
    switch (this.mode()) {
      case 'countdown': {
        const ratio = remainingRatio(this.from(), this.to(), this.clock.now());
        return ratio === null ? 0 : 100 - ratio * 100;
      }
      case 'running': {
        const progress = this.progress();
        return progress === null ? 0 : 100 - Math.min(1, Math.max(0, progress)) * 100;
      }
      default:
        return 0;
    }
  });

  constructor() {
    this.clock.watch();
  }
}
