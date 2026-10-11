import { inject, Injectable, signal } from '@angular/core';
import { NexusApi } from '../../core/api/nexus-api';
import type { ConversationDto } from '../../core/api/schema';

const PAGE_SIZE = 100;
const listOrder = (a: ConversationDto, b: ConversationDto) =>
  Number(b.isFavorite) - Number(a.isFavorite) || b.updatedAt.localeCompare(a.updatedAt);

/** The sidebar's conversation list with its search, view and label filters. */
@Injectable({ providedIn: 'root' })
export class ChatHistory {
  private readonly api = inject(NexusApi);
  readonly conversations = signal<ConversationDto[]>([]);
  readonly hasMore = signal(false);
  readonly search = signal('');
  readonly view = signal('active');
  readonly label = signal('');
  readonly labels = signal<string[]>([]);
  private version = 0;

  /** Only the latest request applies, so fast typing never shows an older result. */
  async refresh(more = false) {
    const version = ++this.version;
    const rows = await this.api.conversations(
      this.search(),
      more ? this.conversations().length : 0,
      this.view(),
      this.label(),
    );
    if (version !== this.version) return;
    this.conversations.update((current) => (more ? [...current, ...rows] : rows));
    this.hasMore.set(rows.length === PAGE_SIZE);
  }

  /**
   * Shows a conversation's new state where the server's list would put it (favourites first, then
   * most recently updated), or drops it when it no longer matches the view or label filter.
   */
  place(conversation: ConversationDto) {
    const view = this.view(),
      label = this.label();
    const fits =
      (view === 'all' || conversation.isArchived === (view === 'archived')) &&
      (view !== 'favorites' || conversation.isFavorite) &&
      (!label || !!conversation.labels?.includes(label));
    this.conversations.update((rows) => {
      const others = rows.filter((row) => row.id !== conversation.id);
      return fits ? [...others, conversation].sort(listOrder) : others;
    });
  }

  remove(id: string) {
    this.conversations.update((rows) => rows.filter((row) => row.id !== id));
  }

  reset() {
    this.version++;
    this.conversations.set([]);
    this.labels.set([]);
    this.view.set('active');
    this.label.set('');
    this.search.set('');
  }
}
