import { Injectable, inject, signal } from '@angular/core';
import { AuthService } from '../../core/auth/auth-service';
import type { Collection } from '../../core/api/types';
import { KnowledgeApi } from './knowledge-api';

/** Conversation selection is account-scoped and persists before a generation begins. */
@Injectable({ providedIn: 'root' })
export class KnowledgeSelection {
  private readonly api = inject(KnowledgeApi);
  private readonly auth = inject(AuthService);
  readonly collections = signal<Collection[]>([]);
  readonly ids = signal<string[]>([]);
  readonly saving = signal(false);
  readonly loadFailed = signal(false);
  readonly error = signal('');
  readonly revision = signal(0);
  private conversation: string | null = null;
  private version = 0;
  reset() {
    this.version++;
    this.conversation = null;
    this.collections.set([]);
    this.ids.set([]);
    this.saving.set(false);
    this.loadFailed.set(false);
    this.error.set('');
    this.revision.update((x) => x + 1);
  }
  async initialize() {
    const generation = this.auth.generation(),
      version = this.version;
    try {
      const rows = await this.api.collections();
      if (generation === this.auth.generation() && version === this.version)
        this.collections.set(rows);
    } catch (error) {
      if (generation === this.auth.generation() && version === this.version)
        this.error.set(error instanceof Error ? error.message : '知識來源載入失敗。');
    }
  }
  async load(conversation: string | null) {
    const version = ++this.version,
      generation = this.auth.generation();
    this.conversation = conversation;
    this.ids.set([]);
    this.saving.set(!!conversation);
    this.loadFailed.set(false);
    this.error.set('');
    try {
      if (conversation) {
        const value = await this.api.selection(conversation);
        if (version === this.version && generation === this.auth.generation())
          this.ids.set(value.collectionIds);
      }
    } catch (error) {
      if (version === this.version && generation === this.auth.generation()) {
        this.loadFailed.set(true);
        this.error.set(
          error instanceof Error ? error.message : '知識來源載入失敗，請重新載入對話。',
        );
      }
    } finally {
      if (version === this.version && generation === this.auth.generation()) {
        this.saving.set(false);
        this.revision.update((x) => x + 1);
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
      next = checked ? [...new Set([...before, id])] : before.filter((x) => x !== id);
    if (next.length > 3) return;
    const version = this.version,
      generation = this.auth.generation();
    this.ids.set(next);
    this.error.set('');
    this.saving.set(true);
    try {
      if (this.conversation) await this.api.select(this.conversation, next);
    } catch (error) {
      if (version === this.version && generation === this.auth.generation()) {
        this.ids.set(before);
        this.error.set(error instanceof Error ? error.message : '知識來源儲存失敗。');
      }
    } finally {
      if (version === this.version && generation === this.auth.generation()) {
        this.saving.set(false);
        this.revision.update((x) => x + 1);
      }
    }
  }
  async bindNew(conversation: string) {
    const generation = this.auth.generation(),
      version = this.version;
    if (this.ids().length) await this.api.select(conversation, this.ids());
    if (version === this.version && generation === this.auth.generation()) {
      this.conversation = conversation;
      this.revision.update((x) => x + 1);
    }
  }
}
