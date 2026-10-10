import { safeMessage } from '../../core/errors/safe-errors';
import { DestroyRef, Injectable, effect, inject, signal, untracked } from '@angular/core';
import { Router } from '@angular/router';
import type { NotificationDto } from '../../core/api/schema';
import { WorkspaceSession } from '../../core/auth/workspace-session';
import { WorkspaceLayout } from '../../core/layout/workspace-layout';
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
        this.latest = Math.max(this.latest, ...page.items.map((x) => Date.parse(x.createdAt)));
        this.primed = true;
      }
      const preserve = poll && this.opened() && this.items().length > 50;
      const old = this.items();
      const freshIds = new Set(page.items.map((x) => x.id));
      this.items.set(
        more
          ? [...new Map([...old, ...page.items].map((x) => [x.id, x])).values()]
          : preserve
            ? [
                ...page.items,
                ...old.filter((x) => !freshIds.has(x.id) && (!this.unreadOnly() || !x.readAt)),
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
  open() {
    this.opened.set(true);
    void this.refresh();
  }
  close() {
    this.opened.set(false);
  }
  completed(id: string) {
    return this.items().some(
      (x) =>
        x.target.kind === 'conversation' &&
        x.target.id === id &&
        x.type === 'conversation.completed' &&
        !x.readAt,
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
      const unread = this.items().some((x) => x.id === item.id && !x.readAt);
      this.items.update((rows) =>
        rows.map((x) => (x.id === item.id ? { ...x, readAt: new Date().toISOString() } : x)),
      );
      if (this.unreadOnly()) this.items.update((rows) => rows.filter((x) => !x.readAt));
      if (unread) this.unread.update((value) => Math.max(0, value - 1));
    } catch (e) {
      if (owner === this.owner && generation === this.session.auth.generation())
        this.error.set(safeMessage(e));
      throw e;
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
    } catch (e) {
      if (generation === this.session.auth.generation()) this.error.set(safeMessage(e));
    }
  }
  async dismiss(item: NotificationDto) {
    const generation = this.session.auth.generation();
    try {
      await this.api.dismiss(item.id);
      if (generation === this.session.auth.generation()) await this.refresh();
    } catch (e) {
      if (generation === this.session.auth.generation()) this.error.set(safeMessage(e));
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
      /* Some browsers only permit notifications through a service worker. In-app delivery remains available. */
    }
  }
}
