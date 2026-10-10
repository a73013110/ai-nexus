import { inject, Injectable, signal } from '@angular/core';
import { NexusApi } from '../../core/api/nexus-api';
import type { ConversationDto } from '../../core/api/schema';

const PAGE_SIZE = 100;

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

  reset() {
    this.version++;
    this.conversations.set([]);
    this.labels.set([]);
    this.view.set('active');
    this.label.set('');
    this.search.set('');
  }
}
