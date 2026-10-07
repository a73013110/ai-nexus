import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  inject,
  input,
  output,
} from '@angular/core';

export interface ViewOption {
  value: string;
  label: string;
}

/** A group of mutually exclusive filters/views. Tabs remain for linked tabpanels. */
@Component({
  selector: 'nx-view-switch',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'ui-view-switch', role: 'group', '[attr.aria-label]': 'label()' },
  template: `@for (option of options(); track option.value) {
    <button
      type="button"
      [attr.aria-pressed]="value() === option.value"
      [disabled]="disabled()"
      (click)="valueChange.emit(option.value)"
      (keydown)="key($event, $index)"
    >
      {{ option.label }}
    </button>
  }`,
})
export class ViewSwitch {
  readonly label = input.required<string>();
  readonly options = input.required<readonly ViewOption[]>();
  readonly value = input.required<string>();
  readonly disabled = input(false);
  readonly valueChange = output<string>();
  private readonly element = inject<ElementRef<HTMLElement>>(ElementRef);
  key(event: KeyboardEvent, index: number) {
    const length = this.options().length;
    if (!length || this.disabled()) return;
    let next: number;
    switch (event.key) {
      case 'ArrowRight':
        next = (index + 1) % length;
        break;
      case 'ArrowLeft':
        next = (index - 1 + length) % length;
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
    this.valueChange.emit(this.options()[next].value);
    this.element.nativeElement.querySelectorAll<HTMLButtonElement>('button')[next]?.focus();
  }
}
