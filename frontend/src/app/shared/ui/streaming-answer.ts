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
import { CopyFeedback } from '../browser/copy-feedback';
import { Notice } from './notice';
import { StreamingMarkdown, type StreamingBlock, type StreamingFence } from './streaming-markdown';

/** Presentation smoothing only. The stream/store remains the authoritative full text. */
@Component({
  selector: 'nx-streaming-answer',
  imports: [MarkdownView, Notice],
  providers: [CopyFeedback],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'streaming-answer' },
  template: `<div class="streaming-copy" aria-live="off">
    @for (block of blocks(); track block.id) {
      <nx-markdown-view
        class="stream-committed"
        [class.stream-continues]="block.continues"
        [content]="block.content"
      />
    }
    @if (fence(); as fence) {
      <div class="markdown stream-frontier">
        <div class="code-block">
          <div class="code-toolbar">
            <span>{{ fence.language }}</span
            ><button
              type="button"
              class="code-copy"
              title="複製程式碼"
              aria-label="複製程式碼"
              (click)="copyFence($event)"
            >
              複製程式碼
            </button>
          </div>
          <pre><code>@for (chunk of fence.chunks; track chunk.id) {<span>{{ chunk.text }}</span>}{{ fence.tail }}</code></pre>
        </div>
      </div>
    } @else {
      <nx-markdown-view class="stream-frontier" [content]="tail()" [streaming]="true" />
    }
    <span class="stream-cursor" aria-hidden="true"></span>
    @if (copy.error()) {
      <nx-notice [message]="copy.error()" />
    }
  </div>`,
})
export class StreamingAnswer {
  readonly content = input.required<string>();
  readonly rendered = output<void>();
  readonly shown = signal('');
  readonly blocks = signal<StreamingBlock[]>([]);
  readonly tail = signal('');
  readonly fence = signal<StreamingFence | null>(null);
  readonly copy = inject(CopyFeedback);
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
    this.fence.set(parsed.fence);
    this.rendered.emit();
  }
  copyFence(event: MouseEvent) {
    const fence = this.fence();
    if (fence)
      void this.copy.copy(
        fence.chunks.map((chunk) => chunk.text).join('') + fence.tail,
        event.currentTarget as Element,
      );
  }
  private boundary(end: number) {
    // Never display half a surrogate pair (emoji/non-BMP text).
    const value = this.target || this.shown();
    return end > 0 && /[\uD800-\uDBFF]/.test(value[end - 1]) ? end - 1 : end;
  }
}
