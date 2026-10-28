import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  inject,
  input,
  viewChild,
} from '@angular/core';
import { anchoredPopover } from '../browser/anchored-popover';
import { positionPopover } from '../browser/popover-position';
import { DataTable } from './data-table';
import { Icon } from './icon';
import { TableFilter } from './table-filter';
import type { ColumnFilter } from './table-model';

/**
 * 表頭儲存格（`<th nxHeader="欄位ID">`）：標題、排序鈕與篩選 popover 都依 `[table]` 的欄位定義產生，
 * 欄位顯示偏好、數字欄靠右也一併套用。沒有排序或篩選的欄位只顯示標題。
 * 讀取中只以 aria-disabled 擋：剛按下的排序鈕若變成 disabled，焦點會掉到頁首。
 */
@Component({
  selector: 'th[nxHeader]',
  imports: [Icon, TableFilter],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    scope: 'col',
    '[attr.aria-sort]': 'ariaSort()',
    '[hidden]': '!table.isVisible(id())',
    '[class.ui-table-numeric]': 'column()?.numeric',
  },
  styles: `
    .ui-table-heading {
      display: flex;
      align-items: center;
      gap: var(--p-space-1);
      min-width: 0;
    }
    :host(.ui-table-numeric) .ui-table-heading {
      justify-content: flex-end;
    }
    .ui-table-sort-idle {
      opacity: 0;
    }
    .ui-table-sort:is(:hover, :focus-visible) .ui-table-sort-idle {
      opacity: 0.5;
    }
    .ui-table-filter-trigger {
      display: inline-grid;
      flex: none;
      place-items: center;
      width: 1.5rem;
      height: 1.5rem;
      border-radius: var(--radius-action);
      color: var(--muted);
      --icon-size: var(--icon-xs);
    }
    .ui-table-filter-trigger:hover {
      background: var(--hover);
      color: var(--ink);
    }
    .ui-table-filter-trigger.is-active {
      color: var(--accent);
    }
    .ui-table-filter-panel {
      padding: var(--p-space-3);
      border-radius: var(--radius-surface);
      font-weight: 400;
      text-align: start;
    }
    .ui-table-filter-panel h3 {
      margin: 0 0 var(--p-space-2);
      font-size: var(--p-font-sm);
    }
  `,
  template: `<span class="ui-table-heading">
    @if (sortable()) {
      <button
        type="button"
        class="ui-table-sort"
        [attr.aria-disabled]="busy() || null"
        [attr.aria-label]="sortLabel()"
        (click)="model()?.toggleSort(id())"
      >
        {{ heading() }}
        @switch (direction()) {
          @case ('asc') {
            <nx-icon name="arrow" />
          }
          @case ('desc') {
            <nx-icon name="down" />
          }
          @default {
            <nx-icon name="selector" class="ui-table-sort-idle" />
          }
        }
      </button>
    } @else {
      <span>{{ heading() }}</span>
    }
    @if (filterable()) {
      <button
        #trigger
        type="button"
        class="ui-table-filter-trigger"
        aria-haspopup="dialog"
        [class.is-active]="!!current()"
        [attr.aria-expanded]="popover.opened()"
        [attr.aria-label]="'篩選' + label() + (current() ? '（已套用）' : '')"
        [title]="current() ? '已篩選' : '篩選'"
        (click)="toggle()"
      >
        <nx-icon name="filter" />
      </button>
      <div
        #panel
        popover="auto"
        role="dialog"
        class="popover-panel ui-table-filter-panel ui-density-compact"
        [attr.aria-label]="'篩選：' + label()"
        (toggle)="popover.sync($event)"
        (keydown.escape)="close($event)"
      >
        <h3>篩選：{{ label() }}</h3>
        @if (popover.opened()) {
          <nx-table-filter [column]="column()!" [value]="current()" (valueChange)="apply($event)" />
        }
      </div>
    }
  </span>`,
})
export class TableHeader {
  readonly id = input.required<string>({ alias: 'nxHeader' });
  protected readonly table = inject(DataTable);
  protected readonly model = computed(() => this.table.table());
  protected readonly column = computed(() => this.model()?.column(this.id()));
  protected readonly label = computed(() => this.column()?.label ?? this.id());
  protected readonly heading = computed(() => this.column()?.header ?? this.label());
  protected readonly busy = computed(() => !!this.model()?.refreshing());
  protected readonly sortable = computed(() => !!this.column()?.sort);
  protected readonly direction = computed(() => {
    const sort = this.model()?.sort();
    return sort?.column === this.id() ? sort.direction : null;
  });
  protected readonly ariaSort = computed(() => {
    const direction = this.direction();
    return direction ? (direction === 'asc' ? 'ascending' : 'descending') : null;
  });
  protected readonly sortLabel = computed(() => {
    const direction = this.direction();
    const column = this.column();
    const [up, down] =
      column?.time || column?.filter === 'date' || column?.filter === 'day'
        ? ['由舊到新', '由新到舊']
        : column?.numeric
          ? ['由小到大', '由大到小']
          : ['遞增', '遞減'];
    return direction
      ? `${this.label()}：${direction === 'asc' ? up : down}，切換為${direction === 'asc' ? down : up}`
      : `依${this.label()}排序`;
  });
  /** 選項來自資料（函式）時，資料只在後端逐頁就不給篩選：選項不完整。 */
  protected readonly filterable = computed(() => {
    const filter = this.column()?.filter;
    if (!filter) return false;
    return (
      !(typeof filter === 'object' && typeof filter.options === 'function') ||
      !!this.model()?.local()
    );
  });
  protected readonly current = computed(() => this.model()?.filters()[this.id()]);
  private readonly trigger = viewChild<ElementRef<HTMLButtonElement>>('trigger');
  private readonly panel = viewChild<ElementRef<HTMLElement>>('panel');
  protected readonly popover = anchoredPopover({
    panel: () => this.panel()?.nativeElement,
    trigger: () => this.trigger()?.nativeElement,
    place: (panel) => {
      const trigger = this.trigger()?.nativeElement;
      // 日期範圍是兩個並排的日曆欄位（各有日曆鈕），240 會把日期截掉。
      const filter = this.column()?.filter;
      const width = filter === 'date' || filter === 'day' ? 320 : 240;
      if (trigger)
        positionPopover(trigger, panel, width, this.column()?.numeric ? 'right' : 'left');
    },
  });

  toggle() {
    if (this.popover.opened()) this.popover.hide();
    else {
      this.popover.show();
      requestAnimationFrame(() =>
        this.panel()?.nativeElement.querySelector<HTMLElement>('input, button')?.focus(),
      );
    }
  }
  close(event: Event) {
    event.preventDefault();
    event.stopPropagation();
    this.popover.hide();
    this.trigger()?.nativeElement.focus({ preventScroll: true });
  }
  apply(filter: ColumnFilter | null) {
    this.model()?.setFilter(this.id(), filter);
    if (!filter) this.popover.hide();
  }
}
