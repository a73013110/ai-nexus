import {
  booleanAttribute,
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  output,
} from '@angular/core';
import { arrivals } from '../browser/arrivals';
import { ServerClock } from '../browser/server-clock';

const MINUTE = 60_000;
const HOUR = 3_600_000;
const DAY = 86_400_000;

/** 倒數或經過的毫秒數轉成時鐘字樣：一小時內 `4:05`、一天內 `2:04:05`；超過一天 `2 天 3 小時`，不再逐秒跳。 */
export function clockText(milliseconds: number) {
  const total = Math.max(0, Math.floor(milliseconds / 1000));
  if (milliseconds >= DAY) {
    const days = Math.floor(milliseconds / DAY);
    const hours = Math.floor((milliseconds % DAY) / HOUR);
    return hours ? `${days} 天 ${hours} 小時` : `${days} 天`;
  }
  const hours = Math.floor(total / 3600);
  const minutes = Math.floor((total % 3600) / 60);
  const seconds = String(total % 60).padStart(2, '0');
  return hours
    ? `${hours}:${String(minutes).padStart(2, '0')}:${seconds}`
    : `${minutes}:${seconds}`;
}

/**
 * 擠在小地方（圓環中心）的時鐘字樣，最多五個字：一小時內同 {@link clockText}（`4:05`），一天內 `2h04`，超過一天 `3d`。
 */
export function compactClockText(milliseconds: number) {
  if (milliseconds < HOUR) return clockText(milliseconds);
  if (milliseconds < DAY) {
    const minutes = Math.floor((milliseconds % HOUR) / MINUTE);
    return `${Math.floor(milliseconds / HOUR)}h${String(minutes).padStart(2, '0')}`;
  }
  return `${Math.floor(milliseconds / DAY)}d`;
}

/** 給螢幕閱讀器的概略時間（「約 5 分鐘」），不逐秒變化，免得讀屏一直重念。 */
export function spokenDuration(milliseconds: number) {
  if (milliseconds < MINUTE) return '不到 1 分鐘';
  if (milliseconds < HOUR) return `約 ${Math.round(milliseconds / MINUTE)} 分鐘`;
  if (milliseconds < DAY) return `約 ${Math.round(milliseconds / HOUR)} 小時`;
  return `約 ${Math.round(milliseconds / DAY)} 天`;
}

/**
 * 逐秒跳動的倒數（給 `to`）或經過時間（給 `from`），讀共用的 `ServerClock`。
 * 每個字元以位置＋字元追蹤，只有變了的那一位重建並播一次滾入動效：倒數由上往下、經過時間由下往上，像翻牌計時器；
 * 減少動態時直接換字。倒數到零時發出 `reached` 一次，停在 `0:00`。
 */
@Component({
  selector: 'nx-ticking-time',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'ticking-time', '[attr.data-direction]': 'direction()' },
  styles: `
    :host {
      display: inline-flex;
      font-variant-numeric: tabular-nums;
      white-space: nowrap;
      overflow: hidden;
      vertical-align: bottom;
    }
    .glyphs {
      display: inline-flex;
    }
    .glyph {
      display: inline-block;
    }
    /* 第一次顯示的字不播（整串一起滾入像載入動畫），之後變了的那一位才播。 */
    .glyph.is-new {
      animation: digit-roll var(--motion-panel) var(--ease-spring);
    }
    :host([data-direction='up']) .glyph {
      --digit-from: 0.6em;
    }
  `,
  template: `<span class="glyphs" aria-hidden="true">
      @for (glyph of glyphs(); track glyph.key) {
        <span class="glyph" [class.is-new]="glyph.fresh">{{ glyph.char }}</span>
      }
    </span>
    <span class="sr-only">{{ spoken() }}</span>`,
})
export class TickingTime {
  /** 倒數的目標時間（ISO）。與 `from` 擇一；都沒給時顯示 `unknown`。 */
  readonly to = input<string | null>(null);
  /** 經過時間的起點（ISO），例如執行開始的時間。 */
  readonly from = input<string | null>(null);
  readonly unknown = input('—');
  /** 用 {@link compactClockText} 的短字樣，放在圓環中心這類只容得下幾個字的地方。 */
  readonly compact = input(false, { transform: booleanAttribute });
  /** 倒數到零。 */
  readonly reached = output<void>();

  private readonly clock = inject(ServerClock);
  private readonly arrived = arrivals();
  readonly direction = computed(() => (this.to() ? 'down' : 'up'));
  private readonly milliseconds = computed(() => {
    const now = this.clock.now();
    const to = this.to(),
      from = this.from();
    if (to) return Math.max(0, Date.parse(to) - now);
    if (from) return Math.max(0, now - Date.parse(from));
    return null;
  });
  readonly text = computed(() => {
    const value = this.milliseconds();
    return value === null || Number.isNaN(value)
      ? this.unknown()
      : this.compact()
        ? compactClockText(value)
        : clockText(value);
  });
  readonly glyphs = computed(() => {
    const chars = [...this.text()];
    // 以「從右數第幾位＋字元」追蹤：只有變了的那一位重建；9:59 → 10:00 多一位時，右邊的位數不受影響。
    const keys = chars.map((char, index) => `${chars.length - index}:${char}`);
    const fresh = this.arrived(keys);
    return chars.map((char, index) => ({ char, key: keys[index], fresh: fresh(keys[index]) }));
  });
  readonly spoken = computed(() => {
    const value = this.milliseconds();
    if (value === null || Number.isNaN(value)) return this.unknown();
    return this.to() ? `剩${spokenDuration(value)}` : `已經過${spokenDuration(value)}`;
  });

  constructor() {
    this.clock.watch();
    let fired = false;
    effect(() => {
      const value = this.milliseconds();
      if (this.to() && value === 0) {
        if (!fired) {
          fired = true;
          this.reached.emit();
        }
      } else fired = false;
    });
  }
}
