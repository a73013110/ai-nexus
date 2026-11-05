import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  linkedSignal,
  output,
  signal,
  type Signal,
} from '@angular/core';
import type { MessageDto } from '@core/api/schema';
import { AiMark } from '@shared/ui/ai-mark';
import { Icon } from '@shared/ui/icon';
import { Notice } from '@shared/ui/notice';
import { CopyFeedback } from '@shared/browser/copy-feedback';
import { MessageContent } from '@features/work/conversations/message-content';
import { MessageTree } from '@features/work/conversations/message-tree';
import type { FileScope } from '@shared/browser/reader-overlay';
import { GenerationIndicator } from '@shared/ui/generation-indicator';
import { generationStatus } from '@core/api/generation-status';
import { MessageFeedback } from '@features/admin/quality/message-feedback';
import { ChargeLabel } from '@features/admin/billing/charge-label';
import { StreamingAnswer } from '@shared/markdown/streaming-answer';
import { RunTimingDisplay } from '@features/work/conversations/run-timing';
import { formatModelDisplayName } from '@shared/browser/format';

const idle: Signal<string> = signal('').asReadonly();

@Component({
  selector: 'nx-chat-message',
  imports: [
    AiMark,
    Icon,
    Notice,
    MessageContent,
    GenerationIndicator,
    MessageFeedback,
    ChargeLabel,
    StreamingAnswer,
    RunTimingDisplay,
  ],
  providers: [CopyFeedback],
  host: { '[attr.data-role]': 'message().role' },
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './chat-message.scss',
  template: `<article
    class="message"
    [class.user]="message().role === 'user'"
    [class.is-latest]="latest()"
    [class.search-match]="matched()"
    [class.search-current]="currentMatch()"
    [attr.data-message-id]="message().id"
    [attr.aria-label]="message().role === 'user' ? '你的提問' : 'AI 回覆'"
    [attr.aria-busy]="active() || null"
    [animate.enter]="enter() ? 'message-enter' : ''"
  >
    <!-- 提問的泡泡已經表明是誰；只有回答標出作者與模型。 -->
    @if (message().role === 'assistant') {
      <header class="message-heading">
        <span class="role-mark"><nx-ai-mark /></span><strong>AI Nexus</strong>
        @if (showModelNames() && message().modelId) {
          <span class="message-model">{{ modelName(message()) }}</span>
        }
      </header>
    }
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
      <nx-message-content [message]="message()" [scope]="fileScope()" [showTiming]="false" />
      <!-- 回答剛完成或提問剛送出時操作列依序浮現；開啟對話時已完成的訊息直接顯示。 -->
      <footer class="message-footer" [class.footer-enter]="enter() || settled()">
        <div class="message-actions" role="group" aria-label="訊息操作">
          <button
            class="icon-button"
            (click)="copyAnswer()"
            [attr.aria-label]="copied() ? '已複製' : '複製訊息'"
            [title]="copied() ? '已複製' : '複製'"
          >
            <nx-icon [name]="copied() ? 'check' : 'copy'" [class.is-confirmed]="copied()" />
          </button>
          <!-- 唯讀檢視只能複製與切換版本，不能改寫別人的對話。 -->
          @if (!readOnly()) {
            @if (message().role === 'user') {
              <button
                class="icon-button"
                (click)="edit.emit(message())"
                [disabled]="busy()"
                aria-label="編輯提問"
                title="編輯"
              >
                <nx-icon name="edit" />
              </button>
            } @else {
              <button
                class="icon-button"
                (click)="regenerate.emit(message())"
                [disabled]="busy()"
                aria-label="重新生成"
                title="重新生成"
              >
                <nx-icon name="repeat" />
              </button>
              @if (allowArtifacts()) {
                <button
                  class="icon-button"
                  (click)="saveArtifact.emit(message())"
                  [disabled]="busy()"
                  aria-label="儲存回答為成果文件"
                  title="儲存成果"
                >
                  <nx-icon name="document" />
                </button>
              }
            }
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
          @if (!readOnly() && message().role === 'assistant' && message().content) {
            <nx-message-feedback [message]="message()" (rated)="rated.emit($event)" />
          }
        </div>
        @if (message().role === 'assistant') {
          <div class="message-meta">
            <nx-run-timing [value]="message().timing" />
            @if (message().charge; as charge) {
              <nx-charge-label [charge]="charge" />
            }
            @if (message().webSearchCharge; as charge) {
              <span class="meta-label">搜尋</span><nx-charge-label [charge]="charge" />
            }
          </div>
        }
      </footer>
    }
    @if (copyError()) {
      <nx-notice [message]="copyError()" />
    }
  </article>`,
})
export class ChatMessage {
  readonly modelName = formatModelDisplayName;
  readonly message = input.required<MessageDto>();
  readonly tree = input.required<MessageTree>();
  readonly matched = input(false);
  readonly currentMatch = input(false);
  /** 最後一則：操作列一直顯示；其他訊息在指到或焦點進入時才顯示（觸控一直顯示）。 */
  readonly latest = input(false);
  readonly active = input(false);
  readonly showModelNames = input(true);
  readonly allowArtifacts = input(false);
  /** 唯讀檢視（管理員稽核）：不提供編輯、重新生成、儲存成果與評分。 */
  readonly readOnly = input(false);
  /** 附件的授權容器；唯讀檢視的附件經由容器開啟，見 {@link MessageContent}。 */
  readonly fileScope = input<FileScope | null>(null);
  readonly saveArtifact = output<MessageDto>();
  readonly rated = output<{ id: string; rating: number }>();
  readonly busy = input(false);
  /** 即時回答的文字；只有使用中的訊息會讀取，其他列略過每個 token。 */
  readonly stream = input<Signal<string>>(idle);
  readonly streamContent = computed(() => (this.active() ? this.stream()() : ''));
  readonly status = input('');
  /** 對話顯示之後才抵達，因此會有進場動畫。 */
  readonly enter = input(false);
  /** 這則回答在畫面上從生成中變成完成（不是開啟對話時就已完成）。 */
  readonly settled = linkedSignal<boolean, boolean>({
    source: this.active,
    computation: (active, previous) => !active && (previous?.source ?? false),
  });
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
    this.versions().findIndex((item) => item.id === this.message().id),
  );
  async copyAnswer() {
    await this.feedback.copy(this.message().content);
  }
}
