import { Notice } from '../ui/notice';
import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { CopyFeedback } from '../browser/copy-feedback';
import { Icon } from '../ui/icon';

/** 唯讀的長文字（JSON、堆疊、範本）加上標題與複製鈕；外觀與 Markdown 的程式碼區塊一致。 */
@Component({
  selector: 'nx-code-block',
  imports: [Notice, Icon],
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
  `,
  template: `<header class="embed-bar">
      <span class="embed-label">{{ label() }}</span>
      <span class="embed-actions"
        ><button
          type="button"
          class="icon-button"
          [attr.aria-label]="'複製' + label()"
          [title]="'複製' + label()"
          (click)="feedback.copy(value())"
        >
          <nx-icon [name]="feedback.copied() ? 'check' : 'copy'" /></button
      ></span>
      <span class="sr-only" role="status">{{ feedback.copied() ? label() + '已複製' : '' }}</span>
    </header>
    <pre class="embed-body"><code>{{ value() }}</code></pre>
    @if (feedback.error()) {
      <nx-notice tone="danger" [message]="feedback.error()" />
    }`,
})
export class CodeBlock {
  readonly label = input.required<string>();
  readonly value = input.required<string>();
  readonly feedback = inject(CopyFeedback);
}
