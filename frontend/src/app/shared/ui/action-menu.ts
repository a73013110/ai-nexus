import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { Icon } from './icon';
import { positionPopover } from '../browser/popover-position';

export interface MenuAction {
  id: string;
  label: string;
  icon: string;
  disabled?: boolean;
  danger?: boolean;
}
let sequence = 0;
@Component({
  selector: 'nx-action-menu',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<button
      #trigger
      type="button"
      class="icon-button"
      [class.profile-trigger]="profile()"
      [attr.aria-label]="label()"
      aria-haspopup="menu"
      [attr.aria-expanded]="opened()"
      [attr.aria-controls]="id"
      (click)="toggle()"
      (keydown)="key($event, true)"
    >
      @if (profile()) {
        <ng-content />
      } @else {
        <nx-icon name="more" />
      }
    </button>
    <div
      #panel
      [id]="id"
      class="action-menu-panel ui-density-compact"
      popover="auto"
      role="menu"
      [attr.aria-label]="label()"
      (toggle)="opened.set($any($event).newState === 'open')"
      (keydown)="key($event)"
    >
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
export class ActionMenu {
  readonly profile = input(false);
  readonly label = input('更多操作');
  readonly items = input.required<MenuAction[]>();
  readonly action = output<string>();
  readonly opened = signal(false);
  readonly id = `nx-menu-${++sequence}`;
  private readonly trigger = viewChild.required<ElementRef<HTMLButtonElement>>('trigger');
  private readonly panel = viewChild.required<ElementRef<HTMLDivElement>>('panel');
  constructor() {
    const position = () => {
      if (this.opened())
        positionPopover(this.trigger().nativeElement, this.panel().nativeElement, 260, 'right');
    };
    window.addEventListener('resize', position);
    window.addEventListener('scroll', position, true);
    inject(DestroyRef).onDestroy(() => {
      window.removeEventListener('resize', position);
      window.removeEventListener('scroll', position, true);
    });
  }
  toggle() {
    if (this.opened()) this.close();
    else {
      this.panel().nativeElement.showPopover();
      this.opened.set(true);
      positionPopover(this.trigger().nativeElement, this.panel().nativeElement, 260, 'right');
      this.buttons()[0]?.focus();
    }
  }
  close() {
    this.panel().nativeElement.hidePopover();
    this.opened.set(false);
    this.trigger().nativeElement.focus();
  }
  choose(id: string) {
    this.close();
    this.action.emit(id);
  }
  key(event: KeyboardEvent, trigger = false) {
    if (event.key === 'Escape' && this.opened()) {
      event.preventDefault();
      event.stopPropagation();
      this.close();
    }
    if (event.key === 'Tab' && this.opened()) this.close();
    if (['ArrowDown', 'ArrowUp', 'Home', 'End'].includes(event.key)) {
      event.preventDefault();
      if (!this.opened()) this.toggle();
      const buttons = this.buttons();
      if (!buttons.length) return;
      const current = buttons.findIndex((x) => x === document.activeElement);
      const index =
        event.key === 'Home' || (trigger && event.key === 'ArrowDown')
          ? 0
          : event.key === 'End' || (trigger && event.key === 'ArrowUp')
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
