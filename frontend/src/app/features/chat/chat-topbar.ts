import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { Router, RouterLink } from '@angular/router';
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
  private readonly router = inject(Router);
  readonly tour = input.required<ProductTourDefinition>();
  readonly action = output<ConversationAction>();
  private readonly media = matchMedia('(max-width: 640px)');
  /** Phones keep the menu button, the title, one overflow menu and the tour. */
  readonly compact = signal(this.media.matches);
  constructor() {
    const change = () => this.compact.set(this.media.matches);
    this.media.addEventListener('change', change);
    inject(DestroyRef).onDestroy(() => this.media.removeEventListener('change', change));
  }
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
