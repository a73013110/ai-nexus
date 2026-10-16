import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { Icon } from './icon';

/**
 * 詳情抽屜的「第 N／M 筆」與上一筆、下一筆（放在 `drawer-actions`）。
 * 只在表格目前這一頁內移動；跨頁由表格的分頁負責。
 */
@Component({
  selector: 'nx-drawer-pager',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'ui-drawer-pager' },
  styles: `
    :host {
      display: flex;
      align-items: center;
      gap: var(--p-space-1);
      color: var(--secondary);
      font-size: var(--p-font-xs);
      font-variant-numeric: tabular-nums;
    }
  `,
  template: `@if (index() >= 0) {
      <span>{{ index() + 1 }} / {{ count() }}</span>
    }
    <button
      type="button"
      class="icon-button"
      [attr.aria-label]="'上一筆' + noun()"
      [title]="'上一筆' + noun()"
      [disabled]="index() <= 0"
      (click)="move.emit(-1)"
    >
      <nx-icon name="left" />
    </button>
    <button
      type="button"
      class="icon-button"
      [attr.aria-label]="'下一筆' + noun()"
      [title]="'下一筆' + noun()"
      [disabled]="index() < 0 || index() >= count() - 1"
      (click)="move.emit(1)"
    >
      <nx-icon name="chevron" />
    </button>`,
})
export class DrawerPager {
  /** 開啟中的列在目前這一頁的位置；不在這一頁時為 -1。 */
  readonly index = input(-1);
  readonly count = input(0);
  /** 列的名稱，組成按鈕文字：「日誌」→「上一筆日誌」。 */
  readonly noun = input('');
  readonly move = output<-1 | 1>();
}
