import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  inject,
  input,
  output,
} from '@angular/core';
import { Icon } from './icon';

export interface Chip {
  id: string;
  label: string;
}

/**
 * 已套用條件的 chip 列（查詢列與表格的欄位篩選共用），每個 chip 可單獨移除。
 * 移除後焦點移到補上同位置的 chip；沒有 chip 了就發出 `allRemoved`，由呼叫端把焦點放到合適的位置，
 * 避免焦點掉回 body。讀取中用 `aria-disabled` 擋，按鈕才不會失去焦點。
 */
@Component({
  selector: 'nx-chip-list',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './chip-list.scss',
  template: `<ul [attr.aria-label]="label()">
    @for (chip of chips(); track chip.id; let i = $index) {
      <li>
        <span [title]="chip.label">{{ chip.label }}</span>
        <button
          type="button"
          data-chip-remove
          [attr.aria-label]="'移除條件：' + chip.label"
          title="移除條件"
          [attr.aria-disabled]="busy() || null"
          (click)="drop(chip, i)"
        >
          <nx-icon name="close" />
        </button>
      </li>
    }
  </ul>`,
})
export class ChipList<T extends Chip = Chip> {
  readonly chips = input.required<readonly T[]>();
  readonly label = input('已套用條件');
  readonly busy = input(false);
  readonly chipRemove = output<T>();
  readonly allRemoved = output<void>();
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;

  drop(chip: T, index: number) {
    if (this.busy()) return;
    this.chipRemove.emit(chip);
    requestAnimationFrame(() => {
      const buttons = this.host.querySelectorAll<HTMLButtonElement>('[data-chip-remove]');
      const next = buttons[Math.min(index, buttons.length - 1)];
      if (next) next.focus();
      else this.allRemoved.emit();
    });
  }
}
