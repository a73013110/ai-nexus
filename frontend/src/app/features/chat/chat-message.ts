import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import type { MessageDto } from '../../core/api/schema';
import { Icon } from '../../shared/ui/icon';
import { Notice } from '../../shared/ui/notice';
import { CopyFeedback } from '../../shared/browser/copy-feedback';
import { MessageContent } from '../workspace/message-content';
import { MessageTree } from './message-tree';
import { GenerationIndicator } from '../../shared/ui/generation-indicator';
import { generationStatus } from '../../core/api/generation-status';
import { MessageFeedback } from '../quality/message-feedback';
import { ChargeLabel } from '../billing/charge-label';
import { StreamingAnswer } from '../../shared/markdown/streaming-answer';
import { formatModelDisplayName } from '../../shared/browser/format';

@Component({
  selector: 'nx-chat-message',
  imports: [
    Icon,
    Notice,
    MessageContent,
    GenerationIndicator,
    MessageFeedback,
    ChargeLabel,
    StreamingAnswer,
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
        <span class="message-model">{{ modelName(message()) }}</span>
      }
    </div>
    @if (active() && message().role === 'assistant') {
      @if (streamContent()) {
        <nx-streaming-answer [content]="streamContent()" (rendered)="rendered.emit()" />
      } @else if (generation(); as current) {
        <nx-generation-indicator
          [waiting]="current.phase === 'queued'"
          [label]="current.label"
          [detail]="current.detail"
        />
      }
    } @else {
      <nx-message-content [message]="message()" />
      @if (message().role === 'assistant') {
        <div class="message-charges">
          @if (message().charge; as charge) {
            <nx-charge-label [charge]="charge" />
          }
          @if (message().webSearchCharge; as charge) {
            <span>搜尋</span><nx-charge-label [charge]="charge" />
          }
        </div>
      }
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
      <nx-notice [message]="copyError()" />
    }
    @if (!active() && message().role === 'assistant' && message().content) {
      <nx-message-feedback [message]="message()" (rated)="rated.emit($event)" />
    }
  </article>`,
})
export class ChatMessage {
  readonly modelName = formatModelDisplayName;
  readonly message = input.required<MessageDto>();
  readonly tree = input.required<MessageTree>();
  readonly matched = input(false);
  readonly currentMatch = input(false);
  readonly active = input(false);
  readonly showModelNames = input(true);
  readonly allowArtifacts = input(false);
  readonly saveArtifact = output<MessageDto>();
  readonly rated = output<{ id: string; rating: number }>();
  readonly busy = input(false);
  readonly streamContent = input('');
  readonly status = input('');
  readonly generation = computed(() => generationStatus(this.status(), !!this.streamContent()));
  readonly rendered = output<void>();
  readonly edit = output<MessageDto>();
  readonly regenerate = output<MessageDto>();
  readonly version = output<number>();
  readonly feedback = inject(CopyFeedback);
  readonly copied = this.feedback.copied;
  readonly copyError = this.feedback.error;
  readonly versions = computed(() => this.tree().versions(this.message()));
  readonly versionIndex = computed(() =>
    this.versions().findIndex((x) => x.id === this.message().id),
  );
  async copyAnswer() {
    await this.feedback.copy(this.message().content);
  }
}
