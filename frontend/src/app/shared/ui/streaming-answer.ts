import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { ThemeService } from '../../core/preferences/theme-service';

/** Presentation smoothing only. The stream/store remains the authoritative full text. */
@Component({
  selector: 'nx-streaming-answer',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'streaming-answer' },
  template: `<div class="streaming-copy" aria-live="off">
    <span>{{ settled() }}</span
    ><span class="stream-frontier">{{ frontier() }}</span>
  </div>`,
})
export class StreamingAnswer {
  readonly content = input.required<string>();
  readonly rendered = output<void>();
  readonly shown = signal('');
  readonly settled = computed(() =>
    this.shown().slice(0, this.boundary(Math.max(0, this.shown().length - 32))),
  );
  readonly frontier = computed(() => this.shown().slice(this.settled().length));
  private readonly themes = inject(ThemeService);
  private frame = 0;
  private lastFrame = 0;
  private target = '';
  constructor() {
    effect(() => {
      const target = this.content();
      this.target = target;
      if (this.themes.reducedMotion() || !target.startsWith(this.shown())) {
        cancelAnimationFrame(this.frame);
        this.frame = 0;
        this.shown.set(target);
        this.rendered.emit();
      } else if (!this.frame && target !== this.shown())
        this.frame = requestAnimationFrame((time) => this.advance(time));
    });
    inject(DestroyRef).onDestroy(() => cancelAnimationFrame(this.frame));
  }
  private advance(time: number) {
    this.frame = 0;
    const remaining = this.target.length - this.shown().length;
    // About 30 updates/second, with larger catch-up steps for bursty providers.
    if (time - this.lastFrame >= 30 && remaining > 0) {
      this.lastFrame = time;
      const end = this.boundary(
        Math.min(this.target.length, this.shown().length + Math.max(4, Math.ceil(remaining / 3))),
      );
      this.shown.set(this.target.slice(0, end));
      this.rendered.emit();
    }
    if (this.shown() !== this.target)
      this.frame = requestAnimationFrame((next) => this.advance(next));
  }
  private boundary(end: number) {
    // Never display half a surrogate pair (emoji/non-BMP text).
    const value = this.target || this.shown();
    return end > 0 && /[\uD800-\uDBFF]/.test(value[end - 1]) ? end - 1 : end;
  }
}
