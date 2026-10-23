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
      [attr.aria-busy]="busy() || null"
      [attr.aria-disabled]="busy() || null"
      (click)="toggle()"
      (keydown)="key($event)"
    >
      @if (profile()) {
        <ng-content />
      } @else {
        <nx-icon [name]="busy() ? 'loading' : icon()" />
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
  /**
   * 從選單發起的動作（匯出、複製、刪除）進行中：觸發鈕轉圈並擋下再次開啟（只用 aria-disabled，
   * 焦點留在觸發鈕）。列表中每列各有一個選單時，只綁正在處理的那一列。
   */
  readonly busy = input(false);
  readonly action = output<string>();
  private readonly trigger = viewChild.required<ElementRef<HTMLButtonElement>>('trigger');
  private readonly menu = viewChild.required(MenuPanel);
  toggle() {
    if (this.busy()) return;
    if (this.menu().opened()) this.menu().close();
    else this.menu().open(this.trigger().nativeElement);
  }
  key(event: KeyboardEvent) {
    if (this.busy() || !['ArrowDown', 'ArrowUp', 'Home', 'End'].includes(event.key)) return;
    event.preventDefault();
    this.menu().open(
      this.trigger().nativeElement,
      event.key === 'ArrowUp' || event.key === 'End' ? 'last' : 'first',
    );
  }
}
