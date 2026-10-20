import {
  ApplicationRef,
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  inject,
  signal,
} from '@angular/core';
import { ThemeService } from '@core/preferences/theme-service';
import { DashboardLayout } from './dashboard-layout';
import { DashboardTile } from './dashboard-tile';

interface Drag {
  id: string;
  element: HTMLElement;
  pointer: number;
  /** 指標位置減去磚塊未轉換的位置；磚塊換位時保留。 */
  offsetX: number;
  offsetY: number;
  lastTarget: string | null;
  lastSwap: number;
  /** 最新的指標位置；頁面在靜止指標下捲動時會重播。 */
  last: PointerEvent;
  scroll: number;
}

/** 交換需要指標明顯位於另一磚塊內，不同大小的磚塊才不會閃爍。 */
const SWAP_INSET = 0.18;
const SWAP_COOLDOWN = 160;
/** 位於捲動區域的頂部或底部附近時，被拖曳的磚塊會捲動頁面。 */
const SCROLL_EDGE = 72;
const SCROLL_SPEED = 18;

/**
 * 12 欄格狀排列的磚塊。編輯模式下，拖曳把手可重新排序（其他磚塊會滑開），
 * 或在把手上用方向鍵，每次移動都會播報。
 */
@Component({
  selector: 'nx-dashboard-board',
  imports: [DashboardTile],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './dashboard-board.scss',
  template: `<div class="board" [class.is-editing]="layout.editing()">
      @for (tile of layout.visible(); track tile.id) {
        <nx-dashboard-tile
          [tile]="tile"
          [editing]="layout.editing()"
          [class.is-dragging]="dragging() === tile.id"
          [style.animation-delay.ms]="$index * 45"
          (grab)="grab($event, tile.id)"
          (nudge)="nudge(tile.id, $event)"
          (sizeChange)="flip(() => layout.resize(tile.id, $event))"
          (tall)="flip(() => layout.setTall(tile.id, $event))"
          (hide)="flip(() => layout.setHidden(tile.id, true))"
        />
      }
    </div>
    <p class="sr-only" aria-live="polite">{{ announcement() }}</p>`,
})
export class DashboardBoard {
  readonly layout = inject(DashboardLayout);
  private readonly app = inject(ApplicationRef);
  private readonly theme = inject(ThemeService);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  readonly dragging = signal<string | null>(null);
  readonly announcement = signal('');
  private drag: Drag | null = null;

  constructor() {
    inject(DestroyRef).onDestroy(() => this.end());
  }

  /** 執行版面變更，讓磚塊從舊位置滑到新位置。 */
  flip(change: () => void, except?: string) {
    const before = this.rects();
    change();
    this.app.tick();
    if (this.theme.reducedMotion()) return;
    for (const [id, element] of this.elements()) {
      const old = before.get(id);
      if (!old || id === except) continue;
      const now = element.getBoundingClientRect();
      const dx = old.left - now.left,
        dy = old.top - now.top;
      if (Math.abs(dx) < 1 && Math.abs(dy) < 1) continue;
      element.animate([{ translate: `${dx}px ${dy}px` }, { translate: '0 0' }], {
        duration: 420,
        easing: 'cubic-bezier(0.34, 1.3, 0.64, 1)',
        composite: 'add',
      });
    }
  }

  nudge(id: string, offset: number | 'first' | 'last') {
    const visible = this.layout.visible();
    const step = offset === 'first' ? -visible.length : offset === 'last' ? visible.length : offset;
    this.flip(() => this.layout.shift(id, step));
    this.announce(id);
    this.handle(id)?.focus();
  }

  grab(event: PointerEvent, id: string) {
    if (event.button !== 0 || this.drag) return;
    const element = this.element(id);
    if (!element) return;
    event.preventDefault();
    const box = element.getBoundingClientRect();
    this.drag = {
      id,
      element,
      pointer: event.pointerId,
      offsetX: event.clientX - box.left,
      offsetY: event.clientY - box.top,
      lastTarget: null,
      lastSwap: 0,
      last: event,
      scroll: 0,
    };
    this.dragging.set(id);
    // 使用視窗監聽器而非指標捕獲：重新排序會在 DOM 中移動磚塊，這會讓捕獲失效。
    window.addEventListener('pointermove', this.move);
    window.addEventListener('pointerup', this.drop);
    window.addEventListener('pointercancel', this.drop);
  }

  private readonly move = (event: PointerEvent) => {
    const drag = this.drag;
    if (!drag || event.pointerId !== drag.pointer) return;
    drag.last = event;
    drag.scroll ||= requestAnimationFrame(() => this.autoscroll());
    this.step(drag, event);
  };

  private step(drag: Drag, event: PointerEvent) {
    this.follow(drag, event);
    const target = this.target(drag, event);
    const now = performance.now();
    if (!target || (target === drag.lastTarget && now - drag.lastSwap < SWAP_COOLDOWN * 3)) return;
    if (now - drag.lastSwap < SWAP_COOLDOWN) return;
    drag.lastTarget = target;
    drag.lastSwap = now;
    this.flip(() => this.layout.move(drag.id, target), drag.id);
    this.follow(drag, event);
  }

  /** 指標停在邊緣附近時捲動，讓較遠的磚塊也能被抵達。 */
  private autoscroll() {
    const drag = this.drag;
    if (!drag) return;
    drag.scroll = 0;
    const scroller = this.host.closest<HTMLElement>('.feature-main');
    if (!scroller) return;
    const box = scroller.getBoundingClientRect(),
      pointY = drag.last.clientY;
    const speed =
      pointY < box.top + SCROLL_EDGE
        ? -SCROLL_SPEED * (1 - Math.max(0, pointY - box.top) / SCROLL_EDGE)
        : pointY > box.bottom - SCROLL_EDGE
          ? SCROLL_SPEED * (1 - Math.max(0, box.bottom - pointY) / SCROLL_EDGE)
          : 0;
    if (!speed) return;
    const before = scroller.scrollTop;
    scroller.scrollTop += speed;
    if (scroller.scrollTop === before) return;
    this.step(drag, drag.last);
    drag.scroll = requestAnimationFrame(() => this.autoscroll());
  }

  private readonly drop = (event: PointerEvent) => {
    const drag = this.drag;
    if (!drag || event.pointerId !== drag.pointer) return;
    const from = drag.element.style.translate || '0 0';
    this.end();
    if (!this.theme.reducedMotion())
      drag.element.animate([{ translate: from }, { translate: '0 0' }], {
        duration: 360,
        easing: 'cubic-bezier(0.34, 1.3, 0.64, 1)',
      });
    this.announce(drag.id);
  };

  /** 播報磚塊移動後的位置，讓螢幕閱讀器使用者知道放到哪裡。 */
  private announce(id: string) {
    const visible = this.layout.visible();
    const position = visible.findIndex((item) => item.id === id) + 1;
    this.announcement.set(
      `${visible[position - 1]?.widget.title ?? ''}：第 ${position} 個，共 ${visible.length} 個`,
    );
  }

  private end() {
    const drag = this.drag;
    if (!drag) return;
    cancelAnimationFrame(drag.scroll);
    window.removeEventListener('pointermove', this.move);
    window.removeEventListener('pointerup', this.drop);
    window.removeEventListener('pointercancel', this.drop);
    drag.element.style.removeProperty('translate');
    this.drag = null;
    this.dragging.set(null);
  }

  /** 讓被拖曳的磚塊跟著指標，不論它的格狀位置現在在哪。 */
  private follow(drag: Drag, event: PointerEvent) {
    drag.element.style.removeProperty('translate');
    const box = drag.element.getBoundingClientRect();
    const dx = event.clientX - drag.offsetX - box.left,
      dy = event.clientY - drag.offsetY - box.top;
    drag.element.style.translate = `${dx}px ${dy}px`;
  }

  private target(drag: Drag, event: PointerEvent) {
    for (const [id, element] of this.elements()) {
      if (id === drag.id) continue;
      const box = this.slot(element);
      const insetX = box.width * SWAP_INSET,
        insetY = box.height * SWAP_INSET;
      if (
        event.clientX > box.left + insetX &&
        event.clientX < box.right - insetX &&
        event.clientY > box.top + insetY &&
        event.clientY < box.bottom - insetY
      )
        return id;
    }
    return null;
  }

  /**
   * 磚塊在格狀中的位置，忽略仍在進行中的滑動。剛讓出空間的磚塊
   * 仍會被視為位於指標下（在忙碌的裝置上，它的滑動甚至可能還沒開始），
   * 若在那裡命中它，被拖曳的磚塊會立刻被換回去。
   */
  private slot(element: HTMLElement) {
    const board = element.parentElement!;
    const origin = board.getBoundingClientRect();
    const left = origin.left + element.offsetLeft - board.offsetLeft,
      top = origin.top + element.offsetTop - board.offsetTop;
    const width = element.offsetWidth,
      height = element.offsetHeight;
    return { left, top, width, height, right: left + width, bottom: top + height };
  }

  private elements() {
    return [...this.host.querySelectorAll<HTMLElement>('nx-dashboard-tile')].map(
      (element) => [element.dataset['id'] ?? '', element] as const,
    );
  }
  private element(id: string) {
    return this.elements().find(([text]) => text === id)?.[1] ?? null;
  }
  private handle(id: string) {
    return this.host.querySelector<HTMLElement>(`[data-handle="${CSS.escape(id)}"]`);
  }
  private rects() {
    return new Map(this.elements().map(([id, element]) => [id, element.getBoundingClientRect()]));
  }
}
