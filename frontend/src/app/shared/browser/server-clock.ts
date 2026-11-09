import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';

/**
 * 每秒跳一次的共用時鐘，倒數與經過時間（`TickingTime`）、時間軸的現在線（`TimeLanes`）都讀它，同一頁的數字因此同時跳。
 * 以 `sync()` 校正到伺服器時間：下次執行時間是伺服器排的，客戶端時鐘快慢幾分鐘時倒數仍對得上。
 * 只在有元件 `watch()` 且分頁可見時計時；回到前景立刻補上一跳，不讓背景分頁每秒喚醒。
 */
@Injectable({ providedIn: 'root' })
export class ServerClock {
  private readonly skew = signal(0);
  private readonly tick = signal(Date.now());
  private watchers = 0;
  private timer: ReturnType<typeof setTimeout> | undefined;
  private readonly visibility = () => this.schedule();

  /** 校正後的現在時間（毫秒）；在有元件 `watch()` 時每秒更新。 */
  readonly now = computed(() => this.tick() + this.skew());

  /** 以伺服器回應裡的時間校正；網路延遲造成的誤差在一秒內，倒數以秒為單位可以忽略。 */
  sync(serverTime: string, receivedAt = Date.now()) {
    const server = Date.parse(serverTime);
    if (Number.isNaN(server)) return;
    this.skew.set(server - receivedAt);
    this.tick.set(Date.now());
  }

  /** 在呼叫端的生命週期內讓時鐘跳動（需在注入環境呼叫）；最後一個離開時停止。 */
  watch(destroyRef = inject(DestroyRef)) {
    if (this.watchers++ === 0) {
      document.addEventListener('visibilitychange', this.visibility);
      this.schedule();
    }
    destroyRef.onDestroy(() => {
      if (--this.watchers > 0) return;
      clearTimeout(this.timer);
      this.timer = undefined;
      document.removeEventListener('visibilitychange', this.visibility);
    });
  }

  /** 對齊到下一個整秒，各個倒數的秒數同時改變。 */
  private schedule() {
    clearTimeout(this.timer);
    this.timer = undefined;
    if (this.watchers === 0 || document.visibilityState === 'hidden') return;
    const now = Date.now();
    this.tick.set(now);
    this.timer = setTimeout(() => this.schedule(), 1000 - ((now + this.skew()) % 1000) + 5);
  }
}
