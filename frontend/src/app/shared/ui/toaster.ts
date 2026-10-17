import {
  afterNextRender,
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  effect,
  ElementRef,
  inject,
  Injector,
  signal,
  untracked,
  viewChild,
  viewChildren,
} from '@angular/core';
import { SwipeTracker } from '../browser/swipe';
import { Icon } from './icon';
import { ToastService, toastIcon, type Toast } from './toast-service';

/** 收合時，位於最前面的提示後方的提示。 */
const VISIBLE = 3;
const PEEK = 10;
const GAP = 8;
const SWIPE_DISMISS = 64;

/**
 * `ToastService` 的右下角堆疊。收合時，較新的提示排在一小疊的前面（參考 Sonner 的堆疊）；
 * 懸停或聚焦時會以彈簧展開，並暫停所有計時器。每則提示先以膠囊形式出現，再 morph 成卡片，與島相同。
 * 此層是手動的 popover，所以會留在頁面內容之上；提示在對話框上方抵達時，會重新進入最上層。
 * 螢幕閱讀器依緊急程度各聽一個 live region，而不是逐張聽卡片。
 */
@Component({
  selector: 'nx-toaster',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './toaster.scss',
  host: { '(keydown.escape)': 'escape($event)' },
  template: `<div class="sr-only" aria-live="polite" aria-atomic="true">{{ polite() }}</div>
    <div class="sr-only" aria-live="assertive" aria-atomic="true">{{ assertive() }}</div>
    <section
      #layer
      popover="manual"
      class="toaster"
      aria-label="操作通知"
      [attr.data-expanded]="expanded() || null"
      (pointerenter)="hover(true)"
      (pointerleave)="hover(false)"
      (focusin)="focusIn($event)"
      (focusout)="focusOut($event)"
    >
      <ol [style.height.px]="height()">
        @for (toast of toasts(); track toast.id; let i = $index) {
          <li
            class="toast"
            animate.enter="toast-enter"
            animate.leave="toast-leave"
            [attr.data-tone]="toast.tone"
            [attr.data-front]="i === 0 || null"
            [attr.data-hidden]="(!expanded() && i >= visible) || null"
            [style.--i]="i"
            [style.--y]="offsets()[i] + 'px'"
            [style.--h]="cardHeight(toast, i)"
            [style.translate]="dragging()?.id === toast.id ? dragging()!.x + 'px 0' : null"
            [attr.data-dragging]="dragging()?.id === toast.id || null"
            (pointerdown)="grab($event, toast)"
            (pointermove)="drag($event, toast)"
            (pointerup)="release($event, toast)"
            (pointercancel)="release($event, toast)"
          >
            @for (version of [toast.version]; track version) {
              <div
                #body
                class="toast-body"
                [attr.data-id]="toast.id"
                [animate.enter]="version > 1 ? 'toast-swap' : ''"
              >
                <nx-icon class="toast-icon" [name]="icon(toast)" />
                <div class="toast-text">
                  <strong>{{ toast.title }}</strong>
                  @if (toast.description) {
                    <span>{{ toast.description }}</span>
                  }
                </div>
                @if (toast.action; as action) {
                  <button
                    type="button"
                    class="toast-action"
                    (click)="act(toast, $event.detail === 0)"
                  >
                    {{ action.label }}
                  </button>
                }
                <button
                  type="button"
                  class="icon-button toast-close"
                  aria-label="關閉通知"
                  title="關閉通知"
                  (click)="dismiss(toast, $event.detail === 0)"
                >
                  <nx-icon name="close" />
                </button>
              </div>
            }
          </li>
        }
      </ol>
    </section>`,
})
export class Toaster {
  private readonly service = inject(ToastService);
  readonly toasts = this.service.toasts;
  readonly visible = VISIBLE;
  readonly polite = signal('');
  readonly assertive = signal('');
  private readonly hovered = signal(false);
  private readonly focused = signal(false);
  readonly expanded = computed(() => this.hovered() || this.focused());
  readonly heights = signal<Record<string, number>>({});
  readonly dragging = signal<{ id: string; swipe: SwipeTracker; x: number } | null>(null);
  private readonly layer = viewChild.required<ElementRef<HTMLElement>>('layer');
  private readonly bodies = viewChildren<ElementRef<HTMLElement>>('body');
  private readonly injector = inject(Injector);
  private returnFocus: HTMLElement | null = null;

  /** 每則提示距離底邊的高度：收合時只露出一點，展開時是完整位置。 */
  readonly offsets = computed(() => {
    const heights = this.heights();
    let pointY = 0;
    return this.toasts().map((toast, index2) => {
      const offset = this.expanded() ? pointY : index2 * PEEK;
      pointY += (heights[toast.id] ?? 0) + GAP;
      return offset;
    });
  });
  readonly height = computed(() => {
    const toasts = this.toasts();
    if (!toasts.length) return 0;
    const heights = this.heights();
    if (this.expanded())
      return toasts.reduce((sum, toast) => sum + (heights[toast.id] ?? 0) + GAP, -GAP);
    return (heights[toasts[0].id] ?? 0) + (Math.min(toasts.length, VISIBLE) - 1) * PEEK;
  });

  constructor() {
    const destroyRef = inject(DestroyRef);
    afterNextRender(() => this.layer().nativeElement.showPopover?.());

    if (typeof ResizeObserver !== 'undefined') {
      const observer = new ResizeObserver((entries) =>
        this.heights.update((heights) => {
          const next = { ...heights };
          for (const entry of entries) {
            const id = (entry.target as HTMLElement).dataset['id'];
            if (id) next[id] = entry.borderBoxSize?.[0]?.blockSize ?? entry.contentRect.height;
          }
          return next;
        }),
      );
      effect((onCleanup) => {
        const elements = this.bodies().map((body) => body.nativeElement);
        elements.forEach((element) => observer.observe(element));
        onCleanup(() => elements.forEach((element) => observer.unobserve(element)));
      });
      destroyRef.onDestroy(() => observer.disconnect());
    }

    // 每則新提示或被取代的提示只播報一次；其後開啟的對話框需要重新把此層提升。
    let seen = new Map<string, number>();
    effect(() => {
      const toasts = this.toasts();
      const fresh = toasts.find((toast) => seen.get(toast.id) !== toast.version);
      seen = new Map(toasts.map((toast) => [toast.id, toast.version]));
      untracked(() => {
        // 清空的堆疊可能在靜止的指標下收縮，而指標不會觸發 pointerleave。
        if (!toasts.length) this.hover(false);
        this.heights.update((heights) =>
          Object.fromEntries(Object.entries(heights).filter(([id]) => seen.has(id))),
        );
      });
      if (!fresh) return;
      untracked(() => {
        const text = [fresh.title, fresh.description].filter(Boolean).join('。');
        const region = fresh.tone === 'danger' ? this.assertive : this.polite;
        region.set('');
        queueMicrotask(() => region.set(text));
        this.raise();
      });
    });

    const visibility = () => (document.hidden ? this.service.pause() : this.syncTimers());
    document.addEventListener('visibilitychange', visibility);
    destroyRef.onDestroy(() => document.removeEventListener('visibilitychange', visibility));
  }

  /** 收合時，後面的卡片採用前卡的高度，只露出邊緣。 */
  cardHeight(toast: Toast, index: number) {
    const id = this.expanded() || index === 0 ? toast.id : this.toasts()[0].id;
    const height = this.heights()[id];
    return height === undefined ? null : height + 'px';
  }
  icon(toast: Toast) {
    return toastIcon(toast.tone);
  }
  hover(value: boolean) {
    this.hovered.set(value);
    this.syncTimers();
  }
  focusIn(event: FocusEvent) {
    const from = event.relatedTarget as HTMLElement | null;
    if (!this.focused() && from && !this.layer().nativeElement.contains(from))
      this.returnFocus = from;
    this.focused.set(true);
    this.syncTimers();
  }
  focusOut(event: FocusEvent) {
    if (this.layer().nativeElement.contains(event.relatedTarget as Node | null)) return;
    this.focused.set(false);
    this.syncTimers();
  }
  /** Escape 會關閉取得焦點的提示。 */
  escape(event: Event) {
    const id = (event.target as HTMLElement).closest<HTMLElement>('.toast-body')?.dataset['id'];
    const toast = this.toasts().find((item) => item.id === id);
    if (toast) this.dismiss(toast, true);
  }
  act(toast: Toast, keyboard: boolean) {
    this.dismiss(toast, keyboard, () => this.service.act(toast.id));
  }
  /** 鍵盤關閉會把焦點留在堆疊中，或歸還到來源處。 */
  dismiss(toast: Toast, keyboard = false, close = () => this.service.dismiss(toast.id)) {
    const next = this.toasts().find((item) => item.id !== toast.id);
    close();
    if (!keyboard) return;
    afterNextRender(
      () => {
        const close =
          next &&
          this.layer().nativeElement.querySelector<HTMLElement>(
            `.toast-body[data-id="${CSS.escape(next.id)}"] .toast-close`,
          );
        (close ?? this.returnFocus)?.focus();
        if (!close) this.returnFocus = null;
      },
      { injector: this.injector },
    );
  }
  grab(event: PointerEvent, toast: Toast) {
    if (event.button !== 0 || (event.target as HTMLElement).closest('button')) return;
    (event.currentTarget as HTMLElement).setPointerCapture(event.pointerId);
    this.dragging.set({
      id: toast.id,
      swipe: new SwipeTracker(1, event.clientX, event.timeStamp),
      x: 0,
    });
  }
  drag(event: PointerEvent, toast: Toast) {
    const drag = this.dragging();
    if (drag?.id !== toast.id) return;
    this.dragging.set({ ...drag, x: drag.swipe.move(event.clientX, event.timeStamp) });
  }
  release(event: PointerEvent, toast: Toast) {
    const drag = this.dragging();
    if (drag?.id !== toast.id) return;
    this.dragging.set(null);
    if (event.type === 'pointercancel' || !drag.swipe.dismisses(SWIPE_DISMISS, event.timeStamp))
      return;
    // 直接設定在元素本身：離開中的視圖的綁定不會再更新。
    const element = event.currentTarget as HTMLElement;
    element.dataset['swiped'] = '';
    element.style.setProperty('--swipe-x', drag.x + 'px');
    this.service.dismiss(toast.id);
  }

  private syncTimers() {
    if (this.expanded() || document.hidden) this.service.pause();
    else this.service.resume();
  }
  private raise() {
    const layer = this.layer().nativeElement;
    if (!layer.showPopover || !document.querySelector('dialog:modal')) return;
    layer.hidePopover();
    layer.showPopover();
  }
}
