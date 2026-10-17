import { ApplicationRef, inject, Injectable } from '@angular/core';
import {
  withViewTransitions,
  type ActivatedRouteSnapshot,
  type ViewTransitionInfo,
} from '@angular/router';
import { ThemeService } from '../preferences/theme-service';

/**
 * 以 View Transitions API 做共享元素的 morph。靜態配對在 `motion.scss` 的
 * `:root[data-morphing]` 下命名，
 * 所以名稱（以及它們建立的堆疊脈絡）只在轉場進行時存在；一次性的配對由起點元素預先武裝，
 * 並落在標有 `data-morph="<name>"` 的元素上。沒有該 API 或啟用了減少動態效果時，直接套用更新。
 */
@Injectable({ providedIn: 'root' })
export class ViewMorph {
  private readonly app = inject(ApplicationRef);
  private readonly theme = inject(ThemeService);
  private armed: { name: string; element: HTMLElement } | null = null;
  private running = 0;
  private morphs = 0;
  private kind: { name: string; origin?: { x: number; y: number } } | null = null;
  private settled: Promise<unknown> = Promise.resolve();

  /** 正在進行 morph；路由轉場會讓位，不會中途打斷它。 */
  get active() {
    return this.morphs > 0;
  }

  get enabled() {
    return 'startViewTransition' in document && !this.theme.reducedMotion();
  }

  /**
   * 下一次轉場（路由切換或 `run`）會以 `name` 編排：執行中會暴露為 `:root[data-morph-kind]`，
   * `origin`（視窗像素）是 `--morph-x` 與 `--morph-y`，半徑 `--morph-r` 則延伸到離它最遠的角落。
   */
  next(name: string, origin?: { x: number; y: number }) {
    this.kind = { name, origin };
  }

  /** 轉場不會執行時，清掉待處理的 kind，避免滲入之後的轉場。 */
  discard() {
    this.kind = null;
  }

  /** 目前為止開始的 `run` morph 結束後才會 resolve。 */
  whenSettled() {
    return this.settled;
  }

  /** 轉場期間標記文件，讓 CSS 的名稱生效。 */
  track(transition: ViewTransition) {
    const root = document.documentElement;
    const kind = this.kind;
    this.kind = null;
    this.running++;
    root.dataset['morphing'] = '';
    if (kind) {
      root.dataset['morphKind'] = kind.name;
      if (kind.origin) {
        const { x: pointX, y: pointY } = kind.origin;
        const reach = Math.hypot(
          Math.max(pointX, innerWidth - pointX),
          Math.max(pointY, innerHeight - pointY),
        );
        root.style.setProperty('--morph-x', `${pointX}px`);
        root.style.setProperty('--morph-y', `${pointY}px`);
        root.style.setProperty('--morph-r', `${Math.ceil(reach)}px`);
      }
    }
    // 較新的轉場會略過這一次；這次的 promise 會被 reject，屬預期行為。
    transition.ready.catch(() => undefined);
    return transition.finished
      .catch(() => undefined)
      .finally(() => {
        if (--this.running === 0) delete root.dataset['morphing'];
        if (kind && root.dataset['morphKind'] === kind.name) {
          delete root.dataset['morphKind'];
          for (const name of ['--morph-x', '--morph-y', '--morph-r'])
            root.style.removeProperty(name);
        }
      });
  }

  /** 下一次針對 `name` 的 `run` 從這個元素開始。 */
  arm(name: string, element: HTMLElement) {
    this.armed = { name, element };
  }

  /**
   * 在 view transition 中套用 `update`。帶 `name` 時，只有該名稱的起點仍在頁面上才會做動畫；
   * 否則直接套用更新。
   */
  async run(update: () => void, name?: string) {
    const source = this.armed?.name === name ? this.armed?.element : null;
    if (name) this.armed = null;
    if (!this.enabled || (name && !source?.isConnected)) {
      this.discard();
      return update();
    }
    if (name && source) source.style.viewTransitionName = name;
    let target: HTMLElement | null = null;
    this.morphs++;
    const transition = document.startViewTransition(() => {
      update();
      this.app.tick();
      if (!name || !source) return;
      source.style.viewTransitionName = '';
      target = document.querySelector<HTMLElement>(`[data-morph="${name}"]`);
      if (target) target.style.viewTransitionName = name;
    });
    this.settled = this.track(transition).finally(() => {
      this.morphs--;
      if (source) source.style.viewTransitionName = '';
      if (target) target.style.viewTransitionName = '';
    });
    await transition.updateCallbackDone.catch(() => undefined);
  }
}

const reusedPage = (from: ActivatedRouteSnapshot, to: ActivatedRouteSnapshot) => {
  let first: ActivatedRouteSnapshot | null = from;
  let second: ActivatedRouteSnapshot | null = to;
  while (first?.firstChild && second?.firstChild)
    [first, second] = [first.firstChild, second.firstChild];
  return !!first?.routeConfig && first.routeConfig === second?.routeConfig;
};

/**
 * 路由的 view transition：頁面交叉淡入，具名元素（品牌、導覽膠囊）則 morph。
 * Angular 會在此功能啟用後延遲一個 task 才切換到新路由（略過時也一樣），
 * 所以點擊後立刻操作的程式碼與測試，要等 URL 真的變了才繼續，而不是假設已經切換。
 * 減少動態效果、`ViewMorph` 執行中，或頁面元件被重用（`/chat` ↔ `/chat/:id`）時會略過，
 * 由頁面自己的狀態變化決定要 morph 什麼。
 */
export const withNexusViewTransitions = () =>
  withViewTransitions({
    skipInitialTransition: true,
    onViewTransitionCreated: ({ transition, from, to }: ViewTransitionInfo) => {
      const morph = inject(ViewMorph);
      if (!morph.enabled || morph.active || reusedPage(from, to)) {
        morph.discard();
        transition.skipTransition();
      } else void morph.track(transition);
    },
  });
