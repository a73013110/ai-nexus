import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import type { Message } from '../../core/api/types';
import { generationError } from '../../core/api/generation-error';
import { Icon } from '../../shared/ui/icon';
import { MarkdownView } from '../../shared/ui/markdown-view';
import { CopyFeedback } from '../../shared/browser/copy-feedback';
import { AttachmentList } from '../attachments/attachment-list';
import { MessageTree } from './message-tree';
import { ThinkingIndicator } from '../../shared/ui/thinking-indicator';
import { ReaderLink } from '../../shared/browser/reader-link';
import { MessageFeedback } from '../quality/message-feedback';
import { ChargeLabel } from '../billing/charge-label';
import { StreamingAnswer } from '../../shared/ui/streaming-answer';
import { RunTimingDisplay } from '../../shared/ui/run-timing';

@Component({
  selector: 'nx-chat-message',
  imports: [
    Icon,
    AttachmentList,
    ThinkingIndicator,
    ReaderLink,
    MarkdownView,
    MessageFeedback,
    ChargeLabel,
    StreamingAnswer,
    RunTimingDisplay,
  ],
  providers: [CopyFeedback],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: ` <article
    class="message"
    [class.user]="message().role === 'user'"
    [class.search-match]="matched()"
    [class.search-current]="currentMatch()"
    [attr.data-message-id]="message().id"
    [attr.aria-label]="message().role === 'user' ? '你的提問' : 'AI 回覆'"
    [attr.aria-busy]="active()"
    animate.enter="message-enter"
  >
    <div class="message-heading">
      <span class="role-mark" [class.nexus]="message().role === 'assistant'">{{
        message().role === 'user' ? '你' : 'N'
      }}</span
      ><strong>{{ message().role === 'user' ? '你的提問' : 'AI Nexus' }}</strong>
      @if (message().role === 'assistant') {
        <span class="assistant-label">AI 回覆</span>
      }
      @if (showModelNames() && message().modelId) {
        <span class="message-model">{{ message().modelId }}</span>
      }
    </div>
    @if (message().role === 'user') {
      <div class="user-copy">{{ message().content }}</div>
      @if (message().attachments?.length) {
        <nx-attachment-list [files]="message().attachments!" />
      }
    } @else if (active()) {
      @if (streamContent()) {
        <nx-streaming-answer [content]="streamContent()" (rendered)="rendered.emit()" />
      } @else {
        <nx-thinking-indicator
          [mode]="status() === 'queued' ? 'waiting' : 'thinking'"
          [label]="
            status() === 'queued'
              ? '等待模型回應'
              : reasoning() !== 'auto' && reasoning() !== 'none'
                ? '正在推理'
                : '正在思考'
          "
          [detail]="
            status() === 'queued' ? '已加入佇列，可隨時停止' : '正在整理資訊，回答將逐步呈現'
          "
        />
      }
    } @else {
      <nx-markdown-view [content]="message().content" animate.enter="answer-settled" />
    }
    @if (!active() && message().sources?.length) {
      <nav class="source-citations" aria-label="回答引用來源">
        @for (source of message().sources; track source.number) {
          <a
            [nxReaderLink]="source.documentId"
            [readerPage]="source.pageNumber"
            [title]="source.excerpt"
            ><strong>[{{ source.number }}]</strong><span>{{ source.title }}</span
            ><small>第 {{ source.pageNumber }} 頁</small><nx-icon name="document"
          /></a>
        }
      </nav>
    }
    @if (message().status === 'cancelled' && !active()) {
      <p class="message-note">已停止 · 保留部分回答</p>
    }
    @if (!active() && message().webSources; as sources) {
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
    @if (!active() && message().role === 'assistant') {
      <nx-run-timing [value]="message().timing" />
      <div class="message-charges">
        @if (message().charge; as charge) {
          <nx-charge-label [charge]="charge" />
        }
        @if (message().webSearchCharge; as charge) {
          <span>搜尋</span><nx-charge-label [charge]="charge" />
        }
      </div>
    }
    @if (message().status === 'failed' && !active()) {
      <p class="message-note error-note">{{ failureText() }}，可重新生成。</p>
    }
    @if (!active()) {
      <div class="message-actions">
        <button
          class="quiet-button"
          (click)="copyAnswer()"
          [attr.aria-label]="copied() ? '已複製' : '複製訊息'"
        >
          <nx-icon [name]="copied() ? 'check' : 'copy'" /><span>{{
            copied() ? '已複製' : '複製'
          }}</span>
        </button>
        @if (message().role === 'user') {
          <button
            class="quiet-button"
            (click)="edit.emit(message())"
            [disabled]="busy()"
            aria-label="編輯提問"
          >
            <nx-icon name="edit" /><span>編輯</span>
          </button>
        } @else {
          @if (allowArtifacts()) {
            <button
              class="quiet-button"
              (click)="saveArtifact.emit(message())"
              [disabled]="busy()"
              aria-label="儲存回答為成果文件"
            >
              <nx-icon name="document" /><span>儲存成果</span>
            </button>
          }
          <button
            class="quiet-button"
            (click)="regenerate.emit(message())"
            [disabled]="busy()"
            aria-label="重新生成"
          >
            <nx-icon name="repeat" /><span>重新生成</span>
          </button>
        }
        @if (versions().length > 1) {
          <div class="version-picker">
            <button
              class="icon-button"
              aria-label="上一個版本"
              (click)="version.emit(-1)"
              [disabled]="versionIndex() === 0 || busy()"
            >
              <nx-icon name="left" /></button
            ><span aria-label="目前版本">{{ versionIndex() + 1 }} / {{ versions().length }}</span
            ><button
              class="icon-button"
              aria-label="下一個版本"
              (click)="version.emit(1)"
              [disabled]="versionIndex() === versions().length - 1 || busy()"
            >
              <nx-icon name="chevron" />
            </button>
          </div>
        }
      </div>
    }
    @if (copyError()) {
      <p role="status" class="message-note">{{ copyError() }}</p>
    }
    @if (!active() && message().role === 'assistant' && message().content) {
      <nx-message-feedback [message]="message()" (rated)="rated.emit($event)" />
    }
  </article>`,
})
export class ChatMessage {
  readonly message = input.required<Message>();
  readonly tree = input.required<MessageTree>();
  readonly matched = input(false);
  readonly currentMatch = input(false);
  readonly active = input(false);
  readonly showModelNames = input(true);
  readonly allowArtifacts = input(false);
  readonly saveArtifact = output<Message>();
  readonly rated = output<{ id: string; rating: number }>();
  readonly busy = input(false);
  readonly streamContent = input('');
  readonly status = input('');
  readonly reasoning = input('auto');
  readonly rendered = output<void>();
  readonly edit = output<Message>();
  readonly regenerate = output<Message>();
  readonly version = output<number>();
  readonly feedback = inject(CopyFeedback);
  readonly copied = this.feedback.copied;
  readonly copyError = this.feedback.error;
  readonly versions = computed(() => this.tree().versions(this.message()));
  readonly versionIndex = computed(() =>
    this.versions().findIndex((x) => x.id === this.message().id),
  );
  readonly failureText = computed(() => generationError(this.message().errorCode));
  async copyAnswer() {
    await this.feedback.copy(this.message().content);
  }
}
