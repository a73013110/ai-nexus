import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import type { SelectOption } from '@shared/ui/select';

let sequence = 0;

/**
 * 推理強度，作為模型面板中的一個區段。窄版輸入框改為顯示它，而不是第二個
 * select so the model name keeps the row; wide composers hide it and keep the 「思考強度」 select.
 * Enter 會被吞掉，因為在 radio 上它會送出輸入框的表單。
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
