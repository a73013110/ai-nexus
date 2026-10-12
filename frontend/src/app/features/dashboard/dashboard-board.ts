import {
  ApplicationRef,
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  inject,
  input,
  signal,
} from '@angular/core';
import { ThemeService } from '../../core/preferences/theme-service';
import { DashboardLayout } from './dashboard-layout';
import { DashboardTile } from './dashboard-tile';

interface Drag {
  id: string;
  element: HTMLElement;
  pointer: number;
  /** Pointer position minus the tile's untranslated position, kept while the tile changes slot. */
  offsetX: number;
  offsetY: number;
  lastTarget: string | null;
  lastSwap: number;
  /** The latest pointer position, replayed while the page scrolls under a still pointer. */
  last: PointerEvent;
  scroll: number;
}

/** A swap needs the pointer well inside the other tile, so tiles of unequal size do not flicker. */
const SWAP_INSET = 0.18;
const SWAP_COOLDOWN = 160;
/** Near the top or bottom of the scroll area, a dragged tile scrolls the page. */
const SCROLL_EDGE = 72;
const SCROLL_SPEED = 18;

/**
 * The tiles on a 12-column grid. In edit mode tiles are reordered by dragging their handle (others
 * glide out of the way), or by arrow keys on the handle, with each move announced.
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
          [tilt]="tilt()"
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
  /** Maximum lean of a tile toward the pointer, in degrees. */
  readonly tilt = input(0);
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

  /** Runs a layout change and lets the tiles glide from their old places to the new ones. */
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
    const position = this.layout.visible().findIndex((x) => x.id === id) + 1;
    const title = visible.find((x) => x.id === id)?.widget.title ?? '';
    this.announcement.set(`${title}：第 ${position} 個，共 ${visible.length} 個`);
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
    // Window listeners, not pointer capture: reordering moves tiles in the DOM, which drops capture.
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

  /** Scrolls while the pointer rests near an edge, so far tiles can be reached. */
  private autoscroll() {
    const drag = this.drag;
    if (!drag) return;
    drag.scroll = 0;
    const scroller = this.host.closest<HTMLElement>('.feature-main');
    if (!scroller) return;
    const box = scroller.getBoundingClientRect(),
      y = drag.last.clientY;
    const speed =
      y < box.top + SCROLL_EDGE
        ? -SCROLL_SPEED * (1 - Math.max(0, y - box.top) / SCROLL_EDGE)
        : y > box.bottom - SCROLL_EDGE
          ? SCROLL_SPEED * (1 - Math.max(0, box.bottom - y) / SCROLL_EDGE)
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
    const visible = this.layout.visible();
    const position = visible.findIndex((x) => x.id === drag.id) + 1;
    this.announcement.set(
      `${visible[position - 1]?.widget.title ?? ''}：第 ${position} 個，共 ${visible.length} 個`,
    );
  };

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

  /** Keeps the dragged tile under the pointer, wherever its grid slot now is. */
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
      const box = element.getBoundingClientRect();
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

  private elements() {
    return [...this.host.querySelectorAll<HTMLElement>('nx-dashboard-tile')].map(
      (element) => [element.dataset['id'] ?? '', element] as const,
    );
  }
  private element(id: string) {
    return this.elements().find(([x]) => x === id)?.[1] ?? null;
  }
  private handle(id: string) {
    return this.host.querySelector<HTMLElement>(`[data-handle="${CSS.escape(id)}"]`);
  }
  private rects() {
    return new Map(this.elements().map(([id, element]) => [id, element.getBoundingClientRect()]));
  }
}
