import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  effect,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import type {
  AdminUser,
  AdminUserDetail,
  AdminConversationPage,
  AdminConversationDetail,
} from '../../core/api/types';
import { Icon } from '../../shared/ui/icon';
import { SearchField } from '../../shared/ui/search-field';
import { Checkbox } from '../../shared/ui/checkbox';
import { MarkdownView } from '../../shared/ui/markdown-view';
import { formatBytes, formatDate, formatNumber } from '../../shared/browser/format';
import { AdminApi } from './admin-api';

@Component({
  selector: 'nx-admin-user-inspector',
  imports: [Icon, SearchField, Checkbox, MarkdownView],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './admin-user-inspector.html',
})
export class AdminUserInspector {
  readonly user = input<AdminUser | null>(null);
  readonly closed = output<void>();
  readonly overview = signal<AdminUserDetail | null>(null);
  readonly conversations = signal<AdminConversationPage | null>(null);
  readonly detail = signal<AdminConversationDetail | null>(null);
  readonly search = signal('');
  readonly includeDeleted = signal(true);
  readonly loading = signal(false);
  readonly reading = signal(false);
  readonly error = signal('');
  readonly readError = signal('');
  readonly date = formatDate;
  readonly format = formatNumber;
  readonly bytes = formatBytes;
  private readonly api = inject(AdminApi);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  private version = 0;
  private listVersion = 0;
  private readVersion = 0;
  private timer?: ReturnType<typeof setTimeout>;
  constructor() {
    effect(() => {
      const user = this.user(),
        dialog = this.dialog().nativeElement;
      ++this.version;
      ++this.listVersion;
      ++this.readVersion;
      clearTimeout(this.timer);
      this.overview.set(null);
      this.conversations.set(null);
      this.detail.set(null);
      this.search.set('');
      this.includeDeleted.set(true);
      this.error.set('');
      this.readError.set('');
      this.reading.set(false);
      if (user) {
        if (!dialog.open) dialog.showModal();
        void this.load(user.id, this.version);
      } else if (dialog.open) dialog.close();
    });
    inject(DestroyRef).onDestroy(() => {
      ++this.version;
      ++this.listVersion;
      ++this.readVersion;
      clearTimeout(this.timer);
    });
  }
  close() {
    this.dialog().nativeElement.close();
  }
  private async load(id: string, version: number) {
    const listVersion = this.listVersion;
    this.loading.set(true);
    try {
      const [overview, conversations] = await Promise.all([
        this.api.insights(id),
        this.api.conversations(id, '', true),
      ]);
      if (version !== this.version) return;
      this.overview.set(overview);
      if (listVersion === this.listVersion) this.conversations.set(conversations);
    } catch (error) {
      if (version === this.version) this.error.set(this.message(error));
    } finally {
      if (version === this.version && listVersion === this.listVersion) this.loading.set(false);
    }
  }
  searchChanged(value: string) {
    this.search.set(value);
    clearTimeout(this.timer);
    ++this.listVersion;
    this.timer = setTimeout(() => void this.loadList(), 250);
  }
  deletedChanged(value: boolean) {
    this.includeDeleted.set(value);
    clearTimeout(this.timer);
    void this.loadList();
  }
  async loadList(offset = 0) {
    const user = this.user();
    if (!user) return;
    const version = ++this.listVersion;
    this.error.set('');
    this.loading.set(true);
    try {
      const rows = await this.api.conversations(
        user.id,
        this.search(),
        this.includeDeleted(),
        offset,
      );
      if (version === this.listVersion) this.conversations.set(rows);
    } catch (error) {
      if (version === this.listVersion) this.error.set(this.message(error));
    } finally {
      if (version === this.listVersion) this.loading.set(false);
    }
  }
  async read(id: string, more = false) {
    if (this.reading() && more) return;
    const version = ++this.readVersion;
    const old = more ? this.detail() : null;
    if (!more) this.detail.set(null);
    this.reading.set(true);
    this.readError.set('');
    try {
      const detail = await this.api.conversation(id, old?.messages.length || 0);
      if (version === this.readVersion)
        this.detail.set(
          old ? { ...detail, offset: 0, messages: [...old.messages, ...detail.messages] } : detail,
        );
    } catch (error) {
      if (version === this.readVersion) this.readError.set(this.message(error));
    } finally {
      if (version === this.readVersion) this.reading.set(false);
    }
  }
  kind(value: string) {
    return (
      ({ chat: '對話', transform: '文字處理', evaluation: '品質評測' } as Record<string, string>)[
        value
      ] || value
    );
  }
  private message(error: unknown) {
    return error instanceof Error ? error.message : '無法取得資料，請重試。';
  }
}
