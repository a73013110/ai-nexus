import { safeMessage } from '@core/errors/safe-errors';
import { DestroyRef, Injectable, effect, inject, signal, untracked } from '@angular/core';
import { Router } from '@angular/router';
import type { NotificationDto } from '@core/api/schema';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { WorkspaceLayout } from '@core/layout/workspace-layout';
import { notificationUrl } from './notification-target';
import { NotificationsApi } from './notifications-api';

@Injectable({ providedIn: 'root' })
export class NotificationStore {
  private readonly api = inject(NotificationsApi);
  private readonly session = inject(WorkspaceSession);
  private readonly router = inject(Router);
  private readonly layout = inject(WorkspaceLayout);
  readonly items = signal<NotificationDto[]>([]);
  readonly unread = signal(0);
  readonly hasMore = signal(false);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly opened = signal(false);
  readonly unreadOnly = signal(false);
  /** 通知中心的分頁：伺服器收件匣，或此分頁的提示歷史。 */
  readonly view = signal<'inbox' | 'session'>('inbox');
  private timer?: ReturnType<typeof setTimeout>;
  private controller?: AbortController;
  private owner = '';
  private generation = -1;
  private revision = 0;
  private seen = new Set<string>();
  private primed = false;
  private latest = 0;
  constructor() {
    effect(() => {
      const owner = this.session.me()?.id ?? '',
        generation = this.session.auth.generation();
      untracked(() => {
        if (owner === this.owner && generation === this.generation) return;
        this.generation = generation;
        this.owner = owner;
        ++this.revision;
        this.controller?.abort();
        clearTimeout(this.timer);
        this.items.set([]);
        this.unread.set(0);
        this.hasMore.set(false);
        this.opened.set(false);
        this.unreadOnly.set(false);
        this.view.set('inbox');
        this.loading.set(false);
        this.error.set('');
        this.seen.clear();
        this.primed = false;
        this.latest = 0;
        if (owner) void this.poll(generation);
      });
    });
    inject(DestroyRef).onDestroy(() => {
      clearTimeout(this.timer);
      this.controller?.abort();
    });
  }
  private async poll(generation: number) {
    const owner = this.owner;
    await this.refresh(false, true);
    if (owner && owner === this.owner && generation === this.session.auth.generation())
      this.timer = setTimeout(
        () => void this.poll(generation),
        document.visibilityState === 'hidden' ? 30000 : 8000,
      );
  }
  async refresh(more = false, poll = false) {
    if (!this.owner) return;
    const revision = ++this.revision,
      owner = this.owner,
      generation = this.session.auth.generation();
    const valid = () =>
      revision === this.revision &&
      owner === this.owner &&
      generation === this.session.auth.generation();
    this.controller?.abort();
    this.controller = new AbortController();
    this.loading.set(true);
    const before = more && this.items().length ? this.items().at(-1)!.id : undefined;
    try {
      const page = await this.api.list(this.unreadOnly(), before, this.controller.signal);
      if (!valid()) return;
      for (const item of page.items) {
        if (
          !more &&
          this.primed &&
          !this.seen.has(item.id) &&
          !item.readAt &&
          Date.parse(item.createdAt) >= this.latest
        )
          this.browserNotify(item);
        this.seen.add(item.id);
      }
      if (!more) {
        this.latest = Math.max(
          this.latest,
          ...page.items.map((item) => Date.parse(item.createdAt)),
        );
        this.primed = true;
      }
      const preserve = poll && this.opened() && this.items().length > 50;
      const old = this.items();
      const freshIds = new Set(page.items.map((item) => item.id));
      this.items.set(
        more
          ? [...new Map([...old, ...page.items].map((item) => [item.id, item])).values()]
          : preserve
            ? [
                ...page.items,
                ...old.filter(
                  (item) => !freshIds.has(item.id) && (!this.unreadOnly() || !item.readAt),
                ),
              ]
            : page.items,
      );
      this.unread.set(page.unread);
      if (!preserve) this.hasMore.set(page.hasMore);
      this.error.set('');
    } catch (error) {
      if (valid() && !(error instanceof DOMException && error.name === 'AbortError'))
        this.error.set(safeMessage(error));
    } finally {
      if (valid()) this.loading.set(false);
    }
  }
  /** 開啟通知中心的鈴鐺；桌面版 popover 會錨定於它並把焦點還給它。 */
  trigger: HTMLElement | null = null;
  open(trigger: HTMLElement | null = null) {
    this.trigger = trigger;
    this.opened.set(true);
    void this.refresh();
  }
  close() {
    this.opened.set(false);
  }
  completed(id: string) {
    return this.items().some(
      (item2) =>
        item2.target.kind === 'conversation' &&
        item2.target.id === id &&
        item2.type === 'conversation.completed' &&
        !item2.readAt,
    );
  }
  readConversation(id: string) {
    for (const item of this.items())
      if (item.target.kind === 'conversation' && item.target.id === id && !item.readAt)
        void this.read(item).catch(() => {});
  }
  async read(item: NotificationDto) {
    const owner = this.owner,
      generation = this.session.auth.generation();
    try {
      await this.api.read(item.id);
      if (owner !== this.owner || generation !== this.session.auth.generation()) return;
      const unread = this.items().some((item3) => item3.id === item.id && !item3.readAt);
      this.items.update((rows) =>
        rows.map((item4) =>
          item4.id === item.id ? { ...item4, readAt: new Date().toISOString() } : item4,
        ),
      );
      if (this.unreadOnly()) this.items.update((rows) => rows.filter((item) => !item.readAt));
      if (unread) this.unread.update((value) => Math.max(0, value - 1));
    } catch (error2) {
      if (owner === this.owner && generation === this.session.auth.generation())
        this.error.set(safeMessage(error2));
      throw error2;
    }
  }
  async activate(item: NotificationDto) {
    const destination = notificationUrl(item.target, item.version);
    if (!destination) return;
    const generation = this.session.auth.generation();
    try {
      await this.read(item);
      if (generation !== this.session.auth.generation()) return;
      this.close();
      this.layout.closeMobile();
      await this.router.navigateByUrl(destination);
    } catch {}
  }
  async readAll() {
    const through = this.items()[0]?.createdAt,
      generation = this.session.auth.generation();
    if (!through) return;
    try {
      await this.api.readThrough(through);
      if (generation === this.session.auth.generation()) await this.refresh();
    } catch (error3) {
      if (generation === this.session.auth.generation()) this.error.set(safeMessage(error3));
    }
  }
  async dismiss(item: NotificationDto) {
    const generation = this.session.auth.generation();
    try {
      await this.api.dismiss(item.id);
      if (generation === this.session.auth.generation()) await this.refresh();
    } catch (error4) {
      if (generation === this.session.auth.generation()) this.error.set(safeMessage(error4));
    }
  }
  private browserNotify(item: NotificationDto) {
    if (
      !this.session.settings.value().notifyOnCompletion ||
      document.visibilityState !== 'hidden' ||
      !('Notification' in window) ||
      Notification.permission !== 'granted'
    )
      return;
    try {
      const notification = new Notification(item.title, { body: item.body, tag: item.id });
      notification.onclick = () => {
        window.focus();
        void this.activate(item);
        notification.close();
      };
    } catch {
      /* 有些瀏覽器只允許 service worker 送出通知；應用內的通知仍可用。 */
    }
  }
}
