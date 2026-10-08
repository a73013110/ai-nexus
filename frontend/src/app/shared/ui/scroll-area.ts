import {
  afterNextRender,
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  ElementRef,
  inject,
  input,
  signal,
  viewChild,
} from '@angular/core';
import { ThemeService } from '../../core/preferences/theme-service';
import { Icon } from './icon';

let sequence = 0;

/** Native scrolling with stable width and position-aware, operable overflow cues. */
@Component({
  selector: 'nx-scroll-area',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[class.has-overflow]': 'overflow()' },
  styleUrl: './scroll-area.scss',
  template: `
    <div
      #viewport
      class="scroll-area-viewport"
      role="region"
      [id]="viewportId"
      [attr.aria-label]="label()"
      [attr.tabindex]="overflow() ? 0 : -1"
      (scroll)="scheduleMeasure()"
      (focusin)="revealFocus($event)"
    >
      <div #content class="scroll-area-content"><ng-content /></div>
    </div>
    <button
      type="button"
      class="scroll-area-edge scroll-area-start"
      [class.is-available]="above()"
      [attr.tabindex]="above() ? 0 : -1"
      [attr.aria-disabled]="!above()"
      [attr.aria-label]="'向上捲動' + label()"
      [title]="'向上捲動' + label()"
      [attr.aria-controls]="viewportId"
      (click)="scroll(-1)"
    >
      <nx-icon name="chevron-up" />
    </button>
    <button
      type="button"
      class="scroll-area-edge scroll-area-end"
      [class.is-available]="below()"
      [attr.tabindex]="below() ? 0 : -1"
      [attr.aria-disabled]="!below()"
      [attr.aria-label]="'向下捲動' + label()"
      [title]="'向下捲動' + label()"
      [attr.aria-controls]="viewportId"
      (click)="scroll(1)"
    >
      <nx-icon name="chevron-down" />
    </button>
  `,
})
export class ScrollArea {
  readonly label = input.required<string>();
  readonly viewportId = `scroll-area-${++sequence}`;
  readonly above = signal(false);
  readonly below = signal(false);
  readonly overflow = computed(() => this.above() || this.below());
  private readonly viewport = viewChild.required<ElementRef<HTMLElement>>('viewport');
  private readonly content = viewChild.required<ElementRef<HTMLElement>>('content');
  private readonly theme = inject(ThemeService);
  private frame = 0;
  private focusAfterResize = false;

  constructor() {
    const destroy = inject(DestroyRef);
    afterNextRender(() => {
      const observer = new ResizeObserver(() => this.scheduleMeasure(true));
      observer.observe(this.viewport().nativeElement);
      observer.observe(this.content().nativeElement);
      this.scheduleMeasure(true);
      destroy.onDestroy(() => observer.disconnect());
    });
    destroy.onDestroy(() => cancelAnimationFrame(this.frame));
  }

  scheduleMeasure(keepFocus = false) {
    this.focusAfterResize ||= keepFocus;
    if (this.frame) return;
    this.frame = requestAnimationFrame(() => {
      this.frame = 0;
      const viewport = this.viewport().nativeElement;
      const maximum = viewport.scrollHeight - viewport.clientHeight;
      // A zero-height viewport has no usable scroll range; ignore fractional pixel rounding.
      const visible = viewport.clientHeight > 0;
      this.above.set(visible && maximum > 1 && viewport.scrollTop > 1);
      this.below.set(visible && maximum > 1 && viewport.scrollTop < maximum - 1);
      if (this.focusAfterResize) {
        this.focusAfterResize = false;
        this.reveal(document.activeElement);
      }
    });
  }

  scroll(direction: -1 | 1) {
    if (direction === -1 ? !this.above() : !this.below()) return;
    const viewport = this.viewport().nativeElement;
    viewport.scrollBy({
      top: direction * Math.max(44, viewport.clientHeight * 0.75),
      behavior: this.theme.reducedMotion() ? 'instant' : 'smooth',
    });
  }

  revealFocus(event: FocusEvent) {
    this.reveal(event.target);
  }

  private reveal(target: EventTarget | null) {
    const viewport = this.viewport().nativeElement;
    if (
      !(target instanceof HTMLElement) ||
      !viewport.contains(target) ||
      target === viewport ||
      !this.overflow()
    )
      return;
    const bounds = viewport.getBoundingClientRect();
    const focused = target.getBoundingClientRect();
    const inset = parseFloat(getComputedStyle(viewport).scrollPaddingTop) || 0;
    if (focused.height > bounds.height - 2 * inset) return;
    const offset =
      focused.top < bounds.top + inset
        ? focused.top - bounds.top - inset
        : focused.bottom > bounds.bottom - inset
          ? focused.bottom - bounds.bottom + inset
          : 0;
    if (offset) viewport.scrollBy({ top: offset, behavior: 'instant' });
  }
}
