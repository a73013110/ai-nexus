import {
  afterNextRender,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  inject,
  Injector,
  input,
  output,
  viewChild,
} from '@angular/core';
import { Icon } from './icon';
import { anchoredPopover } from '../browser/anchored-popover';
import { positionBesidePopover, positionPopover } from '../browser/popover-position';

export interface MenuAction {
  id: string;
  label: string;
  icon: string;
  disabled?: boolean;
  danger?: boolean;
  /** Makes the item a checkbox toggle (menuitemcheckbox) that shows its state. */
  checked?: boolean;
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
      (toggle)="popover.sync($event)"
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
          [attr.role]="item.checked === undefined ? 'menuitem' : 'menuitemcheckbox'"
          [attr.aria-checked]="item.checked ?? null"
          [disabled]="item.disabled"
          [class.danger-text]="item.danger"
          (click)="choose(item.id)"
        >
          <nx-icon [name]="item.icon" />{{ item.label }}
          @if (item.checked) {
            <nx-icon name="check" class="menu-check" />
          }
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
  readonly id = `nx-menu-${++sequence}`;
  private anchor: HTMLElement | null = null;
  private readonly injector = inject(Injector);
  private readonly panel = viewChild.required<ElementRef<HTMLDivElement>>('panel');
  protected readonly popover = anchoredPopover({
    panel: () => this.panel().nativeElement,
    place: (panel) => {
      if (!this.anchor) return;
      if (this.placement() === 'beside')
        positionBesidePopover(this.anchor, panel, this.minWidth() || 240);
      else positionPopover(this.anchor, panel, this.minWidth(), 'right');
    },
  });
  readonly opened = this.popover.opened;
  /** Opens beside `anchor`; focus returns there when the menu closes from the keyboard. */
  open(anchor: HTMLElement, focus: 'first' | 'last' = 'first') {
    if (this.opened()) this.popover.hide();
    this.anchor = anchor;
    this.popover.show();
    // Items may belong to the row that just asked, so place and focus once they have rendered.
    afterNextRender(
      () => {
        if (!this.isOpenFor(anchor)) return;
        this.popover.reposition();
        const buttons = this.buttons();
        (focus === 'first' ? buttons[0] : buttons.at(-1))?.focus();
      },
      { injector: this.injector },
    );
  }
  isOpenFor(anchor: HTMLElement) {
    return this.opened() && this.anchor === anchor;
  }
  close() {
    this.popover.hide();
    this.anchor?.focus();
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
