import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { Icon } from './icon';

/**
 * 搜尋欄。打字即搜尋時傳 `busy`（防抖等待加上讀取中）：放大鏡換成轉圈、設 `aria-busy`，
 * 輸入框照常可打字；結果區的淡化由呼叫端照「重新讀取」處理。
 */
@Component({
  selector: 'nx-search-field',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './search-field.scss',
  host: { '[class.search-bare]': "appearance() === 'bare'" },
  template: `<div class="search-field">
    <nx-icon [name]="busy() ? 'loading' : 'search'" [class.is-busy]="busy()" /><input
      #field
      type="search"
      [attr.aria-label]="label()"
      [attr.aria-busy]="busy() || null"
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
        (click)="clearSearch(field)"
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
  /** 搜尋進行中：綁 `typing() || refreshing()`，防抖等待也算在內。 */
  readonly busy = input(false);
  readonly maxLength = input(120);
  /** `bare` 會去掉欄位外框；用於對話框列或面板已框住搜尋的地方。 */
  readonly appearance = input<'field' | 'bare'>('field');
  readonly valueChange = output<string>();

  clearSearch(field: HTMLInputElement) {
    this.valueChange.emit('');
    field.focus();
  }
}
