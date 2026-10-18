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
import type { RepositoryFileDto } from '@core/api/schema';
import { CopyFeedback } from '@shared/browser/copy-feedback';
import { formatBytes, formatNumber } from '@shared/browser/format';
import { highlightWhenVisible } from '@shared/markdown/code-highlighting';
import { MarkdownView } from '@shared/markdown/markdown-view';
import { Icon } from '@shared/ui/icon';
import { ViewSwitch } from '@shared/ui/view-switch';
import { baseName, codeLanguage, isMarkdown } from './repository-language';

/** 超過此大小，上色的成本會大於效益；原始碼維持純文字。 */
const highlightLimit = 160_000;

/** 釘選提交的單一檔案：Markdown 以頁面呈現，其他則是帶行號的原始碼。 */
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
  /** 資料夾下的 README：不連出去，名稱即為標題。 */
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
    for (
      let index = text.indexOf('\n');
      index >= 0 && index < text.length - 1;
      index = text.indexOf('\n', index + 1)
    )
      count++;
    return count;
  });
  readonly lines = computed(() => formatNumber(this.lineCount()));
  readonly size = computed(() => formatBytes(this.file().size));
  readonly numbers = computed(() =>
    Array.from({ length: this.lineCount() }, (_, index2) => index2 + 1).join('\n'),
  );
  private clear: () => void = () => undefined;

  constructor() {
    // 語法高亮會改寫 code 元素，所以它的文字在這裡設定，而不是綁定。
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
