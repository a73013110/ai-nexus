import { rovingIndex } from '../browser/roving-index';
import { uniqueId } from '../browser/unique-id';
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
  /** 讓此項目成為顯示狀態的核取方塊（menuitemcheckbox）。 */
  checked?: boolean;
  /** 此項目的動作進行中：圖示換成轉圈，點擊不關閉選單也不送出。 */
  busy?: boolean;
}

/**
 * 在任何觸發元素旁開啟的最上層選單。`ActionMenu` 與自己的按鈕配對；列表則只渲染一個面板，
 * 由每一列開啟它，因此列保持為一般元素。
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
          [attr.aria-busy]="item.busy || null"
          [attr.aria-disabled]="item.busy || null"
          [class.danger-text]="item.danger"
          (click)="item.busy || choose(item.id)"
        >
          <nx-icon [name]="item.busy ? 'loading' : item.icon" />{{ item.label }}
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
  /** 最窄寬度；0 代表與觸發元素相同，與帳號選單的做法一致。 */
  readonly minWidth = input(260);
  /** `beside` 會在圖示欄觸發元素的右側開啟，而不是下方。 */
  readonly placement = input<'below' | 'beside'>('below');
  /** 觸發元素無法顯示的脈絡，例如收合圖示欄上的帳號名稱。 */
  readonly heading = input<{ title: string; detail?: string } | null>(null);
  readonly action = output<string>();
  readonly id = uniqueId('nx-menu');
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
  /** 在 `anchor` 旁開啟；用鍵盤關閉時焦點回到該處。 */
  open(anchor: HTMLElement, focus: 'first' | 'last' = 'first') {
    if (this.opened()) this.popover.hide();
    this.anchor = anchor;
    this.popover.show();
    // 項目可能屬於剛剛請求的那一列，所以要等它們渲染完成後再放置與聚焦。
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
      const current = buttons.findIndex((item) => item === document.activeElement);
      buttons[rovingIndex(event.key, current, buttons.length)!]?.focus();
    }
  }
  private buttons() {
    return Array.from(
      this.panel().nativeElement.querySelectorAll<HTMLButtonElement>('button:enabled'),
    );
  }
}
