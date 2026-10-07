import {
  ChangeDetectionStrategy,
  Component,
  Directive,
  ElementRef,
  effect,
  inject,
  input,
  output,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { Icon } from './icon';
import { Select } from './select';
import { positionPopover } from '../browser/popover-position';

export interface TableColumn {
  id: string;
  label: string;
  hideable?: boolean;
  defaultHidden?: boolean;
}
export type TableSortDirection = 'asc' | 'desc';

/** Semantic table shell with view preferences. Data fetching and query state remain feature responsibilities. */
@Component({
  selector: 'nx-data-table',
  imports: [Icon],
  styleUrl: './data-table.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'ui-data-table', '[attr.aria-busy]': 'busy()' },
  template: `<div class="ui-table-toolbar-wrap">
      <ng-content select="[table-toolbar]" />
      @if (columns().length) {
        <button
          #columnTrigger
          type="button"
          class="quiet-button ui-table-columns-trigger"
          aria-label="顯示欄位"
          aria-haspopup="dialog"
          [attr.aria-expanded]="columnsOpen()"
          [attr.aria-controls]="id"
          (click)="toggleColumns()"
        >
          <nx-icon name="columns" />欄位
        </button>
      }
    </div>
    <div
      #columnPanel
      [id]="id"
      popover="auto"
      role="dialog"
      aria-label="顯示欄位"
      class="ui-table-columns-panel"
      (toggle)="columnsOpen.set($any($event).newState === 'open')"
      (keydown.escape)="closeColumns($event)"
    >
      <h3>顯示欄位</h3>
      @for (column of columns(); track column.id) {
        <label
          ><input
            type="checkbox"
            [checked]="isVisible(column.id)"
            [disabled]="column.hideable === false"
            (change)="toggleColumn(column.id)"
          />
          {{ column.label }}</label
        >
      }
      <button type="button" class="quiet-button" (click)="resetColumns()">還原預設欄位</button>
    </div>
    <div class="ui-table-scroll" role="region" tabindex="0" [attr.aria-label]="label()">
      <ng-content select="table" />
      <ng-content select="[table-state]" />
    </div>
    <ng-content select="[table-footer]" />`,
})
export class DataTable {
  readonly label = input.required<string>();
  readonly busy = input(false);
  readonly columns = input<TableColumn[]>([]);
  readonly preferenceKey = input('');
  readonly columnsOpen = signal(false);
  readonly hidden = signal<string[]>([]);
  readonly id = 'nx-table-columns-' + ++tableSequence;
  private readonly trigger = viewChild<ElementRef<HTMLButtonElement>>('columnTrigger');
  private readonly panel = viewChild.required<ElementRef<HTMLElement>>('columnPanel');
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  constructor() {
    effect(() => {
      const columns = this.columns(),
        key = this.preferenceKey();
      let hidden = columns.filter((column) => column.defaultHidden).map((column) => column.id);
      if (key) {
        try {
          const stored: unknown = JSON.parse(
            localStorage.getItem('ai-nexus.table.' + key) ?? 'null',
          );
          if (stored && typeof stored === 'object' && !Array.isArray(stored)) {
            const preferences = stored as Record<string, unknown>;
            hidden = columns
              .filter(
                (column) =>
                  column.hideable !== false &&
                  (typeof preferences[column.id] === 'boolean'
                    ? preferences[column.id] === false
                    : column.defaultHidden),
              )
              .map((column) => column.id);
          }
        } catch {
          /* A missing or unavailable preference never prevents rendering. */
        }
      }
      untracked(() => this.hidden.set(hidden));
    });
  }
  isVisible(id: string) {
    return !this.hidden().includes(id);
  }
  toggleColumns() {
    const panel = this.panel().nativeElement,
      trigger = this.trigger()?.nativeElement;
    if (this.columnsOpen()) panel.hidePopover();
    else if (trigger) {
      panel.showPopover();
      positionPopover(trigger, panel, 220, 'right');
    }
  }
  closeColumns(event: Event) {
    event.preventDefault();
    event.stopPropagation();
    this.panel().nativeElement.hidePopover();
    this.trigger()?.nativeElement.focus({ preventScroll: true });
  }
  toggleColumn(id: string) {
    if (!this.columns().some((column) => column.id === id && column.hideable !== false)) return;
    this.hidden.update((hidden) =>
      hidden.includes(id) ? hidden.filter((key) => key !== id) : [...hidden, id],
    );
    this.saveColumns();
  }
  resetColumns() {
    this.hidden.set(
      this.columns()
        .filter((column) => column.defaultHidden)
        .map((column) => column.id),
    );
    this.saveColumns();
  }
  private saveColumns() {
    if (!this.preferenceKey()) return;
    try {
      const preferences = Object.fromEntries(
        this.columns().map((column) => [column.id, this.isVisible(column.id)]),
      );
      localStorage.setItem('ai-nexus.table.' + this.preferenceKey(), JSON.stringify(preferences));
    } catch {
      /* Optional preference. */
    }
  }
  resetScroll() {
    this.host.nativeElement.querySelector('.ui-table-scroll')?.scrollTo({ top: 0, left: 0 });
  }
}

let tableSequence = 0;

/** Apply the same column ID to col/th/td; visibility has one owner. */
@Directive({ selector: '[nxTableColumn]', host: { '[hidden]': '!table.isVisible(id())' } })
export class DataTableColumn {
  readonly id = input.required<string>({ alias: 'nxTableColumn' });
  readonly table = inject(DataTable);
}

@Component({
  selector: 'th[nxSortHeader]',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { scope: 'col', '[attr.aria-sort]': 'direction() === "asc" ? "ascending" : "descending"' },
  template: `<button
    type="button"
    class="ui-table-sort"
    [disabled]="disabled()"
    [attr.aria-label]="
      label() +
      '：' +
      (direction() === 'asc'
        ? ascendingLabel() + '，切換為' + descendingLabel()
        : descendingLabel() + '，切換為' + ascendingLabel())
    "
    (click)="directionChange.emit(direction() === 'asc' ? 'desc' : 'asc')"
  >
    {{ label() }}<nx-icon [name]="direction() === 'asc' ? 'arrow' : 'down'" />
  </button>`,
})
export class TableSortHeader {
  readonly label = input.required<string>({ alias: 'nxSortHeader' });
  readonly direction = input<TableSortDirection>('desc');
  readonly ascendingLabel = input('由小到大');
  readonly descendingLabel = input('由大到小');
  readonly disabled = input(false);
  readonly directionChange = output<TableSortDirection>();
}

/** Row-sized pointer target; nested controls and text selection retain their own behavior. */
@Directive({
  selector: 'tr[nxTableRow]',
  host: {
    '[class.ui-table-row]': 'enabled()',
    '(click)': 'activate($event)',
    '(keydown)': 'navigate($event)',
  },
})
export class DataTableRow {
  readonly enabled = input(true, { alias: 'rowEnabled' });
  readonly rowActivate = output<void>();
  private readonly host = inject<ElementRef<HTMLTableRowElement>>(ElementRef);
  activate(event: MouseEvent) {
    if (
      !this.enabled() ||
      event.defaultPrevented ||
      !(event.target instanceof Element) ||
      event.target.closest('button,a,input,select,textarea,[contenteditable="true"]') ||
      window.getSelection()?.toString()
    )
      return;
    this.host.nativeElement
      .querySelector<HTMLButtonElement>('[data-row-action]')
      ?.focus({ preventScroll: true });
    this.rowActivate.emit();
  }
  navigate(event: KeyboardEvent) {
    if (
      !this.enabled() ||
      !(event.target instanceof Element) ||
      !event.target.matches('[data-row-action]') ||
      !['ArrowDown', 'ArrowUp', 'Home', 'End'].includes(event.key)
    )
      return;
    const actions = [
      ...this.host.nativeElement.parentElement!.querySelectorAll<HTMLButtonElement>(
        '[data-row-action]:not(:disabled)',
      ),
    ];
    const current = actions.indexOf(event.target as HTMLButtonElement);
    const index =
      event.key === 'Home'
        ? 0
        : event.key === 'End'
          ? actions.length - 1
          : Math.max(
              0,
              Math.min(actions.length - 1, current + (event.key === 'ArrowDown' ? 1 : -1)),
            );
    event.preventDefault();
    actions[index]?.focus();
  }
}

@Component({
  selector: 'nx-table-pagination',
  imports: [Icon, Select],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'ui-table-pagination' },
  template: `<nx-select
      label="每頁筆數"
      [options]="sizes"
      [value]="pageSize().toString()"
      [disabled]="busy()"
      (valueChange)="pageSizeChange.emit(+$event)"
    />
    <span role="status">第 {{ pageIndex() + 1 }} 頁 · 本頁 {{ count() }} 筆</span>
    <button
      type="button"
      class="icon-button"
      aria-label="上一頁"
      title="上一頁"
      [disabled]="busy() || pageIndex() === 0"
      (click)="pageChange.emit(-1)"
    >
      <nx-icon name="left" />
    </button>
    <button
      type="button"
      class="icon-button"
      aria-label="下一頁"
      title="下一頁"
      [disabled]="busy() || !hasNext()"
      (click)="pageChange.emit(1)"
    >
      <nx-icon name="chevron" />
    </button>`,
})
export class TablePagination {
  readonly pageIndex = input(0);
  readonly pageSize = input(50);
  readonly count = input(0);
  readonly hasNext = input(false);
  readonly busy = input(false);
  readonly pageChange = output<-1 | 1>();
  readonly pageSizeChange = output<number>();
  readonly sizes = [25, 50, 100].map((size) => ({
    value: String(size),
    label: '每頁 ' + size + ' 筆',
  }));
}
