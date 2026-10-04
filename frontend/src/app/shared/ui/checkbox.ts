import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { Icon } from './icon';
let sequence = 0;

/** Native checkbox semantics with a consistent, whole-row pointer target. */
@Component({
  selector: 'nx-checkbox',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<label
    class="selection-control"
    [class.is-selected]="checked()"
    [class.is-disabled]="disabled()"
  >
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
  readonly id = `nx-checkbox-${++sequence}`;
  readonly label = input.required<string>();
  readonly description = input('');
  readonly checked = input(false);
  readonly disabled = input(false);
  readonly checkedChange = output<boolean>();
}
