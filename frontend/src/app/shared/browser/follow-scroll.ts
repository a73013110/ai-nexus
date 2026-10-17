import { DestroyRef, inject, linkedSignal, type WritableSignal } from '@angular/core';
import { ThemeService } from '@core/preferences/theme-service';

export interface FollowScrollOptions {
  /** 捲動的元素；它可能隨視圖出現或消失。 */
  viewport: () => HTMLElement | undefined;
  /** 是否從開頭開始跟隨；所讀取的 signal 變化時會重新計算。 */
  initial?: () => boolean;
  /** 距離底部多少像素以內仍視為正在閱讀最新內容。 */
  threshold?: number;
}

export interface FollowScroll {
  /** 內容持續增長時，讓視圖維持在底部。 */
  readonly following: WritableSignal<boolean>;
  /** 內容增長：跟隨中時，下一幀捲到底部（每幀最多一次）。 */
  schedule(): void;
  /** 綁定到視口的 `(scroll)`：捲回底部會恢復跟隨，離開則暫停。 */
  scrolled(): void;
  /** 在目前的位置停止跟隨，例如要跳到內容的其他地方之前。 */
  pause(): void;
  /** 捲到底部並從那裡開始跟隨；`smooth` 會遵守減少動態效果。 */
  toLatest(smooth?: boolean): void;
  /** 記錄程式設定的位置，讓下一次捲動事件可以與它比較。 */
  mark(): void;
}

/**
 * 串流視圖（聊天回答、日誌）的貼底功能：新內容會讓最新一行留在視野中，直到讀者往上捲；
 * 捲回底部時會恢復。在 injection context 中呼叫；尚未完成的幀由呼叫端結束。
 */
export function followScroll(options: FollowScrollOptions): FollowScroll {
  const theme = inject(ThemeService);
  const threshold = options.threshold ?? 96;
  const following = linkedSignal(options.initial ?? (() => true));
  let frame = 0;
  let lastTop = 0;
  const cancel = () => {
    cancelAnimationFrame(frame);
    frame = 0;
  };
  const toLatest = (smooth = true) => {
    const view = options.viewport();
    following.set(true);
    view?.scrollTo({
      top: view.scrollHeight,
      behavior: smooth ? theme.scrollBehavior() : 'instant',
    });
  };
  inject(DestroyRef).onDestroy(cancel);
  return {
    following,
    schedule() {
      if (!following() || frame) return;
      frame = requestAnimationFrame(() => {
        frame = 0;
        if (following()) toLatest(false);
      });
    },
    scrolled() {
      const view = options.viewport();
      if (!view) return;
      const near = view.scrollHeight - view.scrollTop - view.clientHeight < threshold;
      // 向上的平滑跳動從接近底部開始；第一個事件不可以因此恢復跟隨。
      following.set(near && (following() || view.scrollTop > lastTop));
      lastTop = view.scrollTop;
      if (!near) cancel();
    },
    pause() {
      following.set(false);
      cancel();
      lastTop = options.viewport()?.scrollTop ?? 0;
    },
    toLatest,
    mark() {
      lastTop = options.viewport()?.scrollTop ?? 0;
    },
  };
}
