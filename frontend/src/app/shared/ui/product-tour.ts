import { DestroyRef, Injectable, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationStart, Router } from '@angular/router';
import type { Driver, DriveStep } from 'driver.js';
import { ThemeService } from '@core/preferences/theme-service';

export interface TourStep {
  target: string;
  title: string;
  description: string;
  side?: 'top' | 'right' | 'bottom' | 'left';
}
/** 條件不成立的步驟可寫成假值（`options.edit && { ... }`），由引擎略過。 */
export type TourSteps = readonly (TourStep | false | null | undefined)[];
export interface ProductTourDefinition {
  steps: TourSteps;
}
/** 定義中條件成立的步驟；是否在畫面上要到導覽開始時才知道。 */
export function tourSteps(definition: ProductTourDefinition) {
  return definition.steps.filter((step): step is TourStep => !!step);
}
const escape = (value: string) =>
  value.replace(
    /[&<>"']/g,
    (character) =>
      ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[character]!,
  );

const REPLAY_HINT = '右上角的問號圖示可隨時重播導覽。';

/** 延遲載入的導覽引擎；頁面提供內容，生命週期、動效與主題則共用。 */
@Injectable({ providedIn: 'root' })
export class ProductTour {
  private readonly themes = inject(ThemeService);
  private instance?: Driver;
  private release?: () => void;
  private definition?: ProductTourDefinition;
  private sequence = 0;
  private stylesheet?: Promise<void>;
  readonly loading = signal(false);
  readonly active = signal(false);
  constructor() {
    inject(Router)
      .events.pipe(takeUntilDestroyed())
      .subscribe((event) => {
        if (event instanceof NavigationStart) this.stop();
      });
    inject(DestroyRef).onDestroy(() => this.stop());
  }
  async start(definition: ProductTourDefinition) {
    this.stop();
    const sequence = ++this.sequence;
    this.definition = definition;
    const opener = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    this.loading.set(true);
    try {
      const [{ driver }] = await Promise.all([import('driver.js'), this.loadStyles()]);
      if (sequence !== this.sequence) return;
      // 模態 dialog 讓外面的內容變成 inert，但不會加上 [inert] 屬性，所以另外限制在最上層的模態內。
      const modal = [...document.querySelectorAll<HTMLDialogElement>('dialog:modal')].at(-1);
      const visible = (selector: string) =>
        [...document.querySelectorAll<HTMLElement>(selector)].find(
          (element) =>
            element.checkVisibility() &&
            !element.closest('[inert]') &&
            (!modal || modal.contains(element)),
        );
      const shown = tourSteps(definition).filter((step) => visible(step.target));
      const steps: DriveStep[] = shown.map((step, index) => ({
        element: () => visible(step.target)!,
        popover: {
          title: escape(step.title),
          // 不在畫面上的步驟會略過，因此重播提示放在最後一個顯示的步驟上。
          description: escape(
            index === shown.length - 1 ? step.description + REPLAY_HINT : step.description,
          ),
          side: step.side ?? 'top',
          align: 'center',
        },
      }));
      if (!steps.length) return;
      const open = 'dialog[open]';
      const layer = this.openLayer(
        opener?.closest<HTMLDialogElement>(open) ??
          modal ??
          visible(shown[0].target)!.closest<HTMLDialogElement>(open),
      );
      this.release = layer.release;
      this.instance = driver({
        steps,
        animate: !this.themes.reducedMotion(),
        duration: 320,
        smoothScroll: false,
        allowScroll: false,
        disableActiveInteraction: true,
        skipMissingElement: true,
        stagePadding: 6,
        stageRadius: 12,
        overlayOpacity: 0.58,
        overlayColor: '#101c28',
        popoverClass: 'nx-tour',
        showProgress: true,
        progressText: '{{current}} / {{total}}',
        nextBtnText: '下一步',
        prevBtnText: '上一步',
        doneBtnText: '開始使用',
        closeBtnLabel: '跳過導覽',
        // driver.js 只在目標超出視口時捲動；被 dialog 或主區的捲動容器裁掉的目標仍在視口內，要自己捲。
        onHighlightStarted: (element) => element?.scrollIntoView({ block: 'nearest' }),
        onPopoverRender: (popover, { index }) => {
          // 在定位與聚焦之前移入導覽層，之後才搬會丟失焦點。
          layer.element.append(popover.wrapper);
          const eyebrow = document.createElement('span');
          eyebrow.className = 'tour-eyebrow';
          eyebrow.textContent = 'AI NEXUS · 快速導覽';
          popover.wrapper.prepend(eyebrow);
          popover.wrapper.style.setProperty(
            '--tour-progress',
            String(((index ?? 0) + 1) / steps.length),
          );
          popover.wrapper.classList.toggle('tour-reduced-motion', this.themes.reducedMotion());
        },
        // 使用者關閉（Escape、跳過、完成、點遮罩）都先經過這裡；改由 stop() 收尾，
        // 第一個影格前關閉時 driver.js 不會呼叫 onDestroyed。
        onDestroyStarted: () => {
          this.stop();
          const stopped = this.sequence;
          requestAnimationFrame(() => {
            if (stopped === this.sequence && opener?.isConnected)
              opener.focus({ preventScroll: true });
          });
        },
      });
      this.active.set(true);
      this.instance.drive();
    } catch (error) {
      if (sequence === this.sequence) {
        this.stop();
        throw error;
      }
    } finally {
      if (sequence === this.sequence) this.loading.set(false);
    }
  }
  /** 指定 `definition` 時，只在執行中的正是它時才停止（觸發鈕隨對話框銷毀時用）。 */
  stop(definition?: ProductTourDefinition) {
    if (definition && definition !== this.definition) return;
    this.definition = undefined;
    ++this.sequence;
    this.instance?.destroy();
    this.instance = undefined;
    this.release?.();
    this.release = undefined;
    this.active.set(false);
    this.loading.set(false);
  }
  /**
   * driver.js 把遮罩與說明框掛在 body：模態 dialog 在 top layer 會蓋住它們，也讓它們變成 inert。
   * 因此兩者改放進一個 manual popover：它在 top layer 的最上面，且位於目標所在的 dialog 內而不受 inert
   * 影響；top layer 的包含區塊是視口，driver.js 的 fixed 座標不受 dialog 的 translate 影響。
   */
  private openLayer(dialog: HTMLDialogElement | null | undefined) {
    const element = document.createElement('div');
    element.className = 'nx-tour-layer';
    element.popover = 'manual';
    (dialog ?? document.body).append(element);
    element.showPopover();
    // 遮罩在第一個影格才建立，只能事後搬；MutationObserver 在繪製前執行，不會閃一下。
    const observer = new MutationObserver((records) => {
      for (const node of records.flatMap((record) => [...record.addedNodes]))
        if (node instanceof SVGSVGElement && node.classList.contains('driver-overlay'))
          element.prepend(node);
    });
    observer.observe(document.body, { childList: true });
    // driver.js 在 keyup 才關閉導覽；keydown 先在最外層攔下，dialog、抽屜與頁面的 Escape 就不會跟著動作。
    const escape = (event: KeyboardEvent) => {
      if (event.key !== 'Escape') return;
      event.preventDefault();
      event.stopImmediatePropagation();
    };
    const cancel = (event: Event) => event.preventDefault();
    const close = () => this.stop();
    // 只監聽 window 的捲動不夠：dialog 與頁面主區都是自己的捲動容器。
    const scroll = () => this.instance?.refresh();
    window.addEventListener('keydown', escape, true);
    document.addEventListener('scroll', scroll, { capture: true, passive: true });
    dialog?.addEventListener('cancel', cancel);
    dialog?.addEventListener('close', close);
    return {
      element,
      release: () => {
        observer.disconnect();
        window.removeEventListener('keydown', escape, true);
        document.removeEventListener('scroll', scroll, { capture: true });
        dialog?.removeEventListener('cancel', cancel);
        dialog?.removeEventListener('close', close);
        element.remove();
      },
    };
  }
  // driver.js 的樣式與本專案的覆寫只在導覽開始時載入；覆寫會接在 driver.css 之後，
  // 因此在相同特異性下會勝出。
  private loadStyles() {
    return (this.stylesheet ??= Promise.all(
      ['vendor/driver/driver.css', 'driver-tour.css'].map((href) => this.loadStylesheet(href)),
    ).then(
      () => undefined,
      (error: unknown) => {
        this.stylesheet = undefined;
        throw error;
      },
    ));
  }
  private loadStylesheet(href: string) {
    return new Promise<void>((resolve, reject) => {
      const link = document.createElement('link');
      link.rel = 'stylesheet';
      link.href = new URL(href, document.baseURI).href;
      link.onload = () => resolve();
      link.onerror = () => {
        link.remove();
        reject(new Error('Tour stylesheet could not be loaded'));
      };
      document.head.append(link);
    });
  }
}
