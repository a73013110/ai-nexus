import { Injectable, computed, inject, linkedSignal } from '@angular/core';
import { apiResource } from '@core/api/api-resource';
import type { MessageDto } from '@core/api/schema';
import type { FileScope } from '@shared/browser/reader-overlay';
import { ViewScope } from '@shared/browser/view-scope';
import { endpointTable } from '@shared/ui/table-model';
import { MessageTree } from '@features/work/conversations/message-tree';
import { IdentityApi } from '../identity-api';
import { AdminUserStore } from './admin-user-store';

/**
 * 受稽核、唯讀的被檢視使用者對話檢視。分頁第一次顯示時才開始列出（每次列出、讀取與開啟附件都會被稽核），
 * 切換分頁時會保留其位置。內容與聊天共用訊息樹：一次讀完整段對話，依分支顯示並可切換重新生成的版本。
 */
@Injectable()
export class UserConversationReader {
  private readonly api = inject(IdentityApi);
  private readonly scope = inject(ViewScope);
  private readonly owner = inject(AdminUserStore);
  private readonly key = this.owner.key;
  private readonly wanted = linkedSignal(() => (this.key(), false));
  readonly search = linkedSignal(() => (this.key(), ''));
  private readonly query = linkedSignal(() => (this.key(), ''));
  private readonly typing = linkedSignal(() => (this.key(), false));
  readonly includeDeleted = linkedSignal(() => (this.key(), true));
  /**
   * 對話列表：後端每頁固定 50 段、以 offset 分頁。換使用者、搜尋或「含已刪除」都是新的查詢條件，
   * 表格模型自己回到第一頁；回應帶著讀取時的使用者，換人後舊回應不顯示。
   */
  readonly table = endpointTable({
    params: computed(() => {
      const key = this.key(),
        id = this.owner.user()?.id;
      return key && id && this.wanted()
        ? { key, id, search: this.query(), includeDeleted: this.includeDeleted() }
        : undefined;
    }),
    load: async (request, page) => ({
      ...(await this.api.conversations(
        request.id,
        request.search,
        request.includeDeleted,
        page.offset,
      )),
      key: request.key,
      complete: false,
    }),
    columns: [],
    pageSize: 50,
    pageSizes: [50],
    unit: '段',
  });
  readonly page = computed(() => {
    const loaded = this.table.data();
    return loaded && loaded.key === this.key() ? loaded : null;
  });
  /** 超過一頁才顯示分頁；已翻到後面的頁時即使總數變少也保留，才能回到前面。 */
  readonly paged = computed(
    () =>
      !!this.page() && (this.table.total() > this.table.pageSize() || this.table.pageIndex() > 0),
  );
  readonly loading = computed(() => this.typing() || this.table.refreshing());
  readonly error = this.table.error;
  /** 正在閱讀的對話；換使用者時清空。 */
  private readonly opened = linkedSignal<string | undefined, string | null>({
    source: this.key,
    computation: () => null,
  });
  /** 為閱讀而選的對話，在其訊息抵達前後都是如此。 */
  readonly selected = this.opened.asReadonly();
  private readonly detailRead = apiResource({
    params: () => this.opened() ?? undefined,
    loader: async (id) => ({ id, data: await this.api.conversation(id) }),
  });
  /** 只顯示目前選取的對話；切換後舊回應即使抵達也不顯示。 */
  readonly detail = computed(() => {
    const loaded = this.detailRead.value();
    return loaded && loaded.id === this.opened() ? loaded.data : null;
  });
  readonly reading = this.detailRead.refreshing;
  readonly readError = this.detailRead.error;
  /** 附件經由這段對話授權開啟（每次都稽核），不是對原始附件的存取權。 */
  readonly fileScope = computed<FileScope | null>(() => {
    const id = this.detail()?.conversation.id;
    return id ? { kind: 'conversation', id } : null;
  });
  readonly tree = computed(() => new MessageTree(this.detail()?.messages ?? []));
  /**
   * 顯示中的分支：先開擁有者最後停留的分支（沒有記錄時用最新一則）。切換版本只改這個檢視，
   * 不寫回對話，擁有者下次開啟看到的分支不受影響。
   */
  private readonly leaf = linkedSignal(() => {
    const detail = this.detail();
    return detail?.activeLeafId ?? detail?.messages.at(-1)?.id ?? null;
  });
  readonly visibleMessages = computed(() => this.tree().branch(this.leaf()));

  start() {
    this.wanted.set(true);
  }
  searchChanged(value: string) {
    this.search.set(value);
    this.typing.set(true);
    this.scope.later(
      () => {
        this.typing.set(false);
        this.query.set(value);
      },
      250,
      'inspector-search',
    );
  }
  deletedChanged(value: boolean) {
    this.typing.set(false);
    this.scope.cancel('inspector-search');
    this.query.set(this.search());
    this.includeDeleted.set(value);
  }
  /** 窄螢幕上回到列表；下一次讀取會重新開始。 */
  close() {
    this.opened.set(null);
  }
  read(id: string) {
    this.opened.set(id);
  }
  selectVersion(message: MessageDto, direction: number) {
    const leaf = this.tree().versionLeaf(message, direction);
    if (leaf) this.leaf.set(leaf);
  }
}
