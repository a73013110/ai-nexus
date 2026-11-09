import { DestroyRef, Injectable, computed, effect, inject, signal, untracked } from '@angular/core';
import { onVisibilityChange } from '@shared/browser/page-visibility';
import { ToastService } from '@shared/ui/toast-service';
import { ApiClient } from '../api/api-client';
import type { AuthSessionDto, SessionLifetimeDto } from '../api/schema';
import { WorkspaceApi } from '../api/workspace-api';
import { ApiError } from '../errors/safe-errors';
import { INTERACTIONS } from '../monitoring/interactions';
import { AuthService } from './auth-service';

const REPORT_EVERY_MS = 60_000;
const WARN_BEFORE_SECONDS = 120;
type LockMessage = 'locked' | 'unlocked' | { active: number };

/** 距離下次鎖定的倒數。`by` 為 `reauthentication` 時操作不能延長，只能重新驗證。 */
export interface SessionCountdown {
  seconds: number;
  by: 'idle' | 'reauthentication';
  /** 剩多少秒開始提醒；`seconds` 不大於它就在提醒期間。 */
  warnFrom: number;
}

const sameCountdown = (a: SessionCountdown | null, b: SessionCountdown | null) =>
  a?.seconds === b?.seconds && a?.by === b?.by && a?.warnFrom === b?.warnFrom;

const clockText = (total: number) => {
  const minutes = Math.floor(total / 60);
  const seconds = String(total % 60).padStart(2, '0');
  return minutes < 60
    ? `${minutes}:${seconds}`
    : `${Math.floor(minutes / 60)}:${String(minutes % 60).padStart(2, '0')}:${seconds}`;
};

/**
 * 鎖定的前端狀態與倒數。是否鎖定一律以伺服器回傳的 `lifetime.lockedBy` 為準：倒數只負責顯示與「何時去問」，
 * 背景分頁的計時器被瀏覽器節流時頂多晚一點顯示，資料早就被伺服器擋下了。
 */
@Injectable({ providedIn: 'root' })
export class SessionLock {
  private readonly auth = inject(AuthService);
  private readonly transport = inject(ApiClient);
  private readonly api = inject(WorkspaceApi);
  private readonly toasts = inject(ToastService);
  private readonly lifetime = computed(() => this.auth.session()?.lifetime ?? null);
  /** 鎖定原因（`idle`、`reauthentication`、`manual`）；未鎖定為 `null`。 */
  readonly reason = computed(() => this.lifetime()?.lockedBy ?? null);
  /** 測試身分不能解鎖（輸入的是被測者的密碼），到期是回到管理者，所以不顯示倒數也不能手動鎖定。 */
  private readonly canLock = computed(() => this.lockable() && !this.reason());
  private readonly remaining = signal<SessionCountdown | null>(null, { equal: sameCountdown });
  readonly countdown = this.remaining.asReadonly();
  /** 可不可能鎖定（鎖定中也算）：帳號列的鎖頭憑這個留著，鎖定、解鎖的過程中版面才不會跳。 */
  readonly lockable = computed(() => !!this.lifetime() && !this.auth.session()?.testing);
  /** 帳號列與倒數島的文字；不能鎖定時為 `null`。 */
  readonly statusText = computed(() => {
    if (this.reason()) return '已鎖定';
    const value = this.remaining();
    if (!value) return null;
    return `${clockText(value.seconds)} ${value.by === 'idle' ? '後鎖定' : '後需要驗證'}`;
  });
  /** 快到期了：帳號列變色、頂端出現倒數島。 */
  readonly warning = computed(() => {
    const value = this.remaining();
    return !!value && value.seconds <= value.warnFrom;
  });
  /** 伺服器回報的最近期限與重新驗證期限（毫秒時間戳）。 */
  private lockAt = Infinity;
  private reauthenticateAt = Infinity;
  /** 回報有節流，本機的操作先算進倒數；回報一定晚於操作，伺服器的期限不會早於這個推算值。 */
  private lastInteraction = 0;
  private timer?: ReturnType<typeof setTimeout>;
  private reportTimer?: ReturnType<typeof setTimeout>;
  private lastReport = 0;
  private reporting: Promise<unknown> = Promise.resolve();
  private locking = false;
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
      this.canLock();
      untracked(() => this.arm(lifetime));
    });
    if (this.channel)
      this.channel.onmessage = (event: MessageEvent<LockMessage>) => this.received(event.data);
    for (const name of INTERACTIONS)
      document.addEventListener(name, this.interacted, { passive: true });
    onVisibilityChange(this.visibility);
    inject(DestroyRef).onDestroy(() => {
      clearTimeout(this.timer);
      clearTimeout(this.reportTimer);
      this.channel?.close();
      for (const name of INTERACTIONS) document.removeEventListener(name, this.interacted);
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

  /** 手動鎖定。要等進行中的活動回報結束：它的回應會換發 cookie，晚於鎖定抵達就把鎖定蓋掉了。 */
  async lockNow() {
    if (this.locking || !this.canLock()) return;
    this.locking = true;
    this.cancelReport();
    try {
      await this.reporting.catch(() => null);
      this.auth.adopt(await this.api.lockSession());
    } catch {
      this.toasts.show({ tone: 'danger', title: '鎖定未完成，請再試一次。' });
    } finally {
      this.locking = false;
    }
  }

  /** 倒數島的「繼續使用」：立刻回報，不等節流窗，按完就確定延長了。 */
  keepActive() {
    this.cancelReport();
    this.lastInteraction = Date.now();
    this.tick();
    void this.report();
  }

  /**
   * 操作節流成每分鐘最多一次回報：第一次立刻送，之後補在時間窗結束時，伺服器的活動時間最多差一個窗。
   * 窗不超過閒置期限的一半：期限短到一分鐘時，等滿一分鐘才回報，正在操作的人也會被鎖。
   */
  private readonly interacted = () => {
    const lifetime = this.lifetime();
    if (!lifetime || lifetime.lockedBy) return;
    this.lastInteraction = Date.now();
    this.tick();
    if (this.reportTimer !== undefined) return;
    const every = Math.min(REPORT_EVERY_MS, lifetime.idleLockMinutes * 30_000);
    const wait = Math.max(0, this.lastReport + every - Date.now());
    this.reportTimer = setTimeout(() => {
      this.reportTimer = undefined;
      void this.report();
    }, wait);
  };

  private readonly visibility = () => {
    if (document.visibilityState === 'visible') this.tick();
  };

  private cancelReport() {
    clearTimeout(this.reportTimer);
    this.reportTimer = undefined;
  }

  private async report() {
    this.lastReport = Date.now();
    try {
      // 補送的回報晚於操作，附上經過的時間：伺服器若以收到當下起算，倒數會在沒有操作時自己跳回去。
      const reported = this.api.reportActivity(
        Math.ceil((this.lastReport - this.lastInteraction) / 1000),
      );
      this.reporting = reported;
      const session = await reported;
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
    const unlocked = lifetime && !lifetime.lockedBy;
    this.lockAt = unlocked ? Date.now() + lifetime.secondsUntilLock * 1000 : Infinity;
    this.reauthenticateAt = unlocked
      ? Date.now() + lifetime.secondsUntilReauthentication * 1000
      : Infinity;
    if (!unlocked) this.lastInteraction = 0;
    this.tick();
  }

  /** 唯一的計時器：對齊整秒更新倒數，到期就問伺服器。連不上時停在 0，等下一次操作或切回分頁。 */
  private readonly tick = () => {
    clearTimeout(this.timer);
    const lifetime = this.lifetime();
    if (!lifetime || lifetime.lockedBy) {
      this.remaining.set(null);
      return;
    }
    const idleAt = Math.max(this.lockAt, this.lastInteraction + lifetime.idleLockMinutes * 60_000);
    const deadline = Math.min(idleAt, this.reauthenticateAt);
    const left = Math.max(0, deadline - Date.now());
    this.publish(lifetime, deadline, left);
    if (left === 0) void this.ask();
    else this.timer = setTimeout(this.tick, left % 1000 || 1000);
  };

  private publish(lifetime: SessionLifetimeDto, deadline: number, left: number) {
    if (!this.canLock()) {
      this.remaining.set(null);
      return;
    }
    const by = deadline >= this.reauthenticateAt ? 'reauthentication' : 'idle';
    // 期限很短時（閒置設一分鐘）固定兩分鐘就會一直提醒，最多只提醒後半段。
    const warnFrom =
      by === 'idle'
        ? Math.min(WARN_BEFORE_SECONDS, lifetime.idleLockMinutes * 30)
        : WARN_BEFORE_SECONDS;
    this.remaining.set({ seconds: Math.ceil(left / 1000), by, warnFrom });
  }

  /** 期限到了或其他分頁有變動：只問伺服器，不自行判定。 */
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
      if (!this.reason()) {
        this.lockAt = Date.now() + message.active * 1000;
        this.tick();
      }
      return;
    }
    // 雙方都已是同一狀態時不必再問，兩個分頁才不會互相觸發。
    if ((message === 'locked') !== !!this.reason()) void this.ask();
  }
}
