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
import { Icon } from './icon';
import { ToastService, toastIcon, type Toast } from './toast-service';

/** Toasts drawn behind the front one while collapsed. */
const VISIBLE = 3;
const PEEK = 10;
const GAP = 8;
const SWIPE_DISMISS = 64;

/**
 * The bottom-right stack for `ToastService`. Collapsed, newer toasts sit in front of a short pile
 * (Sonner's stack); hovering or focusing fans it out on the spring and holds every timer. Each toast
 * arrives as a capsule that morphs into its card, like the island. The layer is a manual popover,
 * so it stays above page content, and re-enters the top layer when a toast arrives over a modal.
 * Screen readers hear one live region per urgency rather than the cards themselves.
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
  readonly dragging = signal<{ id: string; start: number; x: number } | null>(null);
  private readonly layer = viewChild.required<ElementRef<HTMLElement>>('layer');
  private readonly bodies = viewChildren<ElementRef<HTMLElement>>('body');
  private readonly injector = inject(Injector);
  private returnFocus: HTMLElement | null = null;

  /** Distance of each toast above the bottom edge: a small peek collapsed, its full place fanned. */
  readonly offsets = computed(() => {
    const heights = this.heights();
    let y = 0;
    return this.toasts().map((toast, i) => {
      const offset = this.expanded() ? y : i * PEEK;
      y += (heights[toast.id] ?? 0) + GAP;
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

    // Announce each new or replaced toast once; a modal opened since needs the layer raised again.
    let seen = new Map<string, number>();
    effect(() => {
      const toasts = this.toasts();
      const fresh = toasts.find((toast) => seen.get(toast.id) !== toast.version);
      seen = new Map(toasts.map((toast) => [toast.id, toast.version]));
      untracked(() => {
        // An emptied stack can shrink away under a still pointer, which never fires pointerleave.
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

  /** Collapsed, the cards behind take the front card's height so only their edges show. */
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
  /** Escape dismisses the toast that holds focus. */
  escape(event: Event) {
    const id = (event.target as HTMLElement).closest<HTMLElement>('.toast-body')?.dataset['id'];
    const toast = this.toasts().find((item) => item.id === id);
    if (toast) this.dismiss(toast, true);
  }
  act(toast: Toast, keyboard: boolean) {
    this.dismiss(toast, keyboard, () => this.service.act(toast.id));
  }
  /** Keyboard dismissal keeps focus in the stack, or returns it where it came from. */
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
    this.dragging.set({ id: toast.id, start: event.clientX, x: 0 });
  }
  drag(event: PointerEvent, toast: Toast) {
    const drag = this.dragging();
    if (drag?.id !== toast.id) return;
    const dx = event.clientX - drag.start;
    // Rightward follows the pointer; leftward resists, since that is not a way out.
    this.dragging.set({ ...drag, x: dx > 0 ? dx : dx / 6 });
  }
  release(event: PointerEvent, toast: Toast) {
    const drag = this.dragging();
    if (drag?.id !== toast.id) return;
    this.dragging.set(null);
    if (event.type === 'pointercancel' || drag.x <= SWIPE_DISMISS) return;
    // Set on the element itself: the leaving view's bindings are no longer refreshed.
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
