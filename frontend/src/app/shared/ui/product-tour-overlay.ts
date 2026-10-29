import {
  afterRenderEffect,
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  ElementRef,
  inject,
  input,
  output,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { ThemeService } from '@core/preferences/theme-service';
import { motionTiming } from '@shared/browser/motion-tokens';
import { placeBeside, setPopoverOrigin } from '@shared/browser/popover-position';
import { uniqueId } from '@shared/browser/unique-id';
import { visibleRect } from '@shared/browser/visible-rect';
import { Icon } from './icon';
import type { ShownTourStep } from './product-tour';
import { FrameMotion, ViewMotion } from './view-motion';

/** 聚光框比目標外擴的距離（px）。 */
const PADDING = 6;

/**
 * 導覽層（由 `ProductTour` 建立與移除，不直接使用）：遮罩挖出目標的聚光框，說明框是共用的
 * `.popover-panel`。換步時聚光框與說明框以彈簧移到新目標，內文以 `ViewMotion` 換頁、外框以
 * `FrameMotion` 過渡高度。宿主是全視口的 manual popover，擋住頁面操作並接住點遮罩。
 */
@Component({
  selector: 'nx-product-tour',
  imports: [Icon, FrameMotion, ViewMotion],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'tour-layer',
    popover: 'manual',
    '[class.tour-leaving]': 'leaving()',
    '(click)': 'clickedLayer($event)',
    // 導覽期間頁面不捲動：目標由導覽捲到可見處，使用者捲動會讓聚光框與目標錯開。
    '(wheel)': '$event.preventDefault()',
    '(touchmove)': '$event.preventDefault()',
  },
  template: `
    <div class="tour-spot" #spot></div>
    <section
      #card
      class="popover-panel product-tour"
      popover="manual"
      role="dialog"
      [attr.aria-labelledby]="ids.title"
      [attr.aria-describedby]="ids.description"
      nxFrameMotion
    >
      <div class="tour-body" #body>
        <div [nxViewMotion]="index()">
          <p class="tour-eyebrow">
            快速導覽 <span class="tour-progress">{{ index() + 1 }} / {{ steps().length }}</span>
          </p>
          <h2 class="tour-title" [id]="ids.title">{{ step().title }}</h2>
          <p class="tour-description" [id]="ids.description">{{ step().description }}</p>
        </div>
        <div class="tour-footer">
          <span class="tour-dots" aria-hidden="true">
            @for (item of steps(); track $index) {
              <i [class.current]="$index === index()"></i>
            }
          </span>
          <button type="button" class="quiet-button" [disabled]="!index()" (click)="previous()">
            上一步
          </button>
          <button type="button" class="primary-button" #primary (click)="next()">
            {{ last() ? '開始使用' : '下一步' }}
          </button>
        </div>
        <button
          type="button"
          class="icon-button tour-close"
          aria-label="跳過導覽"
          title="跳過導覽"
          (click)="closed.emit()"
        >
          <nx-icon name="close" />
        </button>
      </div>
    </section>
  `,
  styleUrl: './product-tour-overlay.scss',
})
export class ProductTourOverlay {
  readonly steps = input.required<readonly ShownTourStep[]>();
  /** 使用者要求結束（Escape、跳過、完成、點遮罩）；由 `ProductTour` 收尾。 */
  readonly closed = output<void>();
  readonly index = signal(0);
  readonly leaving = signal(false);
  readonly step = computed(() => this.steps()[this.index()]);
  readonly last = computed(() => this.index() === this.steps().length - 1);
  readonly ids = { title: uniqueId('tour-title'), description: uniqueId('tour-description') };
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  private readonly theme = inject(ThemeService);
  private readonly spot = viewChild.required<ElementRef<HTMLElement>>('spot');
  private readonly card = viewChild.required<ElementRef<HTMLElement>>('card');
  private readonly body = viewChild.required<ElementRef<HTMLElement>>('body');
  private readonly primary = viewChild.required<ElementRef<HTMLButtonElement>>('primary');
  private shown = false;
  private placed?: DOMRect;
  private frame = 0;
  private readonly observer =
    typeof ResizeObserver === 'undefined' ? undefined : new ResizeObserver(() => this.track());

  constructor() {
    afterRenderEffect(() => {
      this.index();
      untracked(() => this.present());
    });
    const keydown = (event: KeyboardEvent) => this.keydown(event);
    const track = () => this.track();
    // 捕獲階段先攔下：dialog、抽屜與頁面的 Escape 與快速鍵不會跟著動作。
    window.addEventListener('keydown', keydown, true);
    // 只監聽 window 的捲動不夠：dialog 與頁面主區都是自己的捲動容器。
    document.addEventListener('scroll', track, { capture: true, passive: true });
    window.addEventListener('resize', track);
    inject(DestroyRef).onDestroy(() => {
      window.removeEventListener('keydown', keydown, true);
      document.removeEventListener('scroll', track, { capture: true });
      window.removeEventListener('resize', track);
      this.observer?.disconnect();
      cancelAnimationFrame(this.frame);
    });
  }
  next() {
    if (this.last()) this.closed.emit();
    else this.index.update((index) => index + 1);
  }
  previous() {
    this.index.update((index) => Math.max(0, index - 1));
  }
  /** 開始退場，回傳要等待的動畫（沒有就立即移除）。之後不再回應操作。 */
  leave(): Animation[] {
    this.leaving.set(true);
    if (this.shown) this.card().nativeElement.hidePopover();
    // 退場的 transition 在樣式重新計算後才出現在 getAnimations()。
    return this.theme.reducedMotion() || !this.host.getAnimations
      ? []
      : this.host.getAnimations({ subtree: true });
  }
  clickedLayer(event: MouseEvent) {
    if (!this.card().nativeElement.contains(event.target as Node)) this.closed.emit();
  }

  /** 每次換步渲染後：第一次先進入 top layer，再把聚光框與說明框放到目標上。 */
  private present() {
    if (this.leaving()) return;
    const card = this.card().nativeElement;
    if (!this.shown) {
      this.shown = true;
      // 遮罩層先、說明框後，說明框在 top layer 的上面。
      this.host.showPopover();
      card.showPopover();
    }
    const target = this.step().target();
    this.observer?.disconnect();
    if (target) {
      // 被 dialog 或主區捲動容器裁掉的目標仍在視口內，所以每步都捲到最近的可見位置。
      target.scrollIntoView({ block: 'nearest', inline: 'nearest' });
      this.observer?.observe(target);
    }
    this.place(target, false);
    if (!card.contains(document.activeElement))
      this.primary().nativeElement.focus({ preventScroll: true });
  }
  /** 捲動與尺寸變化只在目標真的移動時，直接把兩者對齊（不播放彈簧）。 */
  private track() {
    cancelAnimationFrame(this.frame);
    this.frame = requestAnimationFrame(() => {
      if (!this.shown || this.leaving()) return;
      const target = this.step().target();
      const rect = target?.getBoundingClientRect();
      const placed = this.placed;
      if (
        rect &&
        placed &&
        rect.x === placed.x &&
        rect.y === placed.y &&
        rect.width === placed.width &&
        rect.height === placed.height
      )
        return;
      this.host.classList.add('tour-tracking');
      this.place(target, true);
      this.host.getBoundingClientRect();
      this.host.classList.remove('tour-tracking');
    });
  }
  private place(target: HTMLElement | undefined, tracking: boolean) {
    const spot = this.spot().nativeElement,
      card = this.card().nativeElement;
    this.placed = target?.getBoundingClientRect();
    // 只框目標露出的部分：比對話框內容高的段落不會讓框蓋到頁籤或儲存列，說明框也貼著看得到的那一截。
    const visible = target ? visibleRect(target, PADDING) : null;
    const area = visible
      ? {
          left: visible.left,
          top: visible.top,
          width: visible.right - visible.left,
          height: visible.bottom - visible.top,
        }
      : undefined;
    // 沒有目標（已從畫面移除或完全被捲出）時聚光框縮成視口中心的一點，遮罩蓋滿。
    const { left, top, width, height } = area ?? {
      left: innerWidth / 2,
      top: innerHeight / 2,
      width: 0,
      height: 0,
    };
    const moved = spot.style.left !== `${left}px` || spot.style.top !== `${top}px`;
    Object.assign(spot.style, {
      left: `${left}px`,
      top: `${top}px`,
      width: `${width}px`,
      height: `${height}px`,
    });
    // 說明框的高度可能正由 FrameMotion 過渡，以內容的自然高度定位。
    const size = {
      width: card.offsetWidth,
      height: this.body().nativeElement.offsetHeight + card.offsetHeight - card.clientHeight,
    };
    const place = placeBeside(area, size, this.step().side, {
      width: innerWidth,
      height: innerHeight,
    });
    card.style.left = `${place.left}px`;
    card.style.top = `${place.top}px`;
    setPopoverOrigin(card, place.side, place.originX, place.originY);
    if (moved && !tracking && area) this.pulse(spot);
  }
  private pulse(spot: HTMLElement) {
    if (this.theme.reducedMotion() || !spot.animate) return;
    const { duration: morph, easing } = motionTiming(spot, '--motion-morph');
    spot.animate(
      [
        { inset: '-2px', opacity: 1 },
        { inset: '-14px', opacity: 0 },
      ],
      {
        pseudoElement: '::after',
        duration: morph * 1.6,
        delay: morph * 0.6,
        easing,
      },
    );
  }
  private keydown(event: KeyboardEvent) {
    if (this.leaving()) return;
    const card = this.card().nativeElement;
    if (event.key === 'Escape') this.closed.emit();
    else if (event.key === 'ArrowRight' && !this.last()) this.next();
    else if (event.key === 'ArrowLeft') this.previous();
    else if (event.key === 'Tab') {
      // 焦點留在說明框內：頁面被遮罩擋住，Tab 不該跑到看不見的地方。
      const buttons = [...card.querySelectorAll<HTMLButtonElement>('button:enabled')];
      const current = buttons.indexOf(document.activeElement as HTMLButtonElement);
      const next =
        current < 0
          ? event.shiftKey
            ? buttons.length - 1
            : 0
          : (current + (event.shiftKey ? -1 : 1) + buttons.length) % buttons.length;
      buttons[next]?.focus({ preventScroll: true });
    } else return;
    event.preventDefault();
    event.stopImmediatePropagation();
  }
}
