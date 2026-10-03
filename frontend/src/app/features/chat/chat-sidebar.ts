import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { FormField, form, maxLength } from '@angular/forms/signals';
import { RouterLink } from '@angular/router';
import type { Conversation } from '../../core/api/types';
import { ThemeService } from '../../core/preferences/theme-service';
import { Icon } from '../../shared/ui/icon';
import { Disclosure } from '../../shared/ui/disclosure';
import { ChatStore } from './chat-store';

@Component({
  selector: 'nx-chat-sidebar',
  imports: [RouterLink, FormField, Icon, Disclosure],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './chat-sidebar.html',
})
export class ChatSidebar {
  readonly store = inject(ChatStore);
  readonly themes = inject(ThemeService);
  readonly open = input(true);
  readonly narrow = input(false);
  readonly close = output<void>();
  readonly navigate = output<void>();
  readonly rename = output<Conversation>();
  readonly prompts = output<void>();
  readonly commands = output<void>();
  readonly import = output<File>();
  readonly searchModel = signal({ query: '' });
  readonly searchForm = form(this.searchModel, (schema) => maxLength(schema.query, 120));
  readonly preferencesForm = form(this.themes.preferences);
  readonly groups = computed(() => {
    const now = new Date();
    const day = new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Taipei' });
    const today = day.format(now),
      yesterday = day.format(new Date(now.getTime() - 86400000));
    const groups = new Map<string, Conversation[]>();
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
  selectLabel(event: Event) {
    if (event.target instanceof HTMLSelectElement)
      void this.store.filterHistory(undefined, event.target.value);
  }
  savePreferences() {
    queueMicrotask(
      () =>
        void this.store.savePreferences({
          ...this.themes.preferences(),
          defaultModelId: this.store.policy().allowModelSelection
            ? this.store.modelId() || null
            : null,
        }),
    );
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
