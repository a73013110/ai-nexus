import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { generationStatus } from '../../core/api/generation-status';
import { WorkspaceMenuButton } from '../../core/layout/workspace-menu-button';
import { Icon } from '../../shared/ui/icon';
import { InferenceSignal } from '../../shared/ui/inference-signal';
import { InlineTitle } from '../../shared/ui/inline-title';
import type { ProductTourDefinition } from '../../shared/ui/product-tour';
import { ProductTourButton } from '../../shared/ui/product-tour-button';
import { ConversationSpendView } from '../billing/conversation-spend';
import { ConversationActions, type ConversationAction } from '../workspace/conversation-actions';
import { ChatConversations } from './chat-conversations';
import { ChatStore } from './chat-store';

/** The open conversation's title, generation status and actions. */
@Component({
  selector: 'header[nxChatTopbar]',
  imports: [
    RouterLink,
    Icon,
    InferenceSignal,
    InlineTitle,
    ProductTourButton,
    ConversationSpendView,
    ConversationActions,
    WorkspaceMenuButton,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './chat-topbar.scss',
  templateUrl: './chat-topbar.html',
})
export class ChatTopbar {
  readonly store = inject(ChatStore);
  private readonly conversations = inject(ChatConversations);
  readonly tour = input.required<ProductTourDefinition>();
  readonly action = output<ConversationAction>();
  readonly rename = (title: string) => {
    const conversation = this.store.selected();
    return conversation
      ? this.conversations.rename(conversation.id, title)
      : Promise.resolve(false);
  };
  readonly status = computed(() => {
    if (this.store.stopping()) return '正在停止…';
    if (this.store.connection() === 'reconnecting') return '連線中斷，正在恢復…';
    const run = generationStatus(this.store.liveRun()?.status ?? '', !!this.store.streamingText());
    if (run) return run.label;
    if (!this.store.submitting()) return '';
    return this.store.webSearchEnabled() ? '正在搜尋網路並準備回答…' : '正在提交訊息…';
  });
}
