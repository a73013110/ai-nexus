import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  inject,
  input,
  output,
} from '@angular/core';
import { Icon } from './icon';

export interface ViewOption {
  value: string;
  label: string;
  icon?: string;
  disabled?: boolean;
}

/** A group of mutually exclusive filters/views. Tabs remain for linked tabpanels. */
@Component({
  selector: 'nx-view-switch',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './view-switch.scss',
  host: {
    class: 'ui-view-switch',
    role: 'group',
    '[attr.aria-label]': 'label()',
    '[class.ui-view-switch-icons]': 'iconOnly()',
    '[class.ui-view-switch-segment]': "appearance() === 'segment'",
  },
  template: `@for (option of options(); track option.value) {
    <button
      type="button"
      [class.icon-button]="iconOnly()"
      [attr.aria-label]="option.label"
      [attr.title]="iconOnly() ? option.label : null"
      [attr.aria-pressed]="value() === option.value"
      [disabled]="disabled() || option.disabled"
      (click)="valueChange.emit(option.value)"
      (keydown)="key($event, $index)"
    >
      @if (option.icon) {
        <nx-icon [name]="option.icon" />
      }
      @if (!iconOnly()) {
        <span>{{ option.label }}</span>
      }
    </button>
  }`,
})
export class ViewSwitch {
  readonly label = input.required<string>();
  readonly options = input.required<readonly ViewOption[]>();
  readonly value = input.required<string>();
  readonly disabled = input(false);
  readonly iconOnly = input(false);
  readonly appearance = input<'line' | 'segment'>('line');
  readonly valueChange = output<string>();
  private readonly element = inject<ElementRef<HTMLElement>>(ElementRef);
  key(event: KeyboardEvent, index: number) {
    const enabled = this.options()
      .map((option, index) => (option.disabled ? -1 : index))
      .filter((index) => index >= 0);
    const length = enabled.length;
    if (!length || this.disabled()) return;
    const current = enabled.indexOf(index);
    let next: number;
    switch (event.key) {
      case 'ArrowRight':
        next = (current + 1) % length;
        break;
      case 'ArrowLeft':
        next = (current - 1 + length) % length;
        break;
      case 'Home':
        next = 0;
        break;
      case 'End':
        next = length - 1;
        break;
      default:
        return;
    }
    event.preventDefault();
    this.valueChange.emit(this.options()[enabled[next]].value);
    const buttons = this.element.nativeElement.querySelectorAll<HTMLButtonElement>('button');
    buttons[enabled[next]]?.focus();
  }
}
