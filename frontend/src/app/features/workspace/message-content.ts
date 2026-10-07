import { ChangeDetectionStrategy, Component, input, computed } from '@angular/core';
import type { Message } from '../../core/api/types';
import { MarkdownView } from '../../shared/ui/markdown-view';
import { Icon } from '../../shared/ui/icon';
import { RunTimingDisplay } from '../../shared/ui/run-timing';
import { ReaderLink } from '../../shared/browser/reader-link';
import { AttachmentList } from '../attachments/attachment-list';
import { systemProblem } from '../../core/api/safe-errors';
import { IssueCode } from '../../shared/ui/issue-code';

export type MessageDisplay = Pick<
  Message,
  'role' | 'content' | 'status' | 'attachments' | 'sources' | 'webSources' | 'timing' | 'errorCode'
> & { issueCode?: string | null };
/** The same settled answer, citations and attachments in chat and read-only snapshots. */
@Component({
  selector: 'nx-message-content',
  imports: [IssueCode, MarkdownView, Icon, RunTimingDisplay, ReaderLink, AttachmentList],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `@if (message().role === 'user') {
      <div class="user-copy">{{ message().content }}</div>
    } @else {
      <nx-markdown-view [content]="message().content" />
    }
    @if (message().attachments?.length) {
      <nx-attachment-list [files]="message().attachments!" [shareId]="shareId()" />
    }
    @if (message().sources?.length) {
      <nav class="source-citations" aria-label="回答引用來源">
        @for (source of message().sources; track source.number) {
          @if (!shareId()) {
            <a
              [nxReaderLink]="source.documentId"
              [readerPage]="source.pageNumber"
              [title]="source.excerpt"
              ><strong>[{{ source.number }}]</strong><span>{{ source.title }}</span
              ><small
                >第 {{ source.pageNumber
                }}{{ source.endPage > source.pageNumber ? '–' + source.endPage : '' }} 頁</small
              ><nx-icon name="document"
            /></a>
          } @else {
            <details class="shared-citation">
              <summary>
                [{{ source.number }}] {{ source.title }} · 第 {{ source.pageNumber
                }}{{ source.endPage > source.pageNumber ? '–' + source.endPage : '' }} 頁
              </summary>
              <p>{{ source.excerpt }}</p>
            </details>
          }
        }
      </nav>
    }
    @if (message().webSources; as sources) {
      @if (sources.length) {
        <nav class="source-citations web-citations" aria-label="網路搜尋來源">
          @for (source of sources; track source.number) {
            <a
              [href]="source.url"
              target="_blank"
              rel="noopener noreferrer"
              [title]="source.excerpt + ' · ' + source.retrievedAt"
              ><strong>[網路{{ source.number }}]</strong><span>{{ source.title }}</span
              ><nx-icon name="globe"
            /></a>
          }
        </nav>
      } @else {
        <p class="message-note">這次網路搜尋沒有可引用的摘要。</p>
      }
    }
    @if (message().status === 'cancelled') {
      <p class="message-note">已停止 · 保留部分回答</p>
    }
    @if (message().status === 'failed') {
      <p class="message-note error-note">{{ failure() }}<nx-issue-code [message]="failure()" /></p>
    }
    @if (message().role === 'assistant') {
      <nx-run-timing [value]="message().timing" />
    }`,
})
export class MessageContent {
  readonly message = input.required<MessageDisplay>();
  readonly shareId = input<string | null>(null);
  readonly failure = computed(() => systemProblem(this.message().issueCode));
}
