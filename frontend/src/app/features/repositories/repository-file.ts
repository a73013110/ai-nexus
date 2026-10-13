import {
  afterRenderEffect,
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  ElementRef,
  inject,
  input,
  linkedSignal,
  viewChild,
} from '@angular/core';
import type { RepositoryFileDto } from '../../core/api/schema';
import { CopyFeedback } from '../../shared/browser/copy-feedback';
import { formatBytes, formatNumber } from '../../shared/browser/format';
import { highlightWhenVisible } from '../../shared/markdown/code-highlighting';
import { MarkdownView } from '../../shared/markdown/markdown-view';
import { Icon } from '../../shared/ui/icon';
import { ViewSwitch } from '../../shared/ui/view-switch';
import { baseName, codeLanguage, isMarkdown } from './repository-language';

/** Past this, colour would cost more than it helps; the source stays plain text. */
const highlightLimit = 160_000;

/** One file at a fixed commit: Markdown as a page, everything else as numbered source. */
@Component({
  selector: 'nx-repository-file',
  imports: [Icon, MarkdownView, ViewSwitch],
  providers: [CopyFeedback],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './repository-file.scss',
  host: { '[class.compact]': 'compact()' },
  template: `<header class="head">
      <nx-icon [name]="markdown() ? 'readme' : 'file-code'" />
      <strong class="name">{{ name() }}</strong>
      <span class="meta">{{ lines() }} 行 · {{ size() }}</span>
      <span class="tools">
        @if (markdown()) {
          <nx-view-switch
            label="顯示方式"
            appearance="segment"
            [options]="modes"
            [value]="mode()"
            (valueChange)="mode.set($event)"
          />
        }
        <ng-content select="[file-actions]" />
        <button
          type="button"
          class="icon-button"
          [attr.aria-label]="copy.copied() ? '已複製內容' : '複製內容'"
          [title]="copy.copied() ? '已複製' : '複製內容'"
          (click)="copy.copy(file().text, $any($event.currentTarget))"
        >
          <nx-icon [name]="copy.copied() ? 'check' : 'copy'" />
        </button>
        @if (!compact()) {
          <a
            class="icon-button"
            [href]="file().url"
            target="_blank"
            rel="noopener noreferrer"
            aria-label="在 Gitea 開啟這個檔案"
            title="在 Gitea 開啟"
            ><nx-icon name="external"
          /></a>
        }
      </span>
    </header>
    @if (mode() === 'preview') {
      <div class="page"><nx-markdown-view [content]="file().text" /></div>
    } @else if (!file().text) {
      <p class="blank">這個檔案是空的。</p>
    } @else {
      <div class="source" tabindex="0" role="region" [attr.aria-label]="name() + ' 原始碼'">
        <pre class="gutter" aria-hidden="true">{{ numbers() }}</pre>
        <pre class="code"><code #code></code></pre>
      </div>
    }`,
})
export class RepositoryFile {
  readonly file = input.required<RepositoryFileDto>();
  /** A README under a folder: no link out, the name is the heading. */
  readonly compact = input(false);
  readonly copy = inject(CopyFeedback);
  private readonly code = viewChild<ElementRef<HTMLElement>>('code');
  readonly modes = [
    { value: 'preview', label: '預覽' },
    { value: 'source', label: '原始碼' },
  ];
  readonly name = computed(() => baseName(this.file().path));
  readonly markdown = computed(() => isMarkdown(this.file().path));
  readonly mode = linkedSignal<string>(() => (this.markdown() ? 'preview' : 'source'));
  private readonly lineCount = computed(() => {
    const text = this.file().text;
    if (!text) return 0;
    let count = 1;
    for (let i = text.indexOf('\n'); i >= 0 && i < text.length - 1; i = text.indexOf('\n', i + 1))
      count++;
    return count;
  });
  readonly lines = computed(() => formatNumber(this.lineCount()));
  readonly size = computed(() => formatBytes(this.file().size));
  readonly numbers = computed(() =>
    Array.from({ length: this.lineCount() }, (_, i) => i + 1).join('\n'),
  );
  private clear: () => void = () => undefined;

  constructor() {
    // Highlighting rewrites the code element, so its text is set here rather than bound.
    afterRenderEffect(() => {
      const code = this.code()?.nativeElement;
      const file = this.file();
      this.clear();
      this.clear = () => undefined;
      if (!code) return;
      code.textContent = file.text.endsWith('\n') ? file.text.slice(0, -1) : file.text;
      const language = codeLanguage(file.path);
      if (!language || file.text.length > highlightLimit) {
        code.removeAttribute('data-language');
        return;
      }
      code.dataset['language'] = language;
      this.clear = highlightWhenVisible(code.parentElement!);
    });
    inject(DestroyRef).onDestroy(() => this.clear());
  }
}
