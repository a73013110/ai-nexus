import {
  afterNextRender,
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  inject,
  Injector,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { Icon } from './icon';
import { positionBesidePopover, positionPopover } from '../browser/popover-position';

export interface MenuAction {
  id: string;
  label: string;
  icon: string;
  disabled?: boolean;
  danger?: boolean;
}
let sequence = 0;

/**
 * A top-layer menu that opens next to any trigger. `ActionMenu` pairs it with its own button; a
 * list renders one panel and opens it from each row, so rows stay plain elements.
 */
@Component({
  selector: 'nx-menu-panel',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './menu-panel.scss',
  template: `<!-- Focus moves between the menu items; the panel only routes arrow keys. -->
    <!-- eslint-disable-next-line @angular-eslint/template/interactive-supports-focus -->
    <div
      #panel
      [id]="id"
      class="popover-panel action-menu-panel ui-density-compact"
      popover="auto"
      role="menu"
      [attr.aria-label]="label()"
      (toggle)="toggled($any($event).newState === 'open')"
      (keydown)="key($event)"
    >
      @if (heading(); as header) {
        <div class="menu-heading" aria-hidden="true">
          <strong>{{ header.title }}</strong>
          @if (header.detail) {
            <small>{{ header.detail }}</small>
          }
        </div>
      }
      @for (item of items(); track item.id) {
        <button
          type="button"
          role="menuitem"
          [disabled]="item.disabled"
          [class.danger-text]="item.danger"
          (click)="choose(item.id)"
        >
          <nx-icon [name]="item.icon" />{{ item.label }}
        </button>
      }
    </div>`,
})
export class MenuPanel {
  readonly label = input('更多操作');
  readonly items = input.required<MenuAction[]>();
  /** Narrowest width; 0 matches the trigger, as the account menu does. */
  readonly minWidth = input(260);
  /** `beside` opens to the right of an icon-rail trigger instead of below it. */
  readonly placement = input<'below' | 'beside'>('below');
  /** Context the trigger cannot show, such as the account name on the collapsed rail. */
  readonly heading = input<{ title: string; detail?: string } | null>(null);
  readonly action = output<string>();
  readonly opened = signal(false);
  readonly id = `nx-menu-${++sequence}`;
  private anchor: HTMLElement | null = null;
  private readonly injector = inject(Injector);
  private readonly panel = viewChild.required<ElementRef<HTMLDivElement>>('panel');
  constructor() {
    const position = () => {
      if (this.opened() && this.anchor) this.place(this.anchor);
    };
    window.addEventListener('resize', position);
    window.addEventListener('scroll', position, true);
    inject(DestroyRef).onDestroy(() => {
      window.removeEventListener('resize', position);
      window.removeEventListener('scroll', position, true);
    });
  }
  /** Opens beside `anchor`; focus returns there when the menu closes from the keyboard. */
  open(anchor: HTMLElement, focus: 'first' | 'last' = 'first') {
    const panel = this.panel().nativeElement;
    if (this.opened()) panel.hidePopover();
    this.anchor = anchor;
    panel.showPopover();
    this.opened.set(true);
    this.place(anchor);
    // Items may belong to the row that just asked, so place and focus once they have rendered.
    afterNextRender(
      () => {
        if (!this.isOpenFor(anchor)) return;
        this.place(anchor);
        const buttons = this.buttons();
        (focus === 'first' ? buttons[0] : buttons.at(-1))?.focus();
      },
      { injector: this.injector },
    );
  }
  private place(anchor: HTMLElement) {
    const panel = this.panel().nativeElement;
    if (this.placement() === 'beside') positionBesidePopover(anchor, panel, this.minWidth() || 240);
    else positionPopover(anchor, panel, this.minWidth(), 'right');
  }
  isOpenFor(anchor: HTMLElement) {
    return this.opened() && this.anchor === anchor;
  }
  close() {
    this.panel().nativeElement.hidePopover();
    this.opened.set(false);
    this.anchor?.focus();
  }
  toggled(open: boolean) {
    this.opened.set(open);
  }
  choose(id: string) {
    this.close();
    this.action.emit(id);
  }
  key(event: KeyboardEvent) {
    if (event.key === 'Escape' && this.opened()) {
      event.preventDefault();
      event.stopPropagation();
      this.close();
    }
    if (event.key === 'Tab' && this.opened()) this.close();
    if (['ArrowDown', 'ArrowUp', 'Home', 'End'].includes(event.key)) {
      event.preventDefault();
      const buttons = this.buttons();
      if (!buttons.length) return;
      const current = buttons.findIndex((x) => x === document.activeElement);
      const index =
        event.key === 'Home'
          ? 0
          : event.key === 'End'
            ? buttons.length - 1
            : (current + (event.key === 'ArrowDown' ? 1 : -1) + buttons.length) % buttons.length;
      buttons[index]?.focus();
    }
  }
  private buttons() {
    return Array.from(
      this.panel().nativeElement.querySelectorAll<HTMLButtonElement>('button:enabled'),
    );
  }
}
