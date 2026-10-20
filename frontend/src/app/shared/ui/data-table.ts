import { rovingIndex } from '../browser/roving-index';
import {
  ChangeDetectionStrategy,
  Component,
  Directive,
  ElementRef,
  computed,
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
import { anchoredPopover } from '../browser/anchored-popover';
import { positionPopover } from '../browser/popover-position';

export interface TableColumn {
  id: string;
  label: string;
  hideable?: boolean;
  defaultHidden?: boolean;
}
export type TableSortDirection = 'asc' | 'desc';

/**
 * 語意化的表格外殼，附帶檢視偏好。資料讀取與查詢狀態仍屬於功能頁。
 * `stacked` 讓列在 `narrow` 斷點以下變成卡片：儲存格排成單欄，`data-stack="end"`
 * 固定在右上角（列的選單或編輯鈕），
 * `data-stack="hide"` 的內容交給詳情檢視；`data-label` 標出需要表頭名稱的值。
 * 表格本身的最小寬度放在 `--table-min-width`，卡片模式才能清掉它。
 *
 * 工具列放標題（`[table-toolbar]`），表格自己的動作（如匯出，`[table-actions]`）接在欄位選擇器旁邊；
 * `nx-table-state` 負責載入中、無資料與錯誤三種狀態。
 *
 * 可見外框的高度跟著列數走，不會超過宿主；頁面若把剩餘高度交給宿主（`layoutMode="data"`），
 * 表格改由內部捲動、表頭固定，列數少時下方也不會留白。列的狀態與共用樣式在
 * `styles/data-workspace.scss`。
 */
@Component({
  selector: 'nx-data-table',
  imports: [Icon],
  styleUrl: './data-table.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'ui-data-table',
    '[class.ui-table-stack]': 'stacked()',
    '[attr.aria-busy]': 'busy()',
  },
  template: `<div class="ui-table-frame">
    <div class="ui-table-toolbar-wrap">
      <ng-content select="[table-toolbar]" />
      <div class="ui-table-actions"><ng-content select="[table-actions]" /></div>
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
      class="popover-panel ui-table-columns-panel"
      (toggle)="columnsPopover.sync($event)"
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
      <button type="button" class="quiet-button" (click)="resetColumns()">
        <nx-icon name="reset" />還原預設欄位
      </button>
    </div>
    <div class="ui-table-scroll" role="region" tabindex="0" [attr.aria-label]="label()">
      <ng-content select="table" />
      <ng-content select="nx-table-state" />
    </div>
    <ng-content select="[table-footer]" />
  </div>`,
})
export class DataTable {
  readonly label = input.required<string>();
  readonly busy = input(false);
  readonly columns = input<TableColumn[]>([]);
  readonly preferenceKey = input('');
  /** 窄螢幕時列會變成卡片，細節見類別註解。 */
  readonly stacked = input(false);
  readonly hidden = signal<string[]>([]);
  readonly id = 'nx-table-columns-' + ++tableSequence;
  private readonly trigger = viewChild<ElementRef<HTMLButtonElement>>('columnTrigger');
  private readonly panel = viewChild.required<ElementRef<HTMLElement>>('columnPanel');
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  protected readonly columnsPopover = anchoredPopover({
    panel: () => this.panel().nativeElement,
    place: (panel) => {
      const trigger = this.trigger()?.nativeElement;
      if (trigger) positionPopover(trigger, panel, 220, 'right');
    },
  });
  readonly columnsOpen = this.columnsPopover.opened;
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
          /* 偏好缺漏或讀取失敗時，仍要照常渲染。 */
        }
      }
      untracked(() => this.hidden.set(hidden));
    });
  }
  isVisible(id: string) {
    return !this.hidden().includes(id);
  }
  toggleColumns() {
    if (this.columnsOpen()) this.columnsPopover.hide();
    else if (this.trigger()) this.columnsPopover.show();
  }
  closeColumns(event: Event) {
    event.preventDefault();
    event.stopPropagation();
    this.columnsPopover.hide();
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
      /* 選用偏好。 */
    }
  }
  resetScroll() {
    this.host.nativeElement.querySelector('.ui-table-scroll')?.scrollTo({ top: 0, left: 0 });
  }
}

let tableSequence = 0;

function isRowAction(target: EventTarget | null): target is HTMLElement {
  return target instanceof HTMLElement && target.matches('[data-row-action]');
}

/** 同一欄位 ID 套用到 col、th、td；可見與否只由一處決定。 */
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

/**
 * 列大小的指標目標；巢狀控制項與文字選取維持原本行為。點擊時焦點移到列內的 `[data-row-action]`，
 * 但不顯示外框，
 * 讓對話框或抽屜關閉後能把焦點還給該列；鍵盤聚焦到該動作時才框住整列（見 data-workspace.scss）。
 * 瀏覽器會在 Escape 後為還原的焦點畫外框，即使列是被點擊的，所以列上標 `is-focus-returned`，
 * 直到下一次按鍵或焦點移動才清掉。
 * 方向鍵、Home、End 在表格所有列的動作之間移動。
 */
@Directive({
  selector: 'tr[nxTableRow]',
  host: {
    '[class.ui-table-row]': 'enabled()',
    '(click)': 'activate($event)',
    '(keydown)': 'navigate($event)',
    '(focusout)': 'focusLeft($event)',
    '(focusin)': 'focusReturned($event)',
  },
})
export class DataTableRow {
  readonly enabled = input(true, { alias: 'rowEnabled' });
  readonly rowActivate = output<void>();
  private readonly host = inject<ElementRef<HTMLTableRowElement>>(ElementRef);
  /** 列動作是否顯示外框；焦點回到它時讀取。 */
  private ringed = true;
  focusReturned(event: FocusEvent) {
    if (!isRowAction(event.target)) return;
    const from = event.relatedTarget;
    // 在繪製前直接設定（不經過變更偵測），外框才不會閃一下。
    if (
      !this.ringed &&
      !(from instanceof Element && from.isConnected && !from.closest('dialog:not([open])'))
    )
      this.quiet(true);
    this.ringed = event.target.matches(':focus-visible') && !this.quieted();
  }
  focusLeft(event: FocusEvent) {
    if (isRowAction(event.target)) this.quiet(false);
  }
  private quieted() {
    return this.host.nativeElement.classList.contains('is-focus-returned');
  }
  private quiet(on: boolean) {
    this.host.nativeElement.classList.toggle('is-focus-returned', on);
  }
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
      ?.focus({ preventScroll: true, focusVisible: false });
    this.rowActivate.emit();
  }
  navigate(event: KeyboardEvent) {
    // 修飾鍵以外的按鍵都會重新顯示外框，與瀏覽器對點擊控制項的做法一致。
    if (isRowAction(event.target) && !['Shift', 'Control', 'Alt', 'Meta'].includes(event.key)) {
      this.quiet(false);
      this.ringed = true;
    }
    if (
      !this.enabled() ||
      !(event.target instanceof Element) ||
      !event.target.matches('[data-row-action]') ||
      !['ArrowDown', 'ArrowUp', 'Home', 'End'].includes(event.key)
    )
      return;
    const actions = [
      ...(
        this.host.nativeElement.closest('table') ?? this.host.nativeElement.parentElement!
      ).querySelectorAll<HTMLButtonElement>('[data-row-action]:not(:disabled)'),
    ];
    const current = actions.indexOf(event.target as HTMLButtonElement);
    event.preventDefault();
    actions[rovingIndex(event.key, current, actions.length, { wrap: false })!]?.focus();
  }
}

/**
 * 表頭下方的載入中、無資料與讀取失敗狀態。失敗時顯示訊息與重試；仍有列顯示時整個隱藏（錯誤由
 * 頁面自己顯示）。
 */
@Component({
  selector: 'nx-table-state',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    // 讀取失敗要立即讀出，與 nx-notice 的 danger 相同；載入中與空狀態只是狀態更新。
    '[attr.role]': 'error() ? "alert" : "status"',
    '[hidden]': '!visible()',
  },
  styles: `
    :host {
      display: grid;
      justify-items: center;
      gap: var(--p-space-2);
      padding: var(--p-space-8) var(--p-space-4);
      color: var(--secondary);
      font-size: var(--p-font-sm);
      text-align: center;
    }
    :host([hidden]) {
      display: none;
    }
    p {
      margin: 0;
    }
    :host > nx-icon {
      color: var(--muted);
    }
  `,
  template: `@if (error()) {
      <nx-icon name="info" />
      <strong>{{ errorTitle() }}</strong>
      <p>{{ error() }}</p>
      <button type="button" class="secondary-button" [disabled]="loading()" (click)="retry.emit()">
        <nx-icon [name]="loading() ? 'loading' : 'refresh'" />重試
      </button>
    } @else if (loading()) {
      <nx-icon name="loading" />
      <p>{{ loadingText() }}</p>
    } @else {
      <nx-icon name="search" />
      <strong>{{ emptyTitle() }}</strong>
      @if (emptyText()) {
        <p>{{ emptyText() }}</p>
      }
      <ng-content />
    }`,
})
export class TableState {
  /** 顯示中的列數；有列就隱藏。 */
  readonly count = input(0);
  readonly loading = input(false);
  readonly error = input<string | null | undefined>('');
  readonly loadingText = input('正在載入…');
  readonly errorTitle = input('無法載入資料');
  readonly emptyTitle = input('沒有符合條件的資料');
  readonly emptyText = input('');
  readonly retry = output<void>();
  readonly visible = computed(() => this.count() === 0);
}

/** `TablePagination` 預設可選的每頁筆數；頁面收到 `pageSizeChange` 時以它擋下其他值。 */
export const PAGE_SIZES: readonly number[] = [25, 50, 100];

/**
 * `cursor` 分頁看過的各頁游標：游標只能往後讀，上一頁要用記下的游標重讀。
 * `first` 是第一頁的游標（沒有游標時的值）。
 */
export class CursorTrail<T> {
  private cursors: T[];
  constructor(private readonly first: T) {
    this.cursors = [first];
  }
  /** 讀第 `index` 頁前記下它的游標；回到第一頁（新查詢、排序、每頁筆數）時清掉舊紀錄。 */
  visit(index: number, cursor: T) {
    if (index === 0) this.cursors = [this.first];
    this.cursors[index] = cursor;
  }
  /** 看過的第 `index` 頁的游標。 */
  at(index: number): T | undefined {
    return this.cursors[index];
  }
}

/**
 * 表格頁尾的分頁列：左側是範圍與每頁筆數，右側是一組有外框的翻頁按鈕，頁碼在按鈕中間。
 * `cursor`（游標分頁、不算總數）：上一頁、下一頁與回到第一頁，大表不必計數也不會跳過資料。
 * `offset`（已知總數）：顯示範圍，可輸入頁碼跳頁。`pageChange` 前後移一頁；`pageSelect`
 * 到指定頁（第一頁、輸入的頁碼）。
 * 讀取中（`busy`）不送出事件，但按鈕與頁碼欄只用 `aria-disabled`／`readonly`：
 * 真的停用會讓剛操作的控制項失去焦點。
 */
@Component({
  selector: 'nx-table-pagination',
  imports: [Icon, Select],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'ui-table-pagination' },
  template: `<div class="ui-pagination-info">
      @if (mode() === 'offset') {
        <span role="status">
          @if (total()) {
            <b>{{ span() }}</b> / {{ totalText() }} 筆
          } @else {
            0 筆
          }
        </span>
      } @else {
        <span role="status"
          >本頁 <b>{{ count() }}</b> 筆</span
        >
      }
      <span class="ui-pagination-size">
        <span aria-hidden="true">·</span>
        @if (pageSizes().length > 1) {
          <nx-select
            appearance="inline"
            label="每頁筆數"
            [options]="sizes()"
            [value]="pageSize().toString()"
            (valueChange)="changeSize(+$event)"
          />
        } @else {
          <span>每頁 {{ pageSize() }} 筆</span>
        }
      </span>
    </div>
    <div class="ui-pagination-group" role="group" aria-label="分頁">
      @if (mode() === 'cursor') {
        <button
          type="button"
          class="icon-button"
          aria-label="回到第一頁"
          title="回到第一頁"
          [attr.aria-disabled]="busy() || pageIndex() === 0 || null"
          (click)="select(0)"
        >
          <nx-icon name="first" />
        </button>
      }
      <button
        type="button"
        class="icon-button"
        aria-label="上一頁"
        title="上一頁"
        [attr.aria-disabled]="busy() || pageIndex() === 0 || null"
        (click)="step(-1)"
      >
        <nx-icon name="left" />
      </button>
      @if (mode() === 'offset') {
        <label class="ui-pagination-page"
          ><input
            type="number"
            inputmode="numeric"
            min="1"
            [max]="pageCount()"
            aria-label="跳到頁碼"
            title="輸入頁碼後按 Enter"
            [value]="pageIndex() + 1"
            [readOnly]="busy()"
            [disabled]="pageCount() <= 1"
            (keydown.enter)="jump($event)"
            (change)="jump($event)"
          />/ {{ pageCount() }}</label
        >
      } @else {
        <span class="ui-pagination-page"
          >第 <b>{{ pageIndex() + 1 }}</b> 頁</span
        >
      }
      <button
        type="button"
        class="icon-button"
        aria-label="下一頁"
        title="下一頁"
        [attr.aria-disabled]="busy() || !next() || null"
        (click)="step(1)"
      >
        <nx-icon name="chevron" />
      </button>
    </div>`,
})
export class TablePagination {
  readonly mode = input<'cursor' | 'offset'>('cursor');
  readonly pageIndex = input(0);
  readonly pageSize = input(50);
  readonly pageSizes = input<readonly number[]>(PAGE_SIZES);
  /** 目前這一頁的列數。 */
  readonly count = input(0);
  /** `cursor`：這次讀取是否回傳下一頁游標。 */
  readonly hasNext = input(false);
  /** `offset`：符合條件的總列數。 */
  readonly total = input(0);
  readonly busy = input(false);
  readonly pageChange = output<-1 | 1>();
  readonly pageSelect = output<number>();
  readonly pageSizeChange = output<number>();
  readonly sizes = computed(() =>
    this.pageSizes().map((size) => ({
      value: String(size),
      label: '每頁 ' + size + ' 筆',
    })),
  );
  readonly pageCount = computed(() => Math.max(1, Math.ceil(this.total() / this.pageSize())));
  readonly next = computed(() =>
    this.mode() === 'offset' ? this.pageIndex() < this.pageCount() - 1 : this.hasNext(),
  );
  /** 這一頁的起訖列號，例如 `101–150`。 */
  readonly span = computed(() => {
    const first = this.pageIndex() * this.pageSize();
    return `${first + 1}–${first + this.count()}`;
  });
  readonly totalText = computed(() => this.total().toLocaleString('zh-TW'));
  /** 已送出、尚未讀完的頁；Enter 之後失焦還會再觸發一次 change，靠它避免重複讀同一頁。 */
  private requested = -1;
  constructor() {
    // 讀完（或沒有 busy 的呼叫端換了頁）才清掉；讀取失敗時可以再送同一頁。
    effect(() => {
      this.pageIndex();
      if (!this.busy()) this.requested = -1;
    });
  }
  step(direction: -1 | 1) {
    if (this.busy() || (direction === -1 ? this.pageIndex() === 0 : !this.next())) return;
    this.pageChange.emit(direction);
  }
  select(index: number) {
    if (this.busy() || index === this.pageIndex() || index === this.requested) return;
    this.requested = index;
    this.pageSelect.emit(index);
  }
  changeSize(size: number) {
    if (!this.busy() && size !== this.pageSize()) this.pageSizeChange.emit(size);
  }
  jump(event: Event) {
    const input = event.target as HTMLInputElement;
    const page = Math.round(Number(input.value));
    if (!Number.isFinite(page) || page < 1 || page > this.pageCount()) {
      input.value = String(this.pageIndex() + 1);
      return;
    }
    this.select(page - 1);
  }
}
