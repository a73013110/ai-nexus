import { ChangeDetectionStrategy, Component, inject, input, output } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { WorkspaceMenuButton } from '@core/layout/workspace-menu-button';
import { below } from '@shared/browser/breakpoints';
import { mediaQuery } from '@shared/browser/media-query';
import { Icon } from '@shared/ui/icon';
import { InlineTitle } from '@shared/ui/inline-title';
import type { ProductTourDefinition } from '@shared/ui/product-tour';
import { ProductTourButton } from '@shared/ui/product-tour-button';
import { ConversationSpendView } from '@features/admin/billing/conversation-spend';
import {
  ConversationActions,
  type ConversationAction,
} from '@features/work/conversations/conversation-actions';
import { ChatConversations } from './state/chat-conversations';
import { ChatStore } from './chat-store';

/** 開啟中對話的標題與動作；生成狀態則在輸入框的島中。 */
@Component({
  selector: 'header[nxChatTopbar]',
  imports: [
    RouterLink,
    Icon,
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
  private readonly router = inject(Router);
  readonly tour = input.required<ProductTourDefinition>();
  readonly action = output<ConversationAction>();
  /** 手機保留選單按鈕、標題、一個溢出選單與導覽。 */
  readonly compact = mediaQuery(below('phone'));
  choose(action: ConversationAction, spend: ConversationSpendView) {
    const projectId = this.store.selected()?.projectId;
    if (action === 'spend') spend.open();
    else if (action === 'project' && projectId) void this.router.navigate(['/projects', projectId]);
    else this.action.emit(action);
  }
  readonly rename = (title: string) => {
    const conversation = this.store.selected();
    return conversation
      ? this.conversations.rename(conversation.id, title)
      : Promise.resolve(false);
  };
}
