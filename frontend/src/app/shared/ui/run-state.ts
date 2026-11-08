import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import type { BadgeTone } from './count-badge';

/** 一次執行（排程任務、背景工作）的狀態。 */
export type RunStateKind =
  'queued' | 'running' | 'succeeded' | 'idle' | 'failed' | 'cancelled' | 'interrupted' | 'paused';

const TONES: Record<RunStateKind, BadgeTone> = {
  queued: 'info',
  running: 'info',
  succeeded: 'success',
  idle: 'neutral',
  failed: 'danger',
  cancelled: 'neutral',
  interrupted: 'warning',
  paused: 'warning',
};

/** 每個狀態的符號路徑（36×36 座標）；`pathLength="1"` 讓描繪動效不必量長度。 */
const GLYPHS: Record<RunStateKind, string[]> = {
  queued: [],
  running: [],
  succeeded: ['M11.5 18.5l4.5 4.5 8.5-9'],
  idle: ['M12 18h12'],
  failed: ['M13 13l10 10', 'M23 13l-10 10'],
  cancelled: ['M13.5 13.5h9v9h-9z'],
  interrupted: ['M19.5 10.5l-5 8h7l-5 8'],
  paused: ['M15 13v10', 'M21 13v10'],
};

/** 狀態對應的色調，給同一列的其他元件（徽章、紀錄條）共用。 */
export function runStateTone(state: string): BadgeTone {
  return TONES[state as RunStateKind] ?? 'neutral';
}

/**
 * 執行狀態的圓形圖示：排隊中三點依序呼吸、執行中一段圓弧繞行，結束的狀態畫出符號（成功打勾、失敗打叉、停止方塊、中斷閃電、沒事可做一橫）。
 * 自動更新造成的狀態變化（例：執行中→成功）時，圓先以彈簧收一下，符號再一筆畫出來；第一次顯示與減少動態時不播。
 * 給 `label` 時供螢幕閱讀器讀出，否則視為裝飾（旁邊的文字承載狀態）。大小用 `--run-state-size`。
 */
@Component({
  selector: 'nx-run-state',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'run-state',
    '[attr.role]': 'label() ? "img" : null',
    '[attr.aria-label]': 'label() || null',
    '[attr.aria-hidden]': 'label() ? null : "true"',
    '[attr.data-state]': 'state()',
    '[attr.data-tone]': 'tone()',
    '[attr.data-changed]': 'changed() ? "" : null',
  },
  styles: `
    :host {
      --badge-color: var(--muted);
      display: inline-grid;
      flex: none;
      width: var(--run-state-size, 1.25rem);
      height: var(--run-state-size, 1.25rem);
      border-radius: 50%;
      color: var(--badge-color);
      transition: color var(--motion-panel) var(--ease);
    }
    :host([data-tone='info']) {
      --badge-color: var(--accent);
    }
    :host([data-tone='success']) {
      --badge-color: var(--success);
    }
    :host([data-tone='warning']) {
      --badge-color: var(--warning);
    }
    :host([data-tone='danger']) {
      --badge-color: var(--danger);
    }
    :host([data-changed]) {
      animation: status-settle var(--motion-morph) var(--ease-spring);
    }
    svg {
      width: 100%;
      height: 100%;
      overflow: visible;
    }
    .disc {
      fill: color-mix(in srgb, currentColor 12%, var(--surface));
      stroke: color-mix(in srgb, currentColor 30%, transparent);
      stroke-width: 1.5;
      transition:
        fill var(--motion-panel) var(--ease),
        stroke var(--motion-panel) var(--ease);
    }
    .glyph {
      fill: none;
      stroke: currentColor;
      stroke-width: 2.6;
      stroke-linecap: round;
      stroke-linejoin: round;
    }
    :host([data-changed]) .glyph {
      stroke-dasharray: 1;
      animation: run-draw var(--motion-morph) var(--ease) both;
      animation-delay: calc(var(--motion-fast) + var(--i, 0) * var(--motion-stagger));
    }
    .orbit {
      fill: none;
      stroke: currentColor;
      stroke-width: 2.6;
      stroke-linecap: round;
      stroke-dasharray: 0.28 1;
      transform-origin: 18px 18px;
      animation: orbit-turn var(--motion-spin) linear infinite;
    }
    .dot {
      fill: currentColor;
      animation: live-breathe var(--motion-signal) var(--ease) infinite;
      animation-delay: calc(var(--i) * 0.18s);
    }
    @media (forced-colors: active) {
      .glyph,
      .orbit {
        stroke: CanvasText;
      }
      .dot {
        fill: CanvasText;
      }
    }
  `,
  template: `<svg viewBox="0 0 36 36">
    <circle class="disc" cx="18" cy="18" r="16" />
    @switch (state()) {
      @case ('running') {
        <circle class="orbit" cx="18" cy="18" r="10" pathLength="1" />
      }
      @case ('queued') {
        @for (x of dots; track x; let i = $index) {
          <circle class="dot" [attr.cx]="x" cy="18" r="2.2" [style.--i]="i" />
        }
      }
      @default {
        @for (path of glyph(); track path; let i = $index) {
          <path class="glyph" pathLength="1" [attr.d]="path" [style.--i]="i" />
        }
      }
    }
  </svg>`,
})
export class RunState {
  readonly state = input.required<RunStateKind>();
  /** 給螢幕閱讀器的狀態名稱（例：「執行成功」）；省略時對輔助技術隱藏。 */
  readonly label = input<string>();
  readonly dots = [11, 18, 25];
  readonly tone = computed(() => TONES[this.state()] ?? 'neutral');
  readonly glyph = computed(() => GLYPHS[this.state()] ?? []);
  readonly changed = signal(false);

  constructor() {
    let previous: RunStateKind | undefined;
    let frame = 0;
    inject(DestroyRef).onDestroy(() => cancelAnimationFrame(frame));
    effect(() => {
      const state = this.state();
      const before = previous;
      previous = state;
      if (before === undefined || before === state) return;
      // 先移除再於下一幀加回，連續變化時動畫才會重播。
      this.changed.set(false);
      cancelAnimationFrame(frame);
      frame = requestAnimationFrame(() => this.changed.set(true));
    });
  }
}
