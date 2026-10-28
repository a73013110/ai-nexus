import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  input,
  output,
} from '@angular/core';
import { Checkbox } from './checkbox';
import { DateTimePicker } from './date-time-picker';
import { Field } from './field';
import { Icon } from './icon';
import { filterOptions, type ColumnFilter, type TableColumnDef } from './table-model';

/** 文字篩選停止輸入多久才套用；Enter 立即套用。 */
const TEXT_DELAY = 300;

/**
 * 欄位篩選的編輯區（放在 `th[nxHeader]` 的 popover 裡），依欄位的 `filter` 型別顯示文字、選項或範圍。
 * 改了就套用（文字等停止輸入），不另設「套用」鈕；「清除」移除這一欄的篩選。
 */
@Component({
  selector: 'nx-table-filter',
  imports: [Checkbox, DateTimePicker, Field, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: grid;
      gap: var(--p-space-2);
    }
    .ui-filter-range {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: var(--p-space-2);
    }
    .ui-filter-options {
      display: grid;
      max-height: 15rem;
      overflow: auto;
    }
    nx-checkbox {
      --selection-control-height: var(--control-height-compact);
      --selection-control-padding-block: var(--p-space-1);
      --selection-control-padding-inline: var(--p-space-2);
    }
    .quiet-button {
      justify-self: start;
    }
  `,
  template: `@switch (type()) {
      @case ('text') {
        <label class="ui-field"
          >包含文字<input
            nxField
            type="search"
            maxlength="200"
            autocomplete="off"
            [value]="text()"
            (input)="typed($any($event.target).value)"
            (keydown.enter)="typedNow($any($event.target).value, $event)"
        /></label>
      }
      @case ('options') {
        <div class="ui-filter-options" role="group" [attr.aria-label]="column().label">
          @for (option of options(); track option.value) {
            <nx-checkbox
              [label]="option.label"
              [checked]="selected().includes(option.value)"
              (checkedChange)="toggle(option.value, $event)"
            />
          } @empty {
            <p class="form-note">目前的資料沒有可選的值。</p>
          }
        </div>
      }
      @case ('number') {
        <div class="ui-filter-range">
          <label class="ui-field"
            >最小<input
              nxField
              type="number"
              [value]="range().from ?? ''"
              (change)="bound('from', $any($event.target).value)"
          /></label>
          <label class="ui-field"
            >最大<input
              nxField
              type="number"
              [value]="range().to ?? ''"
              (change)="bound('to', $any($event.target).value)"
          /></label>
        </div>
      }
      @default {
        <!-- 日期用共用日曆：民國／西元與鍵盤操作跟其他日期欄位一致；日曆 popover 疊在篩選 popover 之內。 -->
        <div class="ui-filter-range">
          <nx-date-time-picker
            label="起日"
            [withTime]="false"
            [required]="false"
            [max]="range().to ?? ''"
            [value]="range().from ?? ''"
            (valueChange)="bound('from', $event)"
          />
          <nx-date-time-picker
            label="迄日"
            [withTime]="false"
            [required]="false"
            [min]="range().from ?? ''"
            [value]="range().to ?? ''"
            (valueChange)="bound('to', $event)"
          />
        </div>
      }
    }
    @if (value()) {
      <button type="button" class="quiet-button" (click)="valueChange.emit(null)">
        <nx-icon name="reset" />清除篩選
      </button>
    }`,
})
export class TableFilter {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any -- 各頁的列型別不同
  readonly column = input.required<TableColumnDef<any>>();
  readonly value = input<ColumnFilter | undefined>();
  readonly valueChange = output<ColumnFilter | null>();
  protected readonly type = computed(() => {
    const filter = this.column().filter;
    return typeof filter === 'object' ? 'options' : (filter ?? 'text');
  });
  protected readonly options = computed(() => filterOptions(this.column()));
  protected readonly text = computed(() => {
    const value = this.value();
    return value?.kind === 'text' ? value.text : '';
  });
  protected readonly selected = computed(() => {
    const value = this.value();
    return value?.kind === 'options' ? value.values : [];
  });
  protected readonly range = computed(() => {
    const value = this.value();
    return value?.kind === 'range' ? value : { from: undefined, to: undefined };
  });
  private timer = 0;

  constructor() {
    inject(DestroyRef).onDestroy(() => clearTimeout(this.timer));
  }
  typed(text: string) {
    clearTimeout(this.timer);
    this.timer = window.setTimeout(() => this.emitText(text), TEXT_DELAY);
  }
  typedNow(text: string, event: Event) {
    event.preventDefault();
    clearTimeout(this.timer);
    this.emitText(text);
  }
  toggle(value: string, checked: boolean) {
    const values = this.selected().filter((item) => item !== value);
    this.valueChange.emit({ kind: 'options', values: checked ? [...values, value] : values });
  }
  bound(edge: 'from' | 'to', value: string) {
    const { from, to } = this.range();
    // 日曆在輸入到一半時回傳空字串；值沒變就不送，免得清掉另一端或重讀資料。
    if ((value || undefined) === (edge === 'from' ? from : to)) return;
    this.valueChange.emit({ kind: 'range', from, to, [edge]: value || undefined });
  }
  private emitText(text: string) {
    if (text.trim() !== this.text().trim()) this.valueChange.emit({ kind: 'text', text });
  }
}
