import { inject, Injectable, signal } from '@angular/core';
import { WorkspaceApi } from '@core/api/workspace-api';
import type { ConversationDto } from '@core/api/schema';
import { AuthService } from '@core/auth/auth-service';
import { ConversationsApi } from '@features/conversations/conversations-api';

const PAGE_SIZE = 100;
const listOrder = (first: ConversationDto, second: ConversationDto) =>
  Number(second.isFavorite) - Number(first.isFavorite) ||
  second.updatedAt.localeCompare(first.updatedAt);

/** 側邊欄的對話列表，含搜尋、檢視與標籤篩選。 */
@Injectable({ providedIn: 'root' })
export class ChatHistory {
  private readonly api = inject(WorkspaceApi);
  private readonly conversationsApi = inject(ConversationsApi);
  private readonly auth = inject(AuthService);
  readonly conversations = signal<ConversationDto[]>([]);
  readonly hasMore = signal(false);
  readonly search = signal('');
  readonly view = signal('active');
  readonly label = signal('');
  readonly labels = signal<string[]>([]);
  private version = 0;

  /** 只套用最新的請求，因此快速輸入時不會顯示較舊的結果。 */
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
   * 把對話的新狀態放在伺服器列表會排列的位置（我的最愛在前，再依最近更新排序）；若已不符合檢視或標
   * 籤篩選，則移除它。
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

  /** 標籤會隨對話設定與匯入而變，呼叫端在這些操作後要重新讀取。 */
  async reloadLabels() {
    const current = this.auth.guard();
    const labels = await this.conversationsApi.labels();
    if (current()) this.labels.set(labels);
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
