import { afterNextRender, inject, Injectable, Injector, signal } from '@angular/core';
import { NavigationEnd, NavigationError, Router } from '@angular/router';
import { ThemeService } from '../preferences/theme-service';

const STORAGE_KEY = 'ai-nexus.identity-veil';
/** 遮罩的顯示狀態寫在根元素的 `data-identity-veil`，樣式在 `styles/identity-veil.scss`。 */
type VeilState = 'cover' | 'boot' | 'reveal';

/**
 * 換身分要整頁重新載入（清掉所有路由、提供者與進行中的檢視）。這層遮罩把兩次載入接成一個過渡：
 * 舊頁面淡入遮罩後才導向，新頁面由 `boot.js` 在第一幀就顯示同一層遮罩，第一頁畫好後才淡出。
 * 遮罩是 index.html 裡的 manual popover，開啟時進入 top layer，蓋得住正開著的對話框與選單。
 * 標籤（新身分的名稱）以 CSS 字串顯示，不寫進 DOM。
 */
@Injectable({ providedIn: 'root' })
export class IdentityVeil {
  private readonly theme = inject(ThemeService);
  private readonly router = inject(Router);
  private readonly injector = inject(Injector);
  private readonly root = document.documentElement;
  /** 這次載入是從換身分導向而來（`boot.js` 已蓋上遮罩）；在 App 建立前就已確定。 */
  readonly arrived = this.state() === 'boot';
  /** 遮罩還蓋著新頁面；開始淡出時轉為 false，讓頁面上的進場與遮罩退場接在一起。 */
  readonly covering = signal(this.arrived);

  /** 蓋上遮罩並交代下一頁接手；淡入結束才 resolve，之後呼叫端就能導向。 */
  async cover(label: string) {
    try {
      sessionStorage.setItem(STORAGE_KEY, JSON.stringify({ label, at: Date.now() }));
    } catch {
      // 寫不進去時下一頁照一般載入，只是少了接續的遮罩。
    }
    this.root.style.setProperty('--identity-veil-label', JSON.stringify(label));
    this.setState('cover');
    this.element()?.showPopover();
    // 背景分頁不跑動畫，animationend 不會來；減少動態時也沒有淡入可等。
    if (document.hidden || this.theme.reducedMotion()) return;
    await this.settled();
  }

  /** 第一次導覽畫好後淡出 `boot.js` 蓋上的遮罩；App 建立時呼叫一次。 */
  revealAfterFirstNavigation() {
    if (!this.arrived) return;
    const subscription = this.router.events.subscribe((event) => {
      if (!(event instanceof NavigationEnd || event instanceof NavigationError)) return;
      subscription.unsubscribe();
      afterNextRender(() => void this.reveal(), { injector: this.injector });
    });
  }

  private async reveal() {
    this.covering.set(false);
    if (this.state() !== 'boot') return;
    const element = this.element();
    // 已自行逾時退場（boot 的保險動畫跑完）就直接清掉；換成 reveal 會讓它從不透明重新淡出一次。
    const visible = !!element && getComputedStyle(element).visibility !== 'hidden';
    if (visible && !document.hidden && !this.theme.reducedMotion()) {
      this.setState('reveal');
      await this.settled();
      if (this.state() !== 'reveal') return;
    }
    delete this.root.dataset['identityVeil'];
    this.root.style.removeProperty('--identity-veil-label');
  }

  private element() {
    return document.querySelector<HTMLElement>('.identity-veil');
  }

  private state() {
    return this.root.dataset['identityVeil'] as VeilState | undefined;
  }

  private setState(state: VeilState) {
    this.root.dataset['identityVeil'] = state;
  }

  /** 等遮罩這一段動畫結束；元素不在或動畫沒有開始時，最晚一秒後放行。 */
  private settled() {
    const element = this.element();
    return new Promise<void>((resolve) => {
      let timer = 0;
      // 標籤的掃光是無限循環的偽元素動畫，事件也會冒到這個元素上，要排除。
      const done = (event?: AnimationEvent) => {
        if (event && (event.target !== element || event.pseudoElement)) return;
        clearTimeout(timer);
        element?.removeEventListener('animationend', done);
        resolve();
      };
      timer = window.setTimeout(done, 1000);
      element?.addEventListener('animationend', done);
    });
  }
}
