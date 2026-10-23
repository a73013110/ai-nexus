import { uniqueId } from '../browser/unique-id';
import { ChangeDetectionStrategy, Component, booleanAttribute, input, output } from '@angular/core';
import { Icon } from './icon';

/**
 * 原生核取方塊的語意，搭配一致的整列指標目標。只有方框顯示勾選狀態：
 * 列表中的列保持同一背景，因此 hover（`--hover`）不會與它混淆。
 * `mixed` 是表頭全選的「部分已選」（原生 indeterminate，報讀為 mixed）；`hideLabel` 只留方框，
 * 名稱仍給輔助技術（表格的選取欄，姓名另有欄位顯示）。
 */
@Component({
  selector: 'nx-checkbox',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './checkbox.scss',
  template: `<label
    class="selection-control"
    [class.is-disabled]="disabled()"
    [class.is-label-hidden]="hideLabel()"
  >
    <span class="selection-box"
      ><input
        type="checkbox"
        [attr.aria-label]="label()"
        [attr.aria-describedby]="description() ? id : null"
        [checked]="checked()"
        [indeterminate]="mixed() && !checked()"
        [disabled]="disabled()"
        (change)="checkedChange.emit($any($event.target).checked)" />
      <span class="selection-indicator" aria-hidden="true"
        ><nx-icon [name]="mixed() && !checked() ? 'minus' : 'check'" /></span
    ></span>
    @if (!hideLabel()) {
      <span class="selection-copy"
        ><strong>{{ label() }}</strong>
        @if (description()) {
          <small [id]="id">{{ description() }}</small>
        }
      </span>
    }
  </label>`,
})
export class Checkbox {
  readonly id = uniqueId('nx-checkbox');
  readonly label = input.required<string>();
  readonly description = input('');
  readonly checked = input(false);
  readonly disabled = input(false);
  readonly mixed = input(false);
  readonly hideLabel = input(false, { transform: booleanAttribute });
  readonly checkedChange = output<boolean>();
}
