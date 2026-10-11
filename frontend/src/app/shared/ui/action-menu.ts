import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  input,
  output,
  viewChild,
} from '@angular/core';
import { Icon } from './icon';
import { MenuPanel, type MenuAction } from './menu-panel';

export type { MenuAction } from './menu-panel';

@Component({
  selector: 'nx-action-menu',
  imports: [Icon, MenuPanel],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './action-menu.scss',
  template: `<button
      #trigger
      type="button"
      [class.icon-button]="!triggerLabel()"
      [class.secondary-button]="!!triggerLabel()"
      [class.profile-trigger]="profile()"
      [attr.aria-label]="label()"
      aria-haspopup="menu"
      [attr.aria-expanded]="menu.opened()"
      [attr.aria-controls]="menu.id"
      (click)="toggle()"
      (keydown)="key($event)"
    >
      @if (profile()) {
        <ng-content />
      } @else {
        <nx-icon [name]="icon()" />
        @if (triggerLabel()) {
          <span>{{ triggerLabel() }}</span>
        }
      }
    </button>
    <nx-menu-panel
      #menu
      [label]="label()"
      [items]="items()"
      [minWidth]="profile() && placement() === 'below' ? 0 : 240"
      [placement]="placement()"
      [heading]="heading()"
      (action)="action.emit($event)"
    />`,
})
export class ActionMenu {
  readonly triggerLabel = input('');
  readonly icon = input('more');
  readonly profile = input(false);
  readonly placement = input<'below' | 'beside'>('below');
  readonly heading = input<{ title: string; detail?: string } | null>(null);
  readonly label = input('更多操作');
  readonly items = input.required<MenuAction[]>();
  readonly action = output<string>();
  private readonly trigger = viewChild.required<ElementRef<HTMLButtonElement>>('trigger');
  private readonly menu = viewChild.required(MenuPanel);
  toggle() {
    if (this.menu().opened()) this.menu().close();
    else this.menu().open(this.trigger().nativeElement);
  }
  key(event: KeyboardEvent) {
    if (!['ArrowDown', 'ArrowUp', 'Home', 'End'].includes(event.key)) return;
    event.preventDefault();
    this.menu().open(
      this.trigger().nativeElement,
      event.key === 'ArrowUp' || event.key === 'End' ? 'last' : 'first',
    );
  }
}
