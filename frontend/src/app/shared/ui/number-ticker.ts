import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  effect,
  inject,
  input,
  signal,
  untracked,
} from '@angular/core';
import { ThemeService } from '@core/preferences/theme-service';
import { formatNumber } from '../browser/format';

const DURATION = 900;

/**
 * 數字從前一個值滾動到新值，結束時緩和減速，像計數器慢慢停下。
 * 螢幕閱讀器與減少動態效果會立即顯示最終值；未知的值會顯示 `unknown`，而不是零。
 */
@Component({
  selector: 'nx-number-ticker',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'number-ticker' },
  styles: `
    :host {
      font-variant-numeric: tabular-nums;
      white-space: nowrap;
    }
  `,
  template: `<span aria-hidden="true">{{ text() }}</span
    ><span class="sr-only">{{ final() }}</span>`,
})
export class NumberTicker {
  readonly value = input<number | null | undefined>(null);
  /** 把中間值與最終值轉成文字；預設為分組數字。 */
  readonly format = input<(value: number) => string>(formatNumber);
  /** 值未知時顯示；絕不渲染為零。 */
  readonly unknown = input('—');
  private readonly theme = inject(ThemeService);
  readonly text = signal('');
  readonly final = signal('');
  private shown = 0;
  private frame = 0;

  constructor() {
    inject(DestroyRef).onDestroy(() => cancelAnimationFrame(this.frame));
    effect(() => {
      const value = this.value(),
        format = this.format(),
        reduced = this.theme.reducedMotion();
      untracked(() => this.run(value, format, reduced));
    });
  }

  private run(
    value: number | null | undefined,
    format: (value: number) => string,
    reduced: boolean,
  ) {
    cancelAnimationFrame(this.frame);
    if (value === null || value === undefined || !Number.isFinite(value)) {
      this.text.set(this.unknown());
      this.final.set(this.unknown());
      return;
    }
    this.final.set(format(value));
    const from = this.shown;
    if (reduced || from === value || typeof requestAnimationFrame === 'undefined') {
      this.shown = value;
      this.text.set(format(value));
      return;
    }
    const start = performance.now();
    // 滾動時整數保持為整數，讓計數不會出現小數。
    const whole = Number.isInteger(value) && Number.isInteger(from);
    const step = (now: number) => {
      const time = Math.min(1, (now - start) / DURATION);
      const eased = time === 1 ? 1 : 1 - Math.pow(2, -10 * time);
      const current = from + (value - from) * eased;
      this.shown = time === 1 ? value : whole ? Math.round(current) : current;
      this.text.set(format(this.shown));
      if (time < 1) this.frame = requestAnimationFrame(step);
    };
    this.frame = requestAnimationFrame(step);
  }
}
