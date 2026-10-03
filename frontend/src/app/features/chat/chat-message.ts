import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { DomSanitizer } from '@angular/platform-browser';
import type { Message } from '../../core/api/types';
import { Icon } from '../../shared/ui/icon';
import { renderMarkdown } from '../../shared/ui/markdown';
import { CopyFeedback } from '../../shared/browser/copy-feedback';
import { AttachmentList } from '../attachments/attachment-list';
import { MessageTree } from './message-tree';
import { InferenceSignal } from '../../shared/ui/inference-signal';

@Component({
  selector: 'nx-chat-message',
  imports: [Icon, AttachmentList, InferenceSignal],
  providers: [CopyFeedback],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: ` <article
    class="message"
    [class.user]="message().role === 'user'"
    [class.search-match]="matched()"
    [class.search-current]="currentMatch()"
    [attr.data-message-id]="message().id"
    animate.enter="message-enter"
  >
    <div class="message-heading">
      <span class="role-mark" [class.nexus]="message().role === 'assistant'">{{
        message().role === 'user' ? '你' : 'N'
      }}</span
      ><strong>{{ message().role === 'user' ? '你' : 'AI Nexus' }}</strong>
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
        <div class="streaming-copy">{{ streamContent() }}</div>
      } @else {
        <div class="waiting-copy">
          <span class="waiting-graphic"><nx-inference-signal [active]="true" /></span
          ><span
            >{{ status() === 'queued' ? '已加入佇列，等待模型…' : '正在整理回答…'
            }}<small>讓資訊逐步成形</small></span
          >
        </div>
      }
    } @else {
      <div class="markdown" [innerHTML]="html()" (click)="copyCode($event)"></div>
    }
    @if (message().status === 'cancelled' && !active()) {
      <p class="message-note">已停止 · 保留部分回答</p>
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
  </article>`,
})
export class ChatMessage {
  private readonly sanitizer = inject(DomSanitizer);
  readonly message = input.required<Message>();
  readonly tree = input.required<MessageTree>();
  readonly matched = input(false);
  readonly currentMatch = input(false);
  readonly active = input(false);
  readonly showModelNames = input(true);
  readonly busy = input(false);
  readonly streamContent = input('');
  readonly status = input('');
  readonly edit = output<Message>();
  readonly regenerate = output<Message>();
  readonly version = output<number>();
  readonly feedback = inject(CopyFeedback);
  readonly copied = this.feedback.copied;
  readonly copyError = this.feedback.error;
  readonly html = computed(() =>
    this.active()
      ? ''
      : this.sanitizer.bypassSecurityTrustHtml(renderMarkdown(this.message().content)),
  );
  readonly versions = computed(() => this.tree().versions(this.message()));
  readonly versionIndex = computed(() =>
    this.versions().findIndex((x) => x.id === this.message().id),
  );
  readonly failureText = computed(() =>
    this.message().errorCode === 'generation_timeout'
      ? '模型回應逾時'
      : this.message().errorCode === 'server_restarted'
        ? '伺服器重新啟動，生成已中斷'
        : '生成未完成',
  );
  async copyAnswer() {
    await this.feedback.copy(this.message().content);
  }
  async copyCode(event: MouseEvent) {
    const target = event.target instanceof Element ? event.target.closest('.code-copy') : null;
    const code = target?.closest('.code-block')?.querySelector('code');
    if (!target || !code) return;
    await this.feedback.copy(code.textContent ?? '', target);
  }
}
