import { ChangeDetectionStrategy, Component, inject, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { WorkspaceMenuButton } from '../../core/layout/workspace-menu-button';
import { Icon } from '../../shared/ui/icon';
import { InlineTitle } from '../../shared/ui/inline-title';
import type { ProductTourDefinition } from '../../shared/ui/product-tour';
import { ProductTourButton } from '../../shared/ui/product-tour-button';
import { ConversationSpendView } from '../billing/conversation-spend';
import { ConversationActions, type ConversationAction } from '../workspace/conversation-actions';
import { ChatConversations } from './chat-conversations';
import { ChatStore } from './chat-store';

/** The open conversation's title and actions; generation status lives in the composer island. */
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
  readonly tour = input.required<ProductTourDefinition>();
  readonly action = output<ConversationAction>();
  readonly rename = (title: string) => {
    const conversation = this.store.selected();
    return conversation
      ? this.conversations.rename(conversation.id, title)
      : Promise.resolve(false);
  };
}
