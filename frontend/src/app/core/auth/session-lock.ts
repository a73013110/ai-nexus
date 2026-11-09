import { DestroyRef, Injectable, computed, effect, inject, untracked } from '@angular/core';
import { ApiClient } from '../api/api-client';
import type { AuthSessionDto, SessionLifetimeDto } from '../api/schema';
import { WorkspaceApi } from '../api/workspace-api';
import { ApiError } from '../errors/safe-errors';
import { INTERACTIONS } from '../monitoring/interactions';
import { AuthService } from './auth-service';

const REPORT_EVERY_MS = 60_000;
type LockMessage = 'locked' | 'unlocked' | { active: number };

/**
 * 閒置與重新驗證鎖定的前端狀態。是否鎖定一律以伺服器回傳的 `lifetime.lockedBy` 為準：計時器只決定「何時去問」，
 * 背景分頁的計時器被瀏覽器節流時頂多晚一點顯示，資料早就被伺服器擋下了。
 * 只由 `SessionLockScreen` 注入，隨它延後載入；在此之前收到 `session_locked` 時由 `ApiClient` 自行關閘門等待。
 */
@Injectable({ providedIn: 'root' })
export class SessionLock {
  private readonly auth = inject(AuthService);
  private readonly transport = inject(ApiClient);
  private readonly api = inject(WorkspaceApi);
  private readonly lifetime = computed(() => this.auth.session()?.lifetime ?? null);
  /** 鎖定原因（`idle`、`reauthentication`）；未鎖定為 `null`。 */
  readonly reason = computed(() => this.lifetime()?.lockedBy ?? null);
  private deadline = Infinity;
  private deadlineTimer?: ReturnType<typeof setTimeout>;
  private reportTimer?: ReturnType<typeof setTimeout>;
  private lastReport = 0;
  private readonly channel =
    typeof BroadcastChannel === 'undefined' ? null : new BroadcastChannel('ai-nexus-session-lock');

  constructor() {
    effect(() => {
      const reason = this.reason();
      untracked(() => {
        if (reason) {
          this.transport.lock();
          this.channel?.postMessage('locked' satisfies LockMessage);
        } else this.transport.unlock();
      });
    });
    effect(() => {
      if (this.transport.locked() && !untracked(this.reason)) void this.confirm();
    });
    effect(() => {
      const lifetime = this.lifetime();
      untracked(() => this.arm(lifetime));
    });
    if (this.channel)
      this.channel.onmessage = (event: MessageEvent<LockMessage>) => this.received(event.data);
    for (const name of INTERACTIONS)
      document.addEventListener(name, this.interacted, { passive: true });
    document.addEventListener('visibilitychange', this.visibility);
    inject(DestroyRef).onDestroy(() => {
      clearTimeout(this.deadlineTimer);
      clearTimeout(this.reportTimer);
      this.channel?.close();
      for (const name of INTERACTIONS) document.removeEventListener(name, this.interacted);
      document.removeEventListener('visibilitychange', this.visibility);
    });
  }

  /** 驗證密碼。成功後由呼叫端在成功動效之後呼叫 `finish`，閘門才打開。 */
  verify(password: string) {
    return this.api.unlock(password);
  }

  finish(session: AuthSessionDto) {
    this.lastReport = Date.now();
    this.auth.adopt(session);
    this.channel?.postMessage('unlocked' satisfies LockMessage);
  }

  /**
   * 操作節流成每分鐘最多一次回報：第一次立刻送，之後補在時間窗結束時，伺服器的活動時間最多差一個窗。
   * 窗不超過閒置期限的一半：期限短到一分鐘時，等滿一分鐘才回報，正在操作的人也會被鎖。
   */
  private readonly interacted = () => {
    const lifetime = this.lifetime();
    if (this.reportTimer !== undefined || !lifetime || lifetime.lockedBy) return;
    const every = Math.min(REPORT_EVERY_MS, lifetime.idleLockMinutes * 30_000);
    const wait = Math.max(0, this.lastReport + every - Date.now());
    this.reportTimer = setTimeout(() => {
      this.reportTimer = undefined;
      void this.report();
    }, wait);
  };

  private readonly visibility = () => {
    if (document.visibilityState === 'visible' && Date.now() >= this.deadline) void this.ask();
  };

  private async report() {
    this.lastReport = Date.now();
    try {
      const session = await this.api.reportActivity();
      this.auth.adopt(session);
      if (session.lifetime)
        this.channel?.postMessage({
          active: session.lifetime.secondsUntilLock,
        } satisfies LockMessage);
    } catch (error) {
      // 回報失敗不打擾使用者；401 代表期限已到（鎖定或過期），改問一次目前狀態。
      if (error instanceof ApiError && error.status === 401) await this.ask();
    }
  }

  private arm(lifetime: SessionLifetimeDto | null) {
    if (!lifetime || lifetime.lockedBy) {
      clearTimeout(this.deadlineTimer);
      this.deadline = Infinity;
      return;
    }
    this.schedule(lifetime.secondsUntilLock);
  }

  private schedule(seconds: number) {
    clearTimeout(this.deadlineTimer);
    this.deadline = Date.now() + seconds * 1000;
    this.deadlineTimer = setTimeout(() => void this.ask(), seconds * 1000);
  }

  /** 期限到了或其他分頁有變動：只問伺服器，不自行判定。連不上時等下一次操作或切回分頁。 */
  private ask() {
    return this.auth.load().catch(() => null);
  }

  /** 某個請求被回 `session_locked`：確認後由 `reason` 接手；伺服器說沒鎖（別的分頁剛解鎖）就放行重送。 */
  private async confirm() {
    const session = await this.ask();
    if (!session?.lifetime?.lockedBy) this.transport.unlock();
  }

  private received(message: LockMessage) {
    if (typeof message === 'object') {
      this.lastReport = Date.now();
      if (!this.reason()) this.schedule(message.active);
      return;
    }
    // 雙方都已是同一狀態時不必再問，兩個分頁才不會互相觸發。
    if ((message === 'locked') !== !!this.reason()) void this.ask();
  }
}
