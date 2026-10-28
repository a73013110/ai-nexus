import { Injectable, computed, inject, linkedSignal } from '@angular/core';
import { apiResource } from '@core/api/api-resource';
import type { AdminConversationDetailDto } from '@core/api/schema';
import { ViewScope } from '@shared/browser/view-scope';
import { endpointTable } from '@shared/ui/table-model';
import { IdentityApi } from '../identity-api';
import { AdminUserStore } from './admin-user-store';

interface Opened {
  id: string;
  offset: number;
}

/**
 * 受稽核、唯讀的被檢視使用者對話檢視。分頁第一次顯示時才開始列出（每次列出與每次讀取都會被稽核），
 * 切換分頁時會保留其位置。
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
  /** 正在閱讀的對話，以及其中已顯示的訊息數。 */
  private readonly opened = linkedSignal<string | undefined, Opened | null>({
    source: this.key,
    computation: () => null,
  });
  /** 為閱讀而選的對話，在其訊息抵達前後都是如此。 */
  readonly selected = computed(() => this.opened()?.id ?? null);
  private readonly detailRead = apiResource({
    params: () => this.opened() ?? undefined,
    loader: async (request) => ({
      request,
      data: await this.api.conversation(request.id, request.offset),
    }),
  });
  readonly detail = linkedSignal<
    { request: Opened | null; loaded?: { request: Opened; data: AdminConversationDetailDto } },
    AdminConversationDetailDto | null
  >({
    source: () => ({ request: this.opened(), loaded: this.detailRead.value() }),
    computation: ({ request, loaded }, previous) => {
      if (!request) return null;
      const shown = request.offset ? (previous?.value ?? null) : null;
      if (loaded?.request !== request) return shown;
      return shown
        ? { ...loaded.data, offset: 0, messages: [...shown.messages, ...loaded.data.messages] }
        : loaded.data;
    },
  });
  readonly reading = this.detailRead.refreshing;
  readonly readError = this.detailRead.error;

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
  read(id: string, more = false) {
    if (more) {
      if (this.reading()) return;
      this.opened.set({ id, offset: this.detail()?.messages.length || 0 });
    } else this.opened.set({ id, offset: 0 });
  }
}
