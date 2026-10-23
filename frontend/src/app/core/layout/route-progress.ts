import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { Router } from '@angular/router';

/** 換頁超過這麼久才顯示；快的換頁不閃一條線。 */
export const ROUTE_PROGRESS_DELAY = 200;

/**
 * 換頁進度線：延遲載入的路由與 guard（例如等 `/me`）期間，頁面頂端的一條細線，不遮內容也不擋操作。
 * `Router.currentNavigation()` 在 NavigationStart 設定、在 End／Cancel／Error 清掉，所以載入 chunk
 * 與 guard 等待都算在內。沒有真實進度，線慢慢長到九成；減少動態時直接停在那裡。
 */
@Component({
  selector: 'nx-route-progress',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      position: fixed;
      inset: 0 0 auto;
      z-index: 100;
      height: 2px;
      pointer-events: none;
    }
    span {
      display: block;
      height: 100%;
      background: var(--accent);
      transform-origin: left;
      animation: route-progress-grow 8s var(--ease) forwards;
    }
  `,
  template: `@if (shown()) {
    <span role="progressbar" aria-label="正在載入頁面…"></span>
  }`,
})
export class RouteProgress {
  private readonly router = inject(Router);
  private readonly navigating = computed(() => this.router.currentNavigation() !== null);
  readonly shown = signal(false);

  constructor() {
    let timer: ReturnType<typeof setTimeout> | undefined;
    inject(DestroyRef).onDestroy(() => clearTimeout(timer));
    effect(() => {
      clearTimeout(timer);
      if (!this.navigating()) {
        this.shown.set(false);
        return;
      }
      timer = setTimeout(() => this.shown.set(true), ROUTE_PROGRESS_DELAY);
    });
  }
}
