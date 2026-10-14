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
 * A number that rolls from its previous value to the new one, easing out like a counter
 * settling. Screen readers and reduced motion get the final value at once; an unknown value
 * shows `unknown` instead of zero.
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
  /** Turns the in-between and final numbers into text; defaults to grouped digits. */
  readonly format = input<(value: number) => string>(formatNumber);
  /** Shown when the value is unknown; never rendered as zero. */
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
    // Integers stay integers while rolling so counts never show fractions.
    const whole = Number.isInteger(value) && Number.isInteger(from);
    const step = (now: number) => {
      const t = Math.min(1, (now - start) / DURATION);
      const eased = t === 1 ? 1 : 1 - Math.pow(2, -10 * t);
      const current = from + (value - from) * eased;
      this.shown = t === 1 ? value : whole ? Math.round(current) : current;
      this.text.set(format(this.shown));
      if (t < 1) this.frame = requestAnimationFrame(step);
    };
    this.frame = requestAnimationFrame(step);
  }
}
