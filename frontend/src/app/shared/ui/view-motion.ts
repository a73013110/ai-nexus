import {
  afterRenderEffect,
  DestroyRef,
  Directive,
  ElementRef,
  inject,
  Injector,
  input,
} from '@angular/core';
import { ThemeService } from '@core/preferences/theme-service';
import { motionTiming } from '@shared/browser/motion-tokens';

/** 版面大小，不受對話框自身開啟／關閉縮放的影響。 */
function size(element: HTMLElement) {
  return { width: element.offsetWidth, height: element.offsetHeight };
}

/** 顯示中：開啟的對話框或 popover；關閉中的仍會繪製，但不再計入。 */
function shown(element: HTMLElement) {
  return element instanceof HTMLDialogElement ? element.open : element.matches(':popover-open');
}

const frames = new WeakMap<Element, FrameMotion>();

/** 元素最近的框架（若有）。 */
function frameAround(element: Element) {
  for (let node = element.parentElement; node; node = node.parentElement) {
    const frame = frames.get(node);
    if (frame) return frame;
  }
  return undefined;
}

/**
 * 有鍵的面板過場會讓分頁與其鍵盤焦點保持不動。在框架內（每個 `nxCompactDialog`，見 `FrameMotion`），
 * 框架會同時跟隨新視圖的大小。
 */
@Directive({ selector: '[nxViewMotion]' })
export class ViewMotion {
  readonly view = input.required<unknown>({ alias: 'nxViewMotion' });
  private readonly element = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  private readonly theme = inject(ThemeService);
  private animation?: Animation;
  private previous: unknown;
  private initialized = false;
  constructor() {
    afterRenderEffect(() => {
      const view = this.view(),
        reduced = this.theme.reducedMotion();
      const changed = this.initialized && view !== this.previous;
      this.previous = view;
      this.initialized = true;
      this.animation?.cancel();
      if (changed) frameAround(this.element)?.morph();
      if (!changed || reduced || !this.element.getClientRects().length) return;
      this.animation = this.element.animate(
        [
          { opacity: 0, transform: 'translateY(6px) scale(.985)' },
          { opacity: 1, transform: 'none' },
        ],
        motionTiming(this.element, '--motion-panel'),
      );
    });
    inject(DestroyRef).onDestroy(() => this.animation?.cancel());
  }
}

/**
 * 大小跟著內容走的對話框或 popover：內部視圖變更（`ViewMotion`）或擁有者在渲染後呼叫 `morph()` 時，
 * 會從原本的大小過渡到新大小，而不是直接跳動。寬高來自內容與 CSS，因此框架不需要固定高度就能
 * 保持分頁不動。
 * 只在顯示時記住排版後的大小，關閉時遺忘，因此開啟時不會有動畫；原生 dialog 仍負責焦點、Escape
 * 與視口限制。
 */
@Directive({
  selector: '[nxFrameMotion]',
  host: { '(close)': 'forget()', '(toggle)': 'toggled($event)' },
})
export class FrameMotion {
  private readonly element = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  /** 進行 morph 時讀取：每個對話框都是框架，包括單元測試中沒有版面的對話框。 */
  private readonly injector = inject(Injector);
  private animation?: Animation;
  private rectangle?: { width: number; height: number };
  private readonly observer =
    typeof ResizeObserver === 'undefined'
      ? undefined
      : new ResizeObserver(() => {
          if (shown(this.element) && this.animation?.playState !== 'running')
            this.rectangle = size(this.element);
        });
  constructor() {
    if (this.observer) {
      this.observer.observe(this.element);
      frames.set(this.element, this);
    }
    inject(DestroyRef).onDestroy(() => {
      this.observer?.disconnect();
      this.animation?.cancel();
    });
  }
  toggled(event: Event) {
    if ((event as ToggleEvent).newState === 'closed') this.forget();
  }
  forget() {
    this.animation?.cancel();
    this.rectangle = undefined;
  }
  /** 在改變內容的渲染之後呼叫；之前的大小由觀察者提供。 */
  morph() {
    if (!this.observer) return;
    if (!shown(this.element)) return this.forget();
    const before = this.animation?.playState === 'running' ? size(this.element) : this.rectangle;
    this.animation?.cancel();
    const after = size(this.element);
    this.rectangle = after;
    if (
      this.injector.get(ThemeService).reducedMotion() ||
      !before?.width ||
      !before.height ||
      (Math.abs(before.width - after.width) < 1 && Math.abs(before.height - after.height) < 1)
    )
      return;
    this.animation = this.element.animate(
      [
        { width: `${before.width}px`, height: `${before.height}px` },
        { width: `${after.width}px`, height: `${after.height}px` },
      ],
      motionTiming(this.element, '--motion-panel'),
    );
  }
}
