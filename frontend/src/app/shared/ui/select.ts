import { rovingIndex } from '../browser/roving-index';
import { uniqueId } from '../browser/unique-id';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  afterRenderEffect,
  computed,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { Icon } from './icon';
import { OptionTags, type OptionTag } from './option-tags';
import { SearchField } from './search-field';
import { anchoredPopover } from '../browser/anchored-popover';
import { positionPopover } from '../browser/popover-position';

export interface SelectOption {
  value: string;
  label: string;
  /** 第二行：識別資訊（帳號、代碼）或一句短說明；細節放選定後的欄位說明，狀態放 `tags`。 */
  description?: string;
  /** 名稱旁的狀態標籤，已選時也顯示在觸發鈕；搜尋找得到標籤文字。 */
  tags?: readonly OptionTag[];
  /** 分組標題；相鄰且同組的選項排在同一個標題下，搜尋到組名時整組都列出。 */
  group?: string;
  /** 只給搜尋比對、不顯示的文字（代碼、別名）。 */
  keywords?: string;
  disabled?: boolean;
}

/** 一段連續同組的選項；沒有 `group` 的選項自成一段、不加標題。 */
interface SelectSection {
  group?: string;
  entries: { option: SelectOption; index: number }[];
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
 * 選項來自 API 時傳 `loading`：觸發鈕轉圈、還對不到選項的值顯示「正在載入…」而不是 placeholder
 * （看起來會像沒選）；載入中仍可打開，選單裡是「正在載入選項…」而不是「沒有符合的選項」。
 * 長清單可給選項 `group` 分組（標題不可選，報讀器以 `role="group"` 讀出組名），
 * 搜尋比對名稱、說明、標籤、組名與 `keywords`。
 * 階層選項（單位）用 `TreeSelect`。
 */
@Component({
  selector: 'nx-select',
  imports: [Icon, NgTemplateOutlet, OptionTags, SearchField],
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
      [attr.aria-busy]="loading() || null"
      [disabled]="disabled() || (!options().length && !loading())"
      (click)="toggle()"
      (keydown)="key($event)"
    >
      <span class="select-value" [attr.title]="title()">
        <span>{{ summary() }}</span>
        @let tags = multiple() ? undefined : selected()?.tags;
        @if (tags?.length) {
          <nx-option-tags [tags]="tags" />
        }
      </span>
      @if (loading()) {
        <nx-icon name="loading" class="select-loading" />
      } @else {
        <nx-icon name="chevron" class="select-chevron" />
      }
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
      (keydown.escape)="dismiss($event)"
    >
      @if (search()) {
        <nx-search-field
          class="popover-search"
          appearance="filled"
          [label]="'搜尋' + label()"
          [placeholder]="'搜尋' + label() + '…'"
          [controls]="id"
          [activeDescendant]="active() >= 0 ? id + '-' + active() : null"
          [busy]="loading()"
          [value]="query()"
          (valueChange)="filter($event)"
          (keydown)="searchKey($event)"
        />
        <p class="sr-only" role="status">{{ visibleOptions().length }} 個選項</p>
      }
      <ng-template #optionRow let-entry>
        @let option = entry.option; @let i = entry.index;
        <button
          type="button"
          role="option"
          tabindex="-1"
          class="option-row"
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
            <span class="option-lead" aria-hidden="true">
              <span class="option-box">
                @if (isSelected(option.value)) {
                  <nx-icon name="check" />
                }
              </span>
            </span>
          }
          <span class="option-text">
            <span class="option-label" [attr.title]="option.label">{{ option.label }}</span>
            @if (option.description) {
              <small>{{ option.description }}</small>
            }
          </span>
          @if (option.tags?.length) {
            <nx-option-tags [tags]="option.tags" />
          }
          @if (!multiple() && option.value === value()) {
            <nx-icon name="check" class="option-mark" />
          }
        </button>
      </ng-template>
      <!-- 搜尋固定在頂端，選項與 footer 在下方一起捲動。 -->
      <div class="select-body option-list">
        <div
          role="listbox"
          [id]="id"
          [attr.aria-label]="label()"
          [attr.aria-multiselectable]="multiple() || null"
        >
          @for (section of sections(); track $index) {
            @if (section.group) {
              <div role="group" [attr.aria-labelledby]="id + '-group-' + $index">
                <div class="option-group" [id]="id + '-group-' + $index">{{ section.group }}</div>
                @for (entry of section.entries; track entry.option.value) {
                  <ng-container *ngTemplateOutlet="optionRow; context: { $implicit: entry }" />
                }
              </div>
            } @else {
              @for (entry of section.entries; track entry.option.value) {
                <ng-container *ngTemplateOutlet="optionRow; context: { $implicit: entry }" />
              }
            }
          } @empty {
            @if (loading()) {
              <p class="option-empty" role="status">正在載入選項…</p>
            } @else {
              <p class="option-empty">沒有符合的選項</p>
            }
          }
        </div>
        <ng-content select="[select-footer]" />
      </div>
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
  /** 選項讀取中（首次載入或重讀）；綁 `apiResource` 的 `refreshing()`。 */
  readonly loading = input(false);
  readonly placeholder = input('請選擇');
  readonly searchable = input(false);
  /** 選項達到這個數量才顯示搜尋框，短清單打開直接是選項。 */
  readonly searchFrom = input(0);
  readonly search = computed(() => this.searchable() && this.options().length >= this.searchFrom());
  /**
   * 依所在位置選：`field` 是表單裡要送出的值（6px）；`pill` 是工具列上立即篩選畫面的膠囊，
   * 與並排的 `SearchField` 同形；`inline` 去掉外框，用在本身就是外框的列（輸入區控制列、浮動工具列）。
   */
  readonly appearance = input<'field' | 'inline' | 'pill'>('field');
  readonly query = signal('');
  /** 每個選項的搜尋文字，選項不變就不重算（長清單每打一個字都要比對全部）。 */
  private readonly searchText = computed(() =>
    this.options().map((option) =>
      [
        option.label,
        option.description,
        option.group,
        option.keywords,
        ...(option.tags ?? []).map((tag) => tag.label),
      ]
        .join(' ')
        .toLocaleLowerCase(),
    ),
  );
  readonly visibleOptions = computed(() => {
    const query = this.query().trim().toLocaleLowerCase();
    const text = this.searchText();
    return this.options()
      .map((option, index) => ({ option, index }))
      .filter(({ index }) => !query || text[index].includes(query));
  });
  /** 依 `group` 把相鄰的可見選項分段；方向鍵仍依 `visibleOptions` 的順序移動。 */
  readonly sections = computed(() =>
    this.visibleOptions().reduce<SelectSection[]>((sections, entry) => {
      const last = sections.at(-1);
      const group = entry.option.group;
      if (last && group && last.group === group) last.entries.push(entry);
      else sections.push({ group, entries: [entry] });
      return sections;
    }, []),
  );
  readonly valueChange = output<string>();
  readonly valuesChange = output<string[]>();
  readonly selected = computed(() => this.options().find((item) => item.value === this.value()));
  private readonly chosen = computed(() =>
    this.options().filter((option) => this.values().includes(option.value)),
  );
  readonly summary = computed(() => {
    // 選項還沒到時對不到名稱：維持「正在載入…」，不退回 placeholder（已選的值會像被清掉）。
    const empty = this.loading() ? '正在載入…' : this.placeholder();
    if (!this.multiple()) return this.selected()?.label || empty;
    const chosen = this.chosen();
    return chosen.length === 0
      ? empty
      : chosen.length === 1
        ? chosen[0].label
        : `已選 ${chosen.length} 項`;
  });
  readonly title = computed(() => {
    if (this.multiple() && this.chosen().length > 1)
      return this.chosen()
        .map((option) => option.label)
        .join('、');
    return this.summary();
  });
  readonly active = signal(-1);
  readonly id = uniqueId('nx-select');
  private readonly trigger = viewChild.required<ElementRef<HTMLButtonElement>>('trigger');
  private readonly panel = viewChild.required<ElementRef<HTMLDivElement>>('panel');
  protected readonly popover = anchoredPopover({
    panel: () => this.panel().nativeElement,
    trigger: () => this.trigger().nativeElement,
    closeOnFocusOut: true,
    place: (panel) =>
      positionPopover(this.trigger().nativeElement, panel, this.search() ? 360 : 240),
  });
  readonly opened = this.popover.opened;
  private typed = '';
  private searchTimer?: ReturnType<typeof setTimeout>;
  constructor() {
    inject(DestroyRef).onDestroy(() => clearTimeout(this.searchTimer));
    // 載入中打開的面板，選項到了之後高度會變，重新對齊錨點才不會超出視窗。
    afterRenderEffect(() => {
      this.options();
      this.popover.reposition();
    });
  }
  toggle() {
    this.opened() ? this.close() : this.open();
  }
  private open() {
    if (this.disabled() || (!this.options().length && !this.loading())) return;
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
