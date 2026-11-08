import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { arrivals } from '../browser/arrivals';
import { runStateTone } from './run-state';

/** 紀錄條的一根：`finishedAt` 為 null 表示還在執行。 */
export interface RunStripItem {
  id: string;
  status: string;
  startedAt: string;
  finishedAt: string | null;
  /** 滑過時的說明（例：「11/08 14:05・成功・清除 120 筆」）。 */
  title?: string;
}

const STATUS_NAMES: Record<string, string> = {
  succeeded: '成功',
  idle: '沒有事做',
  failed: '失敗',
  cancelled: '停止',
  interrupted: '中斷',
  running: '執行中',
};

/** 每根的高度（0.25–1）：依耗時取對數，耗時差上百倍時短的仍看得見，最長的頂滿。 */
export function barHeights(durations: readonly number[]) {
  const logs = durations.map((value) => Math.log1p(Math.max(0, value) / 1000));
  const max = Math.max(...logs, 0);
  return logs.map((value) => (max === 0 ? 0.25 : 0.25 + (value / max) * 0.75));
}

/** 給螢幕閱讀器的摘要：「最近 24 次：成功 20、失敗 2…」，依狀態出現的多寡排序。 */
export function stripSummary(items: readonly Pick<RunStripItem, 'status'>[]) {
  if (!items.length) return '還沒有執行紀錄';
  const counts = new Map<string, number>();
  for (const item of items) counts.set(item.status, (counts.get(item.status) ?? 0) + 1);
  const parts = [...counts]
    .sort((a, b) => b[1] - a[1])
    .map(([status, count]) => `${STATUS_NAMES[status] ?? status} ${count}`);
  return `最近 ${items.length} 次：${parts.join('、')}`;
}

/**
 * 最近幾輪的紀錄條：一輪一根，舊的在左；顏色是結果，高度是耗時（對數）。不足 `slots` 根時左邊以淡色空位補齊，
 * 每個任務的紀錄條因此等寬、可上下對照。輪詢帶來的新一輪以彈簧從底部長出，執行中的那根呼吸。
 * 整條是一張圖（`role="img"`），摘要給螢幕閱讀器；每根的 `title` 只是滑鼠輔助。
 */
@Component({
  selector: 'nx-run-strip',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'run-strip',
    role: 'img',
    '[attr.aria-label]': 'summary()',
  },
  styles: `
    :host {
      display: flex;
      align-items: flex-end;
      gap: 2px;
      height: var(--run-strip-height, 1.5rem);
      min-width: 0;
    }
    .bar,
    .slot {
      flex: 1 1 0;
      min-width: 2px;
      max-width: 0.5rem;
      border-radius: var(--radius-data);
    }
    .slot {
      height: 25%;
      background: color-mix(in srgb, var(--muted) 14%, transparent);
    }
    .bar {
      --bar-color: var(--muted);
      height: calc(var(--h) * 100%);
      background: var(--bar-color);
      transform-origin: bottom;
      transition:
        height var(--motion-morph) var(--ease-spring),
        background-color var(--motion-panel) var(--ease);
    }
    .bar[data-tone='info'] {
      --bar-color: var(--accent);
    }
    .bar[data-tone='success'] {
      --bar-color: var(--success);
    }
    .bar[data-tone='warning'] {
      --bar-color: var(--warning);
    }
    .bar[data-tone='danger'] {
      --bar-color: var(--danger);
    }
    .bar[data-tone='neutral'] {
      --bar-color: color-mix(in srgb, var(--muted) 55%, transparent);
    }
    .bar.is-new {
      animation: bar-arrive var(--motion-morph) var(--ease-spring);
    }
    .bar.is-running {
      animation: live-breathe var(--motion-signal) var(--ease) infinite;
    }
    @media (forced-colors: active) {
      .bar {
        background: CanvasText;
      }
    }
  `,
  template: `@for (slot of padding(); track $index) {
      <span class="slot" aria-hidden="true"></span>
    }
    @for (bar of bars(); track bar.id) {
      <span
        class="bar"
        aria-hidden="true"
        [class.is-new]="bar.fresh"
        [class.is-running]="bar.running"
        [attr.data-tone]="bar.tone"
        [style.--h]="bar.height"
        [title]="bar.title"
      ></span>
    }`,
})
export class RunStrip {
  /** 舊到新。 */
  readonly runs = input.required<readonly RunStripItem[]>();
  /** 固定的根數；超過時只畫最新的這幾根。 */
  readonly slots = input(24);
  private readonly arrived = arrivals();

  private readonly shown = computed(() => this.runs().slice(-this.slots()));
  readonly padding = computed(() => Array.from({ length: this.slots() - this.shown().length }));
  readonly bars = computed(() => {
    const items = this.shown();
    const heights = barHeights(
      items.map((item) =>
        item.finishedAt ? Date.parse(item.finishedAt) - Date.parse(item.startedAt) : 0,
      ),
    );
    const fresh = this.arrived(items.map((item) => item.id));
    return items.map((item, index) => ({
      id: item.id,
      fresh: fresh(item.id),
      running: !item.finishedAt,
      tone: item.finishedAt ? runStateTone(item.status) : 'info',
      height: item.finishedAt ? heights[index] : 1,
      title: item.title ?? STATUS_NAMES[item.status] ?? item.status,
    }));
  });
  readonly summary = computed(() => stripSummary(this.shown()));
}
