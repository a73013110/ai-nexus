import { uniqueId } from '../browser/unique-id';
import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { Icon } from './icon';

/**
 * 原生核取方塊的語意，搭配一致的整列指標目標。只有方框顯示勾選狀態：
 * 列表中的列保持同一背景，因此 hover（`--hover`）不會與它混淆。
 */
@Component({
  selector: 'nx-checkbox',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './checkbox.scss',
  template: `<label class="selection-control" [class.is-disabled]="disabled()">
    <span class="selection-box"
      ><input
        type="checkbox"
        [attr.aria-label]="label()"
        [attr.aria-describedby]="description() ? id : null"
        [checked]="checked()"
        [disabled]="disabled()"
        (change)="checkedChange.emit($any($event.target).checked)" />
      <span class="selection-indicator" aria-hidden="true"><nx-icon name="check" /></span
    ></span>
    <span class="selection-copy"
      ><strong>{{ label() }}</strong>
      @if (description()) {
        <small [id]="id">{{ description() }}</small>
      }
    </span>
  </label>`,
})
export class Checkbox {
  readonly id = uniqueId('nx-checkbox');
  readonly label = input.required<string>();
  readonly description = input('');
  readonly checked = input(false);
  readonly disabled = input(false);
  readonly checkedChange = output<boolean>();
}
