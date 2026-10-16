import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { Icon } from './icon';

/**
 * Position and previous/next for a detail drawer over the shown page of a table
 * (`drawer-actions`). It moves within that page only; the table owns paging.
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
  /** Position of the open row in the shown page, or -1 when it is not on it. */
  readonly index = input(-1);
  readonly count = input(0);
  /** What a row is, for the button names: 「日誌」 reads 「上一筆日誌」. */
  readonly noun = input('');
  readonly move = output<-1 | 1>();
}
