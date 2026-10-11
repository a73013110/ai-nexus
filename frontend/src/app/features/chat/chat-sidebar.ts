import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  inject,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { FormField, form, maxLength } from '@angular/forms/signals';
import { RouterLink } from '@angular/router';
import type { ConversationDto } from '../../core/api/schema';
import { currentDay } from '../../shared/browser/calendar-day';
import { Icon } from '../../shared/ui/icon';
import { MenuPanel } from '../../shared/ui/menu-panel';
import { Select } from '../../shared/ui/select';
import { NotificationStore } from '../notifications/notification-store';
import { conversationMenu, type ConversationAction } from '../workspace/conversation-actions';
import { ChatConversations } from './chat-conversations';
import { ChatStore } from './chat-store';
import { historyGroups } from './history-groups';

/**
 * Chat history in the shared sidebar. Rows are plain links with one shared action menu, and the
 * browser skips rendering rows outside the viewport, so long histories stay cheap.
 */
@Component({
  selector: 'nx-chat-sidebar',
  imports: [RouterLink, FormField, Icon, Select, MenuPanel],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './chat-sidebar.scss',
  host: { style: 'display: contents' },
  templateUrl: './chat-sidebar.html',
})
export class ChatSidebar {
  readonly store = inject(ChatStore);
  readonly conversations = inject(ChatConversations);
  readonly notifications = inject(NotificationStore);
  readonly action = output<{ conversation: ConversationDto; action: ConversationAction }>();
  readonly navigate = output<void>();
  readonly prompts = output<void>();
  readonly commands = output<void>();
  readonly searchModel = signal({ query: '' });
  readonly searchForm = form(this.searchModel, (schema) => maxLength(schema.query, 120));
  readonly labelOptions = computed(() => [
    { value: '', label: '全部標籤' },
    ...this.store.labels().map((value) => ({ value, label: value })),
  ]);
  private readonly today = currentDay();
  readonly groups = computed(() => historyGroups(this.store.conversations(), this.today()));
  readonly menu = viewChild.required(MenuPanel);
  /** The row whose menu is open; items follow its latest state and running generation. */
  readonly menuTarget = signal<string | null>(null);
  readonly menuItems = computed(() => {
    const conversation = this.store.conversations().find((x) => x.id === this.menuTarget());
    return conversation
      ? conversationMenu(conversation, {
          compact: true,
          sharing: this.store.hasSharing(),
          busy: this.store.liveRun()?.conversationId === conversation.id,
        })
      : [];
  });
  readonly menuLabel = computed(
    () =>
      '對話操作：' +
      (this.store.conversations().find((x) => x.id === this.menuTarget())?.title ?? ''),
  );
  private searchTimer: ReturnType<typeof setTimeout> | null = null;
  constructor() {
    inject(DestroyRef).onDestroy(() => {
      if (this.searchTimer) clearTimeout(this.searchTimer);
    });
  }
  searchChanged() {
    if (this.searchTimer) clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(
      () => void this.store.searchHistory(this.searchModel().query),
      250,
    );
  }
  selectLabel(value: string) {
    void this.conversations.filterHistory(undefined, value);
  }
  async loadMore() {
    try {
      await this.store.refreshHistory(true);
    } catch (error) {
      this.store.report(error);
    }
  }
  menuOpen(id: string) {
    return this.menu().opened() && this.menuTarget() === id;
  }
  openMenu(event: Event, id: string) {
    const trigger = event.currentTarget as HTMLElement;
    if (this.menu().isOpenFor(trigger)) {
      this.menu().close();
      return;
    }
    this.menuTarget.set(id);
    this.menu().open(trigger);
  }
  chooseMenu(action: string) {
    const conversation = this.store.conversations().find((x) => x.id === this.menuTarget());
    if (conversation) this.action.emit({ conversation, action: action as ConversationAction });
  }
  /** Arrow keys move between conversations, like a list; Tab still visits each row's menu. */
  historyKey(event: KeyboardEvent, history: HTMLElement) {
    if (!['ArrowDown', 'ArrowUp', 'Home', 'End'].includes(event.key)) return;
    const links = [...history.querySelectorAll<HTMLElement>('.history-row > a')];
    const current = links.indexOf(event.target as HTMLElement);
    const fromSearch = event.currentTarget !== history;
    if (fromSearch ? event.key !== 'ArrowDown' : current < 0) return;
    event.preventDefault();
    const index =
      event.key === 'Home'
        ? 0
        : event.key === 'End'
          ? links.length - 1
          : Math.max(0, Math.min(links.length - 1, current + (event.key === 'ArrowDown' ? 1 : -1)));
    links[index]?.focus();
  }
}
