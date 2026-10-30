import {
  ApplicationRef,
  createComponent,
  DestroyRef,
  EnvironmentInjector,
  Injectable,
  inject,
  signal,
  type ComponentRef,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationStart, Router } from '@angular/router';
import type { PopoverSide } from '@shared/browser/popover-position';
import type { ProductTourOverlay } from './product-tour-overlay';

export interface TourStep {
  target: string;
  title: string;
  description: string;
  side?: PopoverSide;
}
/** 條件不成立的步驟可寫成假值（`options.edit && { ... }`），由引擎略過。 */
export type TourSteps = readonly (TourStep | false | null | undefined)[];
export interface ProductTourDefinition {
  steps: TourSteps;
}
/** 導覽層實際播放的一步：開始時已確認在畫面上；目標每次定位重新查找，元素重繪後仍找得到。 */
export interface ShownTourStep {
  target: () => HTMLElement | undefined;
  title: string;
  description: string;
  side: PopoverSide;
}
/** 定義中條件成立的步驟；是否在畫面上要到導覽開始時才知道。 */
export function tourSteps(definition: ProductTourDefinition) {
  return definition.steps.filter((step): step is TourStep => !!step);
}

const REPLAY_HINT = '\n右上角的問號圖示可隨時重播導覽。';

/** 操作導覽：頁面提供步驟，導覽層（延遲載入）負責畫面、動效與鍵盤；同一時間只有一個導覽。 */
@Injectable({ providedIn: 'root' })
export class ProductTour {
  private readonly app = inject(ApplicationRef);
  private readonly injector = inject(EnvironmentInjector);
  private overlay?: ComponentRef<ProductTourOverlay>;
  private release?: () => void;
  private definition?: ProductTourDefinition;
  private sequence = 0;
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
      const { ProductTourOverlay } = await import('./product-tour-overlay');
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
      if (!shown.length) return;
      const steps: ShownTourStep[] = shown.map((step, index) => ({
        target: () => visible(step.target),
        title: step.title,
        // 不在畫面上的步驟會略過，因此重播提示放在最後一個顯示的步驟上。
        description: index === shown.length - 1 ? step.description + REPLAY_HINT : step.description,
        side: step.side ?? 'top',
      }));
      const open = 'dialog[open]';
      const dialog =
        opener?.closest<HTMLDialogElement>(open) ??
        modal ??
        visible(shown[0].target)!.closest<HTMLDialogElement>(open);
      // 導覽層是 manual popover：在 top layer 的最上面，放在目標所在的 dialog 內才不受模態 inert 影響；
      // top layer 以視口為包含區塊，不受 dialog 的 translate 影響。
      const host = document.createElement('nx-product-tour');
      (dialog ?? document.body).append(host);
      const overlay = createComponent(ProductTourOverlay, {
        environmentInjector: this.injector,
        hostElement: host,
      });
      overlay.setInput('steps', steps);
      overlay.instance.closed.subscribe(() => this.dismiss(opener));
      this.app.attachView(overlay.hostView);
      overlay.changeDetectorRef.detectChanges();
      this.overlay = overlay;
      this.release = this.guard(dialog);
      this.active.set(true);
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
    this.release?.();
    this.release = undefined;
    const overlay = this.overlay;
    this.overlay = undefined;
    if (overlay) {
      // 退場期間導覽層已不接操作；動畫結束才移除，減少動態或沒有動畫時立即移除。
      const remove = () => {
        overlay.destroy();
        overlay.location.nativeElement.remove();
      };
      const leaving = overlay.instance.leave();
      if (leaving.length)
        void Promise.allSettled(leaving.map((item) => item.finished)).then(remove);
      else remove();
    }
    this.active.set(false);
    this.loading.set(false);
  }
  /** 使用者關閉（Escape、跳過、完成、點遮罩）：結束後焦點回到開啟導覽的元素。 */
  private dismiss(opener: HTMLElement | null) {
    this.stop();
    const stopped = this.sequence;
    requestAnimationFrame(() => {
      if (stopped === this.sequence && opener?.isConnected) opener.focus({ preventScroll: true });
    });
  }
  /** 導覽期間 dialog 的關閉要求（Escape 以外，例如返回手勢）只結束導覽；dialog 自己關閉則導覽一起結束。 */
  private guard(dialog: HTMLDialogElement | null | undefined) {
    if (!dialog) return undefined;
    const cancel = (event: Event) => {
      event.preventDefault();
      this.dismiss(null);
    };
    const close = () => this.stop();
    dialog.addEventListener('cancel', cancel);
    dialog.addEventListener('close', close);
    return () => {
      dialog.removeEventListener('cancel', cancel);
      dialog.removeEventListener('close', close);
    };
  }
}
