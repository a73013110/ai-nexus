import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  effect,
  inject,
  input,
  output,
  signal,
  untracked,
} from '@angular/core';
import { ThemeService } from '../../core/preferences/theme-service';
import { MarkdownView } from './markdown-view';
import { StreamingMarkdown } from './streaming-markdown';

/** Presentation smoothing only. The stream/store remains the authoritative full text. */
@Component({
  selector: 'nx-streaming-answer',
  imports: [MarkdownView],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'streaming-answer' },
  template: `<div class="streaming-copy" aria-live="off">
    @for (block of blocks(); track block.id) {
      <nx-markdown-view class="stream-committed" [content]="block.content" />
    }
    <nx-markdown-view class="stream-frontier" [content]="tail()" [streaming]="true" />
    <span class="stream-cursor" aria-hidden="true"></span>
  </div>`,
})
export class StreamingAnswer {
  readonly content = input.required<string>();
  readonly rendered = output<void>();
  readonly shown = signal('');
  readonly blocks = signal<{ id: number; content: string }[]>([]);
  readonly tail = signal('');
  private readonly markdown = new StreamingMarkdown();
  private readonly themes = inject(ThemeService);
  private frame = 0;
  private lastFrame = 0;
  private target = '';
  constructor() {
    effect(() => {
      const target = this.content();
      this.target = target;
      const shown = untracked(() => this.shown());
      if (this.themes.reducedMotion() || !target.startsWith(shown)) {
        cancelAnimationFrame(this.frame);
        this.frame = 0;
        this.publish(target);
      } else if (!this.frame && target !== shown) {
        this.lastFrame = performance.now();
        this.frame = requestAnimationFrame((time) => this.advance(time));
      }
    });
    inject(DestroyRef).onDestroy(() => cancelAnimationFrame(this.frame));
  }
  private advance(time: number) {
    this.frame = 0;
    const remaining = this.target.length - this.shown().length;
    // Cap Markdown work near 20 fps; absorb provider bursts with a short adaptive buffer.
    if (time - this.lastFrame >= 48 && remaining > 0) {
      const elapsed = Math.min(120, time - this.lastFrame);
      this.lastFrame = time;
      const end = this.boundary(
        Math.min(
          this.target.length,
          this.shown().length + Math.max(2, Math.ceil(remaining * (1 - Math.exp(-elapsed / 140)))),
        ),
      );
      this.publish(this.target.slice(0, end));
    }
    if (this.shown() !== this.target)
      this.frame = requestAnimationFrame((next) => this.advance(next));
  }
  private publish(value: string) {
    this.shown.set(value);
    const parsed = this.markdown.update(value);
    this.blocks.set(parsed.blocks);
    this.tail.set(parsed.tail);
    this.rendered.emit();
  }
  private boundary(end: number) {
    // Never display half a surrogate pair (emoji/non-BMP text).
    const value = this.target || this.shown();
    return end > 0 && /[\uD800-\uDBFF]/.test(value[end - 1]) ? end - 1 : end;
  }
}
