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
  readonly saving = signal(false);
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
    this.loadFailed.set(false);
    this.error.set('');
    this.revision.update((item) => item + 1);
  }
  async initialize() {
    const current = this.guard();
    try {
      const rows = await this.api.collections();
      if (current()) this.collections.set(rows);
    } catch (error) {
      if (current()) this.error.set(safeMessage(error));
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
