import { Notice } from '../ui/notice';
import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { CopyFeedback } from '../browser/copy-feedback';
import { Icon } from '../ui/icon';
import { HighlightedCode } from './highlighted-code';

/**
 * 唯讀的長文字（JSON、堆疊、範本、原始碼）加上標題與複製鈕；外觀與 Markdown 的程式碼區塊一致。
 * 給 `language` 時語法上色、保留原本的換行並可橫向捲動。
 */
@Component({
  selector: 'nx-code-block',
  imports: [HighlightedCode, Notice, Icon],
  providers: [CopyFeedback],
  changeDetection: ChangeDetectionStrategy.OnPush,
  // 外框與標頭是共用的內嵌區塊（styles/embeds.scss）；這裡只有純文字內容的排版。
  host: { class: 'embed' },
  styles: `
    :host {
      display: block;
      margin: 0;
    }
    pre {
      font: var(--p-font-data) / 1.65 var(--font-code);
      white-space: pre-wrap;
      overflow-wrap: anywhere;
      tab-size: 2;
    }
    /* 原始碼換行會改變縮排的意思：保留原樣，太寬時在區塊內橫向捲動。 */
    pre.is-source {
      white-space: pre;
      overflow-wrap: normal;
      overflow-x: auto;
      tab-size: 4;
    }
  `,
  template: `<header class="embed-bar">
      <span class="embed-label">{{ label() }}</span>
      <span class="embed-actions">
        <button
          type="button"
          class="icon-button"
          [attr.aria-label]="'複製' + label()"
          [title]="'複製' + label()"
          (click)="feedback.copy(value())"
        >
          <nx-icon [name]="feedback.copied() ? 'check' : 'copy'" />
        </button>
      </span>
      <span class="sr-only" role="status">{{ feedback.copied() ? label() + '已複製' : '' }}</span>
    </header>
    @if (language()) {
      <pre
        class="embed-body is-source"
        tabindex="0"
        [attr.aria-label]="label()"
      ><code [nxHighlightedCode]="value()" [language]="language()"></code></pre>
    } @else {
      <pre class="embed-body"><code>{{ value() }}</code></pre>
    }
    @if (feedback.error()) {
      <nx-notice tone="danger" [message]="feedback.error()" />
    }`,
})
export class CodeBlock {
  readonly label = input.required<string>();
  readonly value = input.required<string>();
  /** highlight.js 的語言名稱或別名；不給時是會自動換行的純文字。 */
  readonly language = input<string | null>(null);
  readonly feedback = inject(CopyFeedback);
}
