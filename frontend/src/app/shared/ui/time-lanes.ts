import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  inject,
  input,
  signal,
} from '@angular/core';
import { formatDateTimeInput, formatTimeOfDay } from '../browser/format';
import { ServerClock } from '../browser/server-clock';

const HOUR = 3_600_000;
/** 現在線移動一格約 30 秒：再密也看不出差別，不必跟著時鐘每秒改樣式。 */
const NOW_STEP = 30_000;

/** 底色區段（例：夜間），毫秒時間戳。 */
export interface TimeShade {
  from: number;
  to: number;
}

interface TimeTip {
  text: string;
  /** 記號在軌道上的百分比，只用來決定提示靠哪一側。 */
  position: number;
  left: number;
  top: number;
}

const percent = (at: number, from: number, to: number) =>
  to > from ? ((at - from) / (to - from)) * 100 : 0;

/** 工作區時區每 `stepHours` 小時一個刻度；跨日的刻度寫日期，其餘寫時刻。 */
export function timeTicks(from: number, to: number, stepHours: number) {
  const ticks: { at: number; position: number; label: string }[] = [];
  for (let at = Math.ceil(from / HOUR) * HOUR; at <= to; at += HOUR) {
    const wall = formatDateTimeInput(new Date(at));
    const hour = Number(wall.slice(11, 13));
    if (hour % stepHours !== 0) continue;
    ticks.push({
      at,
      position: percent(at, from, to),
      label:
        hour === 0
          ? `${Number(wall.slice(5, 7))}/${Number(wall.slice(8, 10))}`
          : wall.slice(11, 16),
    });
  }
  return ticks;
}

/**
 * 泳道時間軸：一列一條 `TimeLane`，共用刻度、底色、現在線與提示，適合「過去的結果＋未來的預定」放在同一條時間上。
 * 這些都只畫一份，泳道再多也不會變重；現在線以 CSS 變數移動，只有跨過 30 秒才改樣式，不跟著時鐘每秒重繪。
 * 欄寬用 `--time-lane-label`、`--time-lane-end`；容器窄於 40rem 時名稱與時間軸上下排，不橫向捲動。
 * 資料版面（`layoutMode="data"`）把剩餘高度交給它，刻度列固定、泳道在框內捲動，捲軸只在泳道旁。
 */
@Component({
  selector: 'nx-time-lanes',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './time-lanes.scss',
  host: {
    role: 'group',
    '[attr.aria-label]': 'label()',
    '[style.--time-now.%]': 'nowPosition()',
  },
  template: `<div class="time-axis" aria-hidden="true">
      <span class="time-heading">{{ heading() }}</span>
      <div class="time-scale">
        @for (tick of ticks(); track tick.at) {
          <span
            class="time-tick"
            [class.is-start]="tick.position < 2"
            [class.is-end]="tick.position > 98"
            [style.inset-inline-start.%]="tick.position"
            >{{ tick.label }}</span
          >
        }
        @if (nowVisible()) {
          <span class="time-now">現在 {{ nowLabel() }}</span>
        }
      </div>
    </div>
    <div class="time-body" (scroll)="hideTip()">
      <div class="time-rows">
        <div class="time-guides" aria-hidden="true">
          @for (shade of shadeBands(); track shade.left) {
            <i
              class="time-shade"
              [style.inset-inline-start.%]="shade.left"
              [style.width.%]="shade.width"
            ></i>
          }
          @for (tick of ticks(); track tick.at) {
            <i class="time-rule" [style.inset-inline-start.%]="tick.position"></i>
          }
        </div>
        <ng-content />
        <div class="time-front" aria-hidden="true">
          @if (nowVisible()) {
            <i class="time-now-line"></i>
          }
        </div>
      </div>
    </div>
    @if (tip(); as current) {
      <span
        class="time-tip"
        aria-hidden="true"
        [class.is-start]="current.position < 15"
        [class.is-end]="current.position > 85"
        [style.left.px]="current.left"
        [style.top.px]="current.top"
        >{{ current.text }}</span
      >
    }`,
})
export class TimeLanes {
  /** 區間起點（毫秒）。 */
  readonly from = input.required<number>();
  /** 區間終點（毫秒）。 */
  readonly to = input.required<number>();
  /** 整個時間軸的無障礙名稱。 */
  readonly label = input.required<string>();
  /** 名稱欄的表頭。 */
  readonly heading = input('');
  /** 刻度間隔（小時），以工作區時區對齊。 */
  readonly tickHours = input(3);
  readonly shades = input<readonly TimeShade[]>([]);

  private readonly clock = inject(ServerClock);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly now = computed(() => Math.floor(this.clock.now() / NOW_STEP) * NOW_STEP);

  readonly tip = signal<TimeTip | null>(null, {
    equal: (a, b) => a?.text === b?.text && a?.left === b?.left && a?.top === b?.top,
  });
  readonly ticks = computed(() => timeTicks(this.from(), this.to(), this.tickHours()));
  readonly nowPosition = computed(() => percent(this.now(), this.from(), this.to()));
  readonly nowVisible = computed(() => this.now() >= this.from() && this.now() <= this.to());
  readonly nowLabel = computed(() => formatTimeOfDay(this.now()));
  readonly shadeBands = computed(() =>
    this.shades().map((shade) => {
      const left = percent(shade.from, this.from(), this.to());
      return { left, width: percent(shade.to, this.from(), this.to()) - left };
    }),
  );

  constructor() {
    this.clock.watch();
  }

  /**
   * 由泳道呼叫：`position` 是記號在軌道上的百分比，`track` 用來把提示放在那一列上方。
   * 提示在捲動區外、以宿主的內框為座標，所以從軌道換算位置；絕對定位不含邊框，扣掉 client 偏移。
   */
  showTip(text: string, position: number, track: HTMLElement) {
    const host = this.host.nativeElement;
    const frame = host.getBoundingClientRect();
    const box = track.getBoundingClientRect();
    this.tip.set({
      text,
      position,
      left: box.left - frame.left - host.clientLeft + (box.width * position) / 100,
      top: box.top - frame.top - host.clientTop,
    });
  }

  /** 捲動時指標沒動、收不到 pointermove，提示會留在已不在指標下的那列，所以一併收起。 */
  hideTip() {
    this.tip.set(null);
  }
}
