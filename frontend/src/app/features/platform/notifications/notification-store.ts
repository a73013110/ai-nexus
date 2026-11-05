import { isCancellation, safeMessage } from '@core/errors/safe-errors';
import { DestroyRef, Injectable, computed, effect, inject, signal, untracked } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import type { NotificationDto, NotificationTargetDto } from '@core/api/schema';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { WorkspaceLayout } from '@core/layout/workspace-layout';
import { action } from '@shared/browser/action';
import type { NavigationBadgeMap } from '@core/layout/navigation-badges';
import { notificationFeature, notificationUrl, showsTarget } from './notification-target';
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
  /**
   * 使用者感覺得到的讀取：開啟、手動重讀、切換只看未讀、寫入後重讀。輪詢不設它，
   * 背景更新才不會每 8 秒讓重讀鈕轉圈、讓列表淡化。
   */
  readonly loading = signal(false);
  /** 「載入更早的通知」進行中；只讓那顆按鈕轉，列表不淡化。 */
  readonly loadingMore = signal(false);
  /** 寫入：`read:<id>`、`dismiss:<id>` 或 `all`，只讓按下的那顆轉；換帳號後回來的結果丟掉。 */
  private readonly writing = action({ concurrent: true, guard: () => this.guard() });
  private readonly readError = signal('');
  readonly error = computed(() => this.writing.error() || this.readError());
  readonly opened = signal(false);
  readonly unreadOnly = signal(false);
  /** 通知中心的分頁：伺服器收件匣，或此分頁的提示歷史。 */
  readonly view = signal<'inbox' | 'session'>('inbox');
  /**
   * 側欄徽章：已載入的通知中，每個功能的未讀數（例：失敗的對話記在對話）。只算目前載入的最新一頁，
   * 輪詢會更新；更早的未讀不在其中，未讀總數仍以鈴鐺（伺服器的計數）為準。
   */
  readonly featureBadges = computed<NavigationBadgeMap>(() => {
    const counts = new Map<string, number>();
    for (const item of this.items()) {
      const feature = item.readAt ? null : notificationFeature(item.target);
      if (feature) counts.set(feature, (counts.get(feature) ?? 0) + 1);
    }
    return counts;
  });
  private timer?: ReturnType<typeof setTimeout>;
  private controller?: AbortController;
  private owner = '';
  private generation = -1;
  /**
   * 每次 refresh 遞增：同帳號下較新的讀取也要讓舊回應作廢（回應已讀完時 abort 不會讓它拋錯），
   * 也避免舊請求的 finally 清掉新請求的 loading。帳號守衛涵蓋不了這種同帳號的競態。
   */
  private revision = 0;
  private seen = new Set<string>();
  private primed = false;
  private latest = 0;
  /** 使用者正在看的網址；分頁在背景時為 `null`，沒在看就不算讀過。 */
  private readonly viewing = signal<string | null>(null);
  /** 正在標為已讀的目的地（`kind:id`），避免輪詢在回應前重送。 */
  private readonly readingTargets = new Set<string>();
  constructor() {
    const view = () =>
      this.viewing.set(document.visibilityState === 'visible' ? this.router.url : null);
    const navigated = this.router.events.subscribe((event) => {
      if (event instanceof NavigationEnd) view();
    });
    document.addEventListener('visibilitychange', view);
    view();
    // 看過就算讀過：通知指向的畫面正在眼前（剛打開，或在畫面上時才送達），不必再回通知中心點掉。
    effect(() => {
      const url = this.viewing();
      if (!url) return;
      const shown = this.items().filter(
        (item) => !item.readAt && showsTarget(url, item.target, item.version),
      );
      untracked(() => shown.forEach((item) => void this.readTarget(item.target)));
    });
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
        this.loadingMore.set(false);
        this.readError.set('');
        this.writing.error.set('');
        this.seen.clear();
        this.primed = false;
        this.latest = 0;
        if (owner) void this.poll(this.guard());
      });
    });
    inject(DestroyRef).onDestroy(() => {
      clearTimeout(this.timer);
      this.controller?.abort();
      navigated.unsubscribe();
      document.removeEventListener('visibilitychange', view);
    });
  }
  /** 帳號與登入世代都沒變；換人時 effect 已重設狀態，晚到的結果一律丟棄。 */
  private guard() {
    const owner = this.owner,
      current = this.session.auth.guard();
    return () => owner === this.owner && current();
  }
  /** 守衛在 effect 取得一次並沿用整條輪詢鏈，換帳號時舊鏈自行停止，不會與新鏈重疊。 */
  private async poll(current: () => boolean) {
    await this.refresh(false, true);
    if (current())
      this.timer = setTimeout(
        () => void this.poll(current),
        document.visibilityState === 'hidden' ? 30000 : 8000,
      );
  }
  async refresh(more = false, poll = false) {
    if (!this.owner) return;
    // 使用者觸發的讀取還在途中就跳過這次輪詢：輪詢會中止它，那次讀取的轉圈與結果都會被吞掉。
    if (poll && (this.loading() || this.loadingMore())) return;
    const revision = ++this.revision,
      current = this.guard();
    const valid = () => revision === this.revision && current();
    this.controller?.abort();
    this.controller = new AbortController();
    if (!poll) (more ? this.loadingMore : this.loading).set(true);
    const before = more && this.items().length ? this.items().at(-1)!.id : undefined;
    try {
      const page = await this.api.list(this.unreadOnly(), before, this.controller.signal);
      if (!valid()) return;
      for (const item of page.items) {
        // 自己發起的事完成時排入即已讀，仍要依個人設定跳出瀏覽器通知，所以不看 readAt。
        if (
          !more &&
          this.primed &&
          !this.seen.has(item.id) &&
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
      this.readError.set('');
    } catch (error) {
      if (valid() && !isCancellation(error)) this.readError.set(safeMessage(error));
    } finally {
      // 較新的讀取會中止這次並接手；兩個旗標都由最後那次清掉，不會卡在讀取中。
      if (valid()) {
        this.loading.set(false);
        this.loadingMore.set(false);
      }
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
  /** 這個寫入（`read:<id>`、`dismiss:<id>`、`all`）正在進行。 */
  isWorking(key: string) {
    return this.writing.pendingFor(key);
  }
  /** 標為已讀；成功回傳 true。失敗原因放在 `error`，列表維持原狀。 */
  async read(item: NotificationDto) {
    const outcome = await this.writing.run(() => this.api.read(item.id), {
      key: `read:${item.id}`,
    });
    if (!outcome.ok) return false;
    this.markedRead((row) => row.id === item.id);
    return true;
  }
  /** 開啟通知指向的畫面；到了畫面上就由「看過就算讀過」標為已讀，跳轉失敗則維持未讀。 */
  async activate(item: NotificationDto) {
    const destination = notificationUrl(item.target, item.version);
    if (!destination) return;
    this.close();
    this.layout.closeMobile();
    await this.router.navigateByUrl(destination);
  }
  /** 自動發生，不顯示轉圈或錯誤；失敗時下一次輪詢或換頁會再試。 */
  private async readTarget(target: NotificationTargetDto) {
    const key = `${target.kind}:${target.id}`;
    if (this.readingTargets.has(key)) return;
    this.readingTargets.add(key);
    const current = this.guard();
    try {
      await this.api.readTarget(target);
      if (current())
        this.markedRead((row) => row.target.kind === target.kind && row.target.id === target.id);
    } catch {
      /* 盡力而為：通知維持未讀，之後再試。 */
    } finally {
      this.readingTargets.delete(key);
    }
  }
  /** 伺服器已標為已讀後同步本地列表與未讀數，不必整頁重讀。 */
  private markedRead(which: (row: NotificationDto) => boolean) {
    const readAt = new Date().toISOString();
    let marked = 0;
    this.items.update((rows) =>
      rows
        .map((row) => {
          if (row.readAt || !which(row)) return row;
          marked++;
          return { ...row, readAt };
        })
        .filter((row) => !this.unreadOnly() || !row.readAt),
    );
    this.unread.update((value) => Math.max(0, value - marked));
  }
  async readAll() {
    const through = this.items()[0]?.createdAt;
    if (!through) return;
    await this.write('all', () => this.api.readThrough(through));
  }
  async dismiss(item: NotificationDto) {
    await this.write(`dismiss:${item.id}`, () => this.api.dismiss(item.id));
  }
  /** 寫入成功就重讀最新一頁，未讀數以伺服器為準；按鈕轉到重讀結束，結果才不會晚一拍出現。 */
  private async write(key: string, request: () => Promise<unknown>) {
    await this.writing.run(
      async ({ alive }) => {
        await request();
        if (alive()) await this.refresh();
      },
      { key },
    );
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
