import { Injectable, computed, inject, linkedSignal } from '@angular/core';
import { apiResource } from '@core/api/api-resource';
import type { AdminConversationDetailDto } from '@core/api/schema';
import { ViewScope } from '@shared/browser/view-scope';
import { AdministrationApi } from '../administration-api';
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
  private readonly api = inject(AdministrationApi);
  private readonly scope = inject(ViewScope);
  private readonly owner = inject(AdminUserStore);
  private readonly key = this.owner.key;
  private readonly wanted = linkedSignal(() => (this.key(), false));
  readonly search = linkedSignal(() => (this.key(), ''));
  private readonly query = linkedSignal(() => (this.key(), ''));
  private readonly typing = linkedSignal(() => (this.key(), false));
  readonly includeDeleted = linkedSignal(() => (this.key(), true));
  private readonly offset = linkedSignal(() => (this.key(), 0));
  readonly pageSize = 50;
  private readonly listRead = apiResource({
    params: () => {
      const key = this.key(),
        id = this.owner.user()?.id;
      return key && id && this.wanted()
        ? {
            key,
            id,
            search: this.query(),
            includeDeleted: this.includeDeleted(),
            offset: this.offset(),
          }
        : undefined;
    },
    loader: async (request) => ({
      key: request.key,
      data: await this.api.conversations(
        request.id,
        request.search,
        request.includeDeleted,
        request.offset,
      ),
    }),
  });
  readonly page = computed(() => {
    const loaded = this.listRead.value();
    return loaded && loaded.key === this.key() ? loaded.data : null;
  });
  readonly loading = computed(() => this.typing() || this.listRead.refreshing());
  readonly error = this.listRead.error;
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
        this.offset.set(0);
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
    this.offset.set(0);
    this.includeDeleted.set(value);
  }
  loadList(offset = 0) {
    this.offset.set(offset);
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
