import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import type { SelectOption } from '@shared/ui/select';

let sequence = 0;

/**
 * Reasoning effort as a section of the model panel. Narrow composers show it instead of a second
 * select so the model name keeps the row; wide composers hide it and keep the 「思考強度」 select.
 * Enter is swallowed because on a radio it would submit the composer form.
 */
@Component({
  selector: 'nx-reasoning-choice',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './reasoning-choice.scss',
  template: `<fieldset>
    <legend>思考強度</legend>
    <div class="reasoning-options">
      @for (option of options(); track option.value) {
        <label
          ><input
            type="radio"
            class="sr-only"
            [name]="name"
            [value]="option.value"
            [checked]="option.value === value()"
            [disabled]="disabled()"
            (change)="valueChange.emit(option.value)"
            (keydown.enter)="$event.preventDefault()"
          /><span>{{ option.label }}</span></label
        >
      }
    </div>
  </fieldset>`,
})
export class ReasoningChoice {
  readonly name = `nx-reasoning-${++sequence}`;
  readonly options = input.required<SelectOption[]>();
  readonly value = input('auto');
  readonly disabled = input(false);
  readonly valueChange = output<string>();
}
