import { rovingIndex } from '../browser/roving-index';
import { uniqueId } from '../browser/unique-id';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  computed,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { Icon } from './icon';
import { anchoredPopover } from '../browser/anchored-popover';
import { positionPopover } from '../browser/popover-position';

export interface SelectOption {
  value: string;
  label: string;
  description?: string;
  disabled?: boolean;
}

/**
 * 單選或多選的下拉清單。`multiple` 以核取方塊切換選項、選後不關閉，讀寫 `values`；
 * trigger 顯示單一選項名稱或「已選 N 項」，沒選時顯示 placeholder（篩選器的 placeholder
 * 即「全部」）。
 * 多選的摘要只有「已選 N 項」，trigger 改以 `aria-labelledby` 指向欄位名稱＋完整已選內容，
 * 報讀器才讀得到選了什麼。
 * 面板在原生 top layer，不會被對話框或捲動區裁切。
 * 投影的 `[select-footer]` 放在選項下方的相關設定（例如模型下的推理強度）；Tab 會移入，
 * 焦點離開面板即關閉。
 */
@Component({
  selector: 'nx-select',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './select.scss',
  host: {
    '[class.select-inline]': "appearance() === 'inline'",
    '[class.select-pill]': "appearance() === 'pill'",
  },
  template: `<button
      #trigger
      type="button"
      class="select-trigger"
      role="combobox"
      [attr.aria-label]="multiple() ? null : label()"
      [attr.aria-labelledby]="multiple() ? id + '-label ' + id + '-value' : null"
      [attr.aria-expanded]="opened()"
      [attr.aria-haspopup]="search() ? 'dialog' : 'listbox'"
      [attr.aria-controls]="search() ? id + '-panel' : id"
      [attr.aria-activedescendant]="opened() && active() >= 0 ? id + '-' + active() : null"
      [disabled]="disabled() || !options().length"
      (click)="toggle()"
      (keydown)="key($event)"
    >
      <span [attr.title]="title()">{{ summary() }}</span
      ><nx-icon name="chevron" />
    </button>
    @if (multiple()) {
      <span hidden [id]="id + '-label'">{{ label() }}</span>
      <span hidden [id]="id + '-value'">{{ title() }}</span>
    }
    <!-- 焦點留在投影的 footer 控制項上，面板只轉送它們的 Escape。 -->
    <!-- eslint-disable-next-line @angular-eslint/template/interactive-supports-focus -->
    <div
      #panel
      [id]="id + '-panel'"
      popover="auto"
      class="popover-panel select-panel"
      [attr.role]="search() ? 'dialog' : null"
      [attr.aria-label]="label()"
      (toggle)="popover.sync($event)"
      (focusout)="leave($event)"
      (keydown.escape)="dismiss($event)"
    >
      @if (search()) {
        <div class="select-search">
          <nx-icon name="search" /><input
            type="search"
            role="combobox"
            aria-expanded="true"
            aria-haspopup="listbox"
            [attr.aria-controls]="id"
            [attr.aria-activedescendant]="active() >= 0 ? id + '-' + active() : null"
            [attr.aria-label]="'搜尋' + label()"
            [placeholder]="'搜尋' + label() + '…'"
            [value]="query()"
            (input)="filter($any($event.target).value)"
            (keydown)="searchKey($event)"
          />
        </div>
        <p class="sr-only" role="status">{{ visibleOptions().length }} 個選項</p>
      }
      <div
        role="listbox"
        [id]="id"
        [attr.aria-label]="label()"
        [attr.aria-multiselectable]="multiple() || null"
      >
        @for (entry of visibleOptions(); track entry.option.value) {
          @let option = entry.option; @let i = entry.index;
          <button
            type="button"
            role="option"
            tabindex="-1"
            class="select-option"
            [class.is-multiple]="multiple()"
            [id]="id + '-' + i"
            [attr.aria-selected]="isSelected(option.value)"
            [attr.aria-disabled]="!!option.disabled"
            [disabled]="option.disabled"
            [class.is-active]="active() === i"
            (pointermove)="active.set(i)"
            (mousedown)="$event.preventDefault()"
            (click)="choose(i)"
          >
            @if (multiple()) {
              <span class="select-check" aria-hidden="true">
                @if (isSelected(option.value)) {
                  <nx-icon name="check" />
                }
              </span>
            }
            <span
              ><strong [attr.title]="option.label">{{ option.label }}</strong>
              @if (option.description) {
                <small>{{ option.description }}</small>
              }
            </span>
            @if (!multiple() && option.value === value()) {
              <nx-icon name="check" />
            }
          </button>
        } @empty {
          <p class="select-empty">沒有符合的選項</p>
        }
      </div>
      <ng-content select="[select-footer]" />
    </div>`,
})
export class Select {
  readonly label = input.required<string>();
  readonly value = input('');
  readonly multiple = input(false);
  /** `multiple` 時已選的值。 */
  readonly values = input<readonly string[]>([]);
  readonly options = input.required<SelectOption[]>();
  readonly disabled = input(false);
  readonly placeholder = input('請選擇');
  readonly searchable = input(false);
  /** 選項達到這個數量才顯示搜尋框，短清單打開直接是選項。 */
  readonly searchFrom = input(0);
  readonly search = computed(() => this.searchable() && this.options().length >= this.searchFrom());
  /** `inline` 去掉欄位外框，用在輸入區控制列這類工具列。 */
  readonly appearance = input<'field' | 'inline' | 'pill'>('field');
  readonly query = signal('');
  readonly visibleOptions = computed(() =>
    this.options()
      .map((option, index) => ({ option, index }))
      .filter(({ option }) =>
        `${option.label} ${option.description || ''}`
          .toLocaleLowerCase()
          .includes(this.query().trim().toLocaleLowerCase()),
      ),
  );
  readonly valueChange = output<string>();
  readonly valuesChange = output<string[]>();
  readonly selected = computed(() => this.options().find((item) => item.value === this.value()));
  private readonly chosen = computed(() =>
    this.options().filter((option) => this.values().includes(option.value)),
  );
  readonly summary = computed(() => {
    if (!this.multiple()) return this.selected()?.label || this.placeholder();
    const chosen = this.chosen();
    return chosen.length === 0
      ? this.placeholder()
      : chosen.length === 1
        ? chosen[0].label
        : `已選 ${chosen.length} 項`;
  });
  readonly title = computed(() =>
    this.multiple() && this.chosen().length > 1
      ? this.chosen()
          .map((option) => option.label)
          .join('、')
      : this.summary(),
  );
  readonly active = signal(-1);
  readonly id = uniqueId('nx-select');
  private readonly trigger = viewChild.required<ElementRef<HTMLButtonElement>>('trigger');
  private readonly panel = viewChild.required<ElementRef<HTMLDivElement>>('panel');
  protected readonly popover = anchoredPopover({
    panel: () => this.panel().nativeElement,
    place: (panel) =>
      positionPopover(this.trigger().nativeElement, panel, this.search() ? 360 : 240),
  });
  readonly opened = this.popover.opened;
  private typed = '';
  private searchTimer?: ReturnType<typeof setTimeout>;
  constructor() {
    inject(DestroyRef).onDestroy(() => clearTimeout(this.searchTimer));
  }
  toggle() {
    this.opened() ? this.close() : this.open();
  }
  private open() {
    if (this.disabled() || !this.options().length) return;
    this.query.set('');
    this.active.set(
      Math.max(
        0,
        this.options().findIndex((item) => this.isSelected(item.value)),
      ),
    );
    this.popover.show();
    this.reveal();
  }
  close() {
    this.popover.hide();
  }
  isSelected(value: string) {
    return this.multiple() ? this.values().includes(value) : value === this.value();
  }
  choose(index: number) {
    const option = this.options()[index];
    if (!option || option.disabled) return;
    if (this.multiple()) {
      // 依選項順序而非點選順序輸出，同一組選擇永遠讀起來一樣。
      const values = this.isSelected(option.value)
        ? this.values().filter((value) => value !== option.value)
        : [...this.values(), option.value];
      this.valuesChange.emit(
        this.options()
          .map((item) => item.value)
          .filter((value) => values.includes(value)),
      );
      return;
    }
    this.valueChange.emit(option.value);
    this.close();
    this.trigger().nativeElement.focus();
  }
  key(event: KeyboardEvent) {
    if (event.isComposing) return;
    const key = event.key;
    if (key === 'Tab') {
      if (!event.shiftKey && this.opened() && this.focusFooter()) event.preventDefault();
      else this.close();
      return;
    }
    if (key === 'Escape' && this.opened()) {
      event.preventDefault();
      event.stopPropagation();
      this.close();
      return;
    }
    if (['ArrowDown', 'ArrowUp', 'Home', 'End'].includes(key)) {
      event.preventDefault();
      if (!this.opened()) this.open();
      const enabled = this.visibleOptions()
        .map(({ option, index }) => (option.disabled ? -1 : index))
        .filter((item) => item >= 0);
      if (!enabled.length) return;
      this.active.set(enabled[rovingIndex(key, enabled.indexOf(this.active()), enabled.length)!]);
      this.reveal();
      return;
    }
    if ((key === 'Enter' || key === ' ') && this.opened()) {
      event.preventDefault();
      this.choose(this.active());
      return;
    }
    if (key.length === 1 && key !== ' ' && !event.ctrlKey && !event.metaKey && !event.altKey) {
      event.preventDefault();
      if (!this.opened()) this.open();
      clearTimeout(this.searchTimer);
      this.typed += key.toLocaleLowerCase();
      const index = this.options().findIndex(
        (item) => !item.disabled && item.label.toLocaleLowerCase().startsWith(this.typed),
      );
      if (index >= 0) {
        this.active.set(index);
        this.reveal();
      }
      this.searchTimer = setTimeout(() => (this.typed = ''), 650);
    }
  }
  private focusFooter() {
    const footer = this.panel().nativeElement.querySelector<HTMLElement>('[select-footer]');
    if (!footer?.checkVisibility()) return false;
    const target =
      footer.querySelector<HTMLElement>('input:checked:enabled') ??
      footer.querySelector<HTMLElement>('input:enabled, button:enabled');
    target?.focus();
    return !!target;
  }
  leave(event: FocusEvent) {
    const next = event.relatedTarget as Node | null;
    if (
      this.opened() &&
      next &&
      !this.panel().nativeElement.contains(next) &&
      next !== this.trigger().nativeElement
    )
      this.close();
  }
  /** footer 控制項上的 Escape；trigger 與搜尋框自己處理。 */
  dismiss(event: Event) {
    event.preventDefault();
    this.close();
    this.trigger().nativeElement.focus();
  }
  private reveal() {
    requestAnimationFrame(() =>
      document.getElementById(`${this.id}-${this.active()}`)?.scrollIntoView({ block: 'nearest' }),
    );
  }
  filter(query: string) {
    this.query.set(query);
    this.active.set(this.visibleOptions().find((item) => !item.option.disabled)?.index ?? -1);
    this.popover.reposition();
  }
  searchKey(event: KeyboardEvent) {
    if (['ArrowDown', 'ArrowUp', 'Home', 'End', 'Enter', 'Escape', 'Tab'].includes(event.key))
      this.key(event);
    if (event.key === 'Escape') this.trigger().nativeElement.focus();
  }
}
