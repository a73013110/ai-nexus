import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { formatTimeOfDay } from '../browser/format';
import { TimeLanes } from './time-lanes';

/** `planned` 是未來的預定；其他是已發生的結果，`active` 正在進行。 */
export type TimeMarkKind = 'planned' | 'neutral' | 'success' | 'warning' | 'danger' | 'active';

/** 時間軸上的一點。 */
export interface TimeMark {
  at: number;
  kind: TimeMarkKind;
  /** 提示文字；省略時顯示時刻。 */
  title?: string;
  /** 給了就在軌道上畫圓點與這段文字（例：`03:15`），只給少數幾個點用。 */
  label?: string;
}

const WIDTH = 1000;
const MIDDLE = 20;
/** 間距小於這個寬度（千分比）的點併成一條線：逐一畫只會糊成一排鋸齒。 */
const DENSE_GAP = 8;
const REACH: Record<TimeMarkKind, number> = {
  planned: 4,
  neutral: 4,
  success: 9,
  warning: 9,
  active: 11,
  danger: 11,
};
/** 畫的順序：重要的在上層。 */
const LAYERS = [
  'planned-band',
  'neutral-band',
  'planned',
  'neutral',
  'success',
  'warning',
  'active',
  'danger',
];
/** 指標離記號多少像素內算指到它。 */
const POINTER_REACH = 8;

/**
 * 把記號轉成每種類別一條 SVG 路徑（舊到新）：一列最多幾條 `path`，幾百個點也不會變成幾百個節點；
 * 字串相同時 Angular 不會動畫面，輪詢沒帶來新資料就完全不寫 DOM。
 */
export function lanePaths(marks: readonly TimeMark[], from: number, to: number) {
  const parts = new Map<string, string>();
  const add = (layer: string, part: string) => parts.set(layer, (parts.get(layer) ?? '') + part);
  const tick = (x: number, kind: TimeMarkKind) =>
    add(kind, `M${x} ${MIDDLE - REACH[kind]}V${MIDDLE + REACH[kind]}`);
  const quiet: Record<'planned' | 'neutral', number[]> = { planned: [], neutral: [] };
  for (const mark of marks) {
    if (mark.at < from || mark.at > to || mark.label) continue;
    const x = Math.round(((mark.at - from) / (to - from)) * WIDTH * 10) / 10;
    if (mark.kind === 'planned' || mark.kind === 'neutral') quiet[mark.kind].push(x);
    else tick(x, mark.kind);
  }
  for (const kind of ['planned', 'neutral'] as const) {
    const xs = quiet[kind];
    let start = 0;
    for (let index = 1; index <= xs.length; index++) {
      if (index < xs.length && xs[index] - xs[index - 1] <= DENSE_GAP) continue;
      if (index - start > 1) add(`${kind}-band`, `M${xs[start]} ${MIDDLE}H${xs[index - 1]}`);
      else tick(xs[start], kind);
      start = index;
    }
  }
  return LAYERS.filter((layer) => parts.has(layer)).map((kind) => ({ kind, d: parts.get(kind)! }));
}

/** 離 `at` 最近、且在 `reach` 內的記號（`marks` 舊到新，二分搜尋）。 */
export function nearestMark(marks: readonly TimeMark[], at: number, reach: number) {
  let low = 0;
  let high = marks.length;
  while (low < high) {
    const middle = (low + high) >> 1;
    if (marks[middle].at < at) low = middle + 1;
    else high = middle;
  }
  const candidates = [marks[low - 1], marks[low]].filter(Boolean);
  const best = candidates.sort((a, b) => Math.abs(a.at - at) - Math.abs(b.at - at))[0];
  return best && Math.abs(best.at - at) <= reach ? best : null;
}

/**
 * `TimeLanes` 裡的一列：名稱、一條時間軌道與列尾動作。名稱與動作以 `lane-label`、`lane-end` 投影。
 * 軌道是一張圖（`role="img"`），`summary` 給螢幕閱讀器；每個記號的提示只是滑鼠輔助。
 */
@Component({
  selector: 'nx-time-lane',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './time-lane.scss',
  host: { role: 'listitem' },
  template: `<div class="lane-label"><ng-content select="[lane-label]" /></div>
    <div
      class="lane-track"
      role="img"
      [attr.aria-label]="summary()"
      (pointermove)="point($event)"
      (pointerleave)="lanes.hideTip()"
    >
      <svg viewBox="0 0 1000 40" preserveAspectRatio="none" aria-hidden="true">
        @for (path of paths(); track path.kind) {
          <path [attr.data-kind]="path.kind" [attr.d]="path.d" />
        }
      </svg>
      @for (flag of flags(); track flag.at) {
        <span
          class="lane-flag"
          [class.is-flipped]="flag.position > 88"
          [style.inset-inline-start.%]="flag.position"
          aria-hidden="true"
          ><i></i>{{ flag.label }}</span
        >
      }
      @if (note()) {
        <span class="lane-note" aria-hidden="true">{{ note() }}</span>
      }
    </div>
    <div class="lane-end"><ng-content select="[lane-end]" /></div>`,
})
export class TimeLane {
  /** 舊到新。 */
  readonly marks = input.required<readonly TimeMark[]>();
  readonly summary = input.required<string>();
  /** 現在線右邊的一句說明，沒有預定時說明原因（例：已暫停）。 */
  readonly note = input('');

  readonly lanes = inject(TimeLanes);
  readonly paths = computed(() => lanePaths(this.marks(), this.lanes.from(), this.lanes.to()));
  readonly flags = computed(() => {
    const from = this.lanes.from();
    const to = this.lanes.to();
    return this.marks()
      .filter((mark) => mark.label && mark.at >= from && mark.at <= to)
      .map((mark) => ({
        at: mark.at,
        label: mark.label,
        position: ((mark.at - from) / (to - from)) * 100,
      }));
  });

  /** 一列只處理指到的那個記號；同一個記號不重設提示，移動指標不會造成重繪。 */
  point(event: PointerEvent) {
    const track = event.currentTarget as HTMLElement;
    const box = track.getBoundingClientRect();
    const from = this.lanes.from();
    const span = this.lanes.to() - from;
    const at = from + ((event.clientX - box.left) / box.width) * span;
    const mark = nearestMark(this.marks(), at, (POINTER_REACH / box.width) * span);
    if (!mark) {
      this.lanes.hideTip();
      return;
    }
    const time = formatTimeOfDay(mark.at);
    const text = mark.title ?? (mark.kind === 'planned' ? `預定 ${time}` : time);
    this.lanes.showTip(text, ((mark.at - from) / span) * 100, track);
  }
}
