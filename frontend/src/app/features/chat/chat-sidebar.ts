import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  inject,
  output,
  signal,
} from '@angular/core';
import { FormField, form, maxLength } from '@angular/forms/signals';
import { RouterLink } from '@angular/router';
import type { ConversationDto } from '../../core/api/schema';
import { Icon } from '../../shared/ui/icon';
import { NotificationStore } from '../notifications/notification-store';
import { ConversationActions, type ConversationAction } from '../workspace/conversation-actions';
import { ChatStore } from './chat-store';
import { Select } from '../../shared/ui/select';
import { WorkspaceSidebar } from '../../core/layout/workspace-sidebar';

@Component({
  selector: 'nx-chat-sidebar',
  imports: [RouterLink, FormField, Icon, Select, WorkspaceSidebar, ConversationActions],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './chat-sidebar.scss',
  host: { style: 'display: contents' },
  templateUrl: './chat-sidebar.html',
})
export class ChatSidebar {
  readonly hasSharing = (feature: { id: string }) => feature.id === 'shared';
  readonly store = inject(ChatStore);
  readonly notifications = inject(NotificationStore);
  readonly action = output<{ conversation: ConversationDto; action: ConversationAction }>();
  readonly navigate = output<void>();
  readonly prompts = output<void>();
  readonly commands = output<void>();
  readonly import = output<File>();
  readonly searchModel = signal({ query: '' });
  readonly searchForm = form(this.searchModel, (schema) => maxLength(schema.query, 120));
  readonly labelOptions = computed(() => [
    { value: '', label: '全部標籤' },
    ...this.store.labels().map((value) => ({ value, label: value })),
  ]);
  readonly groups = computed(() => {
    const now = new Date();
    const day = new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Taipei' });
    const today = day.format(now),
      yesterday = day.format(new Date(now.getTime() - 86400000));
    const groups = new Map<string, ConversationDto[]>();
    for (const conversation of this.store.conversations()) {
      const date = day.format(new Date(conversation.updatedAt));
      const label = conversation.isFavorite
        ? '收藏'
        : date === today
          ? '今天'
          : date === yesterday
            ? '昨天'
            : '先前的對話';
      const rows = groups.get(label) ?? [];
      rows.push(conversation);
      groups.set(label, rows);
    }
    return [...groups].map(([label, conversations]) => ({ label, conversations }));
  });
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
    void this.store.filterHistory(undefined, value);
  }
  importFile(event: Event) {
    const input = event.target as HTMLInputElement;
    if (input.files?.[0]) this.import.emit(input.files[0]);
    input.value = '';
  }
  async loadMore() {
    try {
      await this.store.refreshHistory(true);
    } catch (error) {
      this.store.report(error);
    }
  }
}
