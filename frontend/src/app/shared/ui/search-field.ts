import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { Icon } from './icon';

@Component({
  selector: 'nx-search-field',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './search-field.scss',
  template: `<div class="search-field">
    <nx-icon name="search" /><input
      #field
      type="search"
      [attr.aria-label]="label()"
      [placeholder]="placeholder() || label()"
      [value]="value()"
      [disabled]="disabled()"
      [maxLength]="maxLength()"
      (input)="valueChange.emit(field.value)"
    />
    @if (value()) {
      <button
        type="button"
        class="icon-button"
        [attr.aria-label]="'清除' + label()"
        (click)="valueChange.emit(''); field.focus()"
      >
        <nx-icon name="close" />
      </button>
    }
  </div>`,
})
export class SearchField {
  readonly label = input.required<string>();
  readonly placeholder = input('');
  readonly value = input('');
  readonly disabled = input(false);
  readonly maxLength = input(120);
  readonly valueChange = output<string>();
}
