import { safeMessage } from '@core/errors/safe-errors';
import { Injectable, inject, signal } from '@angular/core';
import { AuthService } from '@core/auth/auth-service';
import type { CollectionDto } from '@core/api/schema';
import { KnowledgeApi } from '../knowledge-api';

/** 對話的選取範圍屬於帳號，且在生成開始前就會保存。 */
@Injectable({ providedIn: 'root' })
export class KnowledgeSelection {
  private readonly api = inject(KnowledgeApi);
  private readonly auth = inject(AuthService);
  readonly collections = signal<CollectionDto[]>([]);
  readonly ids = signal<string[]>([]);
  /** 讀取或儲存選取中；期間擋下其他切換。 */
  readonly saving = signal(false);
  /** 正在儲存的那個知識庫（勾選旁轉圈）；讀取選取時為 null。 */
  readonly toggling = signal<string | null>(null);
  /** 可選的知識庫清單讀取中；沒有清單時畫骨架，不先說「沒有符合的知識庫」。 */
  readonly listing = signal(false);
  readonly loadFailed = signal(false);
  readonly error = signal('');
  readonly revision = signal(0);
  private conversation: string | null = null;
  private version = 0;
  /** 直到帳號改變，或較新的載入／重設取代此選取前都為 true。 */
  private guard() {
    const version = this.version,
      session = this.auth.guard();
    return () => version === this.version && session();
  }
  reset() {
    this.version++;
    this.conversation = null;
    this.collections.set([]);
    this.ids.set([]);
    this.saving.set(false);
    this.toggling.set(null);
    this.listing.set(false);
    this.loadFailed.set(false);
    this.error.set('');
    this.revision.update((item) => item + 1);
  }
  async initialize() {
    const current = this.guard(),
      session = this.auth.guard();
    this.listing.set(true);
    try {
      const rows = await this.api.collections();
      if (current()) this.collections.set(rows);
    } catch (error) {
      if (current()) this.error.set(safeMessage(error));
    } finally {
      // 較新的 load() 會讓 current 失效，但清單讀取與它無關，仍要解除；換帳號時由 reset() 處理。
      if (session()) this.listing.set(false);
    }
  }
  async load(conversation: string | null) {
    this.version++;
    const current = this.guard();
    this.conversation = conversation;
    this.ids.set([]);
    this.saving.set(!!conversation);
    this.loadFailed.set(false);
    this.error.set('');
    try {
      if (conversation) {
        const value = await this.api.selection(conversation);
        if (current()) this.ids.set(value.collectionIds);
      }
    } catch (error) {
      if (current()) {
        this.loadFailed.set(true);
        this.error.set(safeMessage(error));
      }
    } finally {
      if (current()) {
        this.saving.set(false);
        this.revision.update((item) => item + 1);
      }
    }
  }
  reload() {
    if (!this.saving()) return this.load(this.conversation);
    return Promise.resolve();
  }
  async toggle(id: string, checked: boolean) {
    if (this.saving() || this.loadFailed()) return;
    const before = this.ids(),
      next = checked ? [...new Set([...before, id])] : before.filter((item) => item !== id);
    if (next.length > 3) return;
    const current = this.guard();
    this.ids.set(next);
    this.error.set('');
    this.saving.set(true);
    this.toggling.set(id);
    try {
      if (this.conversation) await this.api.select(this.conversation, next);
    } catch (error) {
      if (current()) {
        this.ids.set(before);
        this.error.set(safeMessage(error));
      }
    } finally {
      if (current()) {
        this.saving.set(false);
        this.toggling.set(null);
        this.revision.update((item) => item + 1);
      }
    }
  }
  async bindNew(conversation: string) {
    const current = this.guard();
    if (this.ids().length) await this.api.select(conversation, this.ids());
    if (current()) {
      this.conversation = conversation;
      this.revision.update((item) => item + 1);
    }
  }
}
