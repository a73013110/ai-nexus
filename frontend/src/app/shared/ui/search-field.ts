import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  input,
  output,
  viewChild,
} from '@angular/core';
import { Icon } from './icon';

/**
 * 搜尋欄。打字即搜尋時傳 `busy`（防抖等待加上讀取中）：放大鏡換成轉圈、設 `aria-busy`，
 * 輸入框照常可打字；結果區的淡化由呼叫端照「重新讀取」處理。
 * 搜尋帶動一份清單（下拉、樹、指令清單）時給 `controls`：輸入框成為 combobox，
 * 方向鍵與 Enter 由呼叫端在 `<nx-search-field>` 上聽冒泡的 `keydown` 處理。
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
      [attr.role]="controls() ? 'combobox' : null"
      [attr.aria-expanded]="controls() ? true : null"
      [attr.aria-haspopup]="controls() ? popup() : null"
      [attr.aria-controls]="controls() || null"
      [attr.aria-activedescendant]="controls() ? activeDescendant() : null"
      [attr.autocomplete]="controls() ? 'off' : null"
      [placeholder]="placeholder() || label()"
      [value]="value()"
      [disabled]="disabled()"
      [maxLength]="maxLength()"
      (input)="valueChange.emit(field.value)"
    />
    @if (value()) {
      <!-- 按下不搶焦點：游標留在輸入框，浮層裡的搜尋也不會因焦點移動而關閉。 -->
      <button
        type="button"
        class="icon-button"
        [attr.aria-label]="'清除' + label()"
        [attr.tabindex]="controls() ? -1 : null"
        (mousedown)="$event.preventDefault()"
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
  /**
   * `bare` 去掉欄位外框，用在已有外框的列：對話框標題列（`.dialog-bar`）、
   * 浮層頂端（加 `class="popover-search"`）、側欄頂端。
   */
  readonly appearance = input<'field' | 'bare'>('field');
  /**
   * 這個搜尋過濾的清單 id。給了才是 combobox；清除鈕此時不進 Tab 順序，
   * 因為浮層裡的 Tab 是離開浮層；鍵盤以全選刪除清空。
   */
  readonly controls = input<string>();
  /** `controls` 指向的清單角色。 */
  readonly popup = input<'listbox' | 'tree'>('listbox');
  /** 清單中目前標示的選項 id；焦點留在輸入框，報讀器靠它讀出選項。 */
  readonly activeDescendant = input<string | null>(null);
  readonly valueChange = output<string>();
  private readonly field = viewChild.required<ElementRef<HTMLInputElement>>('field');

  focus() {
    this.field().nativeElement.focus();
  }

  clearSearch(field: HTMLInputElement) {
    this.valueChange.emit('');
    field.focus();
  }
}
