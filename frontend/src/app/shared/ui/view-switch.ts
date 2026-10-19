import { rovingIndex } from '../browser/roving-index';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  inject,
  input,
  output,
} from '@angular/core';
import { Icon } from './icon';
import { SlidingIndicator } from './sliding-indicator';

export interface ViewOption {
  value: string;
  label: string;
  icon?: string;
  disabled?: boolean;
}

/**
 * 一組互斥的篩選／檢視；一個指示器會滑到被按下的選項上。
 * `line` 為底線（頁面層級的檢視），`segment` 在軌道中抬起一個晶片（精簡篩選），
 * `fill` 讓選項平分寬度，icon-only 則抬起晶片但不顯示軌道。
 * 連結到 tabpanel 的情況仍使用 Tabs。
 */
@Component({
  selector: 'nx-view-switch',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './view-switch.scss',
  hostDirectives: [SlidingIndicator],
  host: {
    class: 'ui-view-switch',
    '[class.ui-view-switch-fill]': 'fill()',
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
  readonly fill = input(false);
  readonly valueChange = output<string>();
  private readonly element = inject<ElementRef<HTMLElement>>(ElementRef);
  key(event: KeyboardEvent, index: number) {
    const enabled = this.options()
      .map((option, index) => (option.disabled ? -1 : index))
      .filter((index) => index >= 0);
    if (!enabled.length || this.disabled()) return;
    const next = rovingIndex(event.key, enabled.indexOf(index), enabled.length, {
      orientation: 'horizontal',
    });
    if (next === null) return;
    event.preventDefault();
    this.valueChange.emit(this.options()[enabled[next]].value);
    const buttons = this.element.nativeElement.querySelectorAll<HTMLButtonElement>('button');
    buttons[enabled[next]]?.focus();
  }
}
