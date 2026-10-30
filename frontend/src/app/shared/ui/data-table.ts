import { rovingIndex } from '../browser/roving-index';
import { Checkbox } from './checkbox';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  Directive,
  ElementRef,
  Injector,
  afterNextRender,
  computed,
  forwardRef,
  contentChild,
  effect,
  inject,
  input,
  output,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { ThemeService } from '@core/preferences/theme-service';
import { ChipList } from './chip-list';
import { Icon } from './icon';
import { PendingButton } from './pending-button';
import { Select } from './select';
import { PAGE_SIZES, type TableColumn, type TableModel } from './table-model';
import { anchoredPopover } from '../browser/anchored-popover';
import { positionPopover } from '../browser/popover-position';
import { motionTiming } from '../browser/motion-tokens';

export {
  PAGE_SIZES,
  type TableColumn,
  type TableColumnDef,
  type TableModel,
  type TableSortDirection,
} from './table-model';

/**
 * 語意化的表格外殼，附帶檢視偏好。資料讀取與查詢狀態仍屬於功能頁。
 * `stacked` 讓列在 `narrow` 斷點以下變成卡片：儲存格排成單欄，`data-stack="end"`
 * 固定在右上角（列的選單或編輯鈕），
 * `data-stack="hide"` 的內容交給詳情檢視；`data-label` 標出需要表頭名稱的值。
 * 表格本身的最小寬度放在 `--table-min-width`，卡片模式才能清掉它。
 *
 * 工具列放標題（`[table-toolbar]`），表格自己的動作（如匯出，`[table-actions]`）接在欄位選擇器旁邊；
 * `nx-table-state` 負責載入中、無資料與錯誤三種狀態，已有列時的重讀由表格自己播報。
 * 內容超出捲動區時多一顆「放大表格」：外框改為貼齊視窗（`is-maximized`），從原位置過渡過去，
 * Esc 或焦點移回頁面時還原。
 *
 * 外框填滿宿主：頁面把剩餘高度交給宿主（`layoutMode="data"`）時，外框一律延伸到頁底、分頁列貼底，
 * 列多時內部捲動、表頭固定；一般流動版面裡宿主跟著內容走，外框就等於列高。列的狀態與共用樣式在
 * `styles/data-workspace.scss`。
 */
@Component({
  selector: 'nx-data-table',
  imports: [
    Checkbox,
    ChipList,
    Icon,
    forwardRef(() => TablePagination),
    forwardRef(() => TableState),
  ],
  styleUrl: './data-table.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'ui-data-table',
    '[class.ui-table-stack]': 'stacked()',
    '[attr.aria-busy]': 'working() || null',
    '(keydown.escape)': 'escapeMaximized($event)',
    '(focusout)': 'focusLeft($event)',
  },
  template: `<div #frame class="ui-table-frame">
    <div class="ui-table-toolbar-wrap">
      <ng-content select="[table-toolbar]" />
      <div class="ui-table-actions">
        <ng-content select="[table-actions]" />
        @if (pickerColumns().length) {
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
        @if (overflowing() || maximized()) {
          <button
            type="button"
            class="icon-button ui-table-maximize"
            [attr.aria-label]="maximized() ? '還原表格' : '放大表格'"
            [title]="maximized() ? '還原表格' : '放大表格'"
            (click)="setMaximized(!maximized())"
          >
            <nx-icon [name]="maximized() ? 'minimize' : 'maximize'" />
          </button>
        }
      </div>
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
      @for (column of pickerColumns(); track column.id) {
        <nx-checkbox
          [label]="column.label"
          [checked]="isVisible(column.id)"
          [disabled]="column.hideable === false"
          (checkedChange)="toggleColumn(column.id)"
        />
      }
      <button type="button" class="quiet-button" (click)="resetColumns()">
        <nx-icon name="reset" />還原預設欄位
      </button>
    </div>
    @if (table(); as model) {
      <!-- 以 hidden 收起而非移除：最後一個 chip 移除後清單仍要在，才能把焦點交出去。 -->
      @if (!model.chipsClaimed()) {
        <nx-chip-list
          class="ui-table-chips"
          label="欄位篩選"
          [hidden]="!model.chips().length"
          [chips]="model.chips()"
          [busy]="working()"
          (chipRemove)="$event.remove()"
          (allRemoved)="focusScroll()"
        />
      }
    }
    <span class="refresh-line" [class.is-active]="refreshing()" aria-hidden="true"></span>
    <!-- 首次載入由 nx-table-state 播報；這裡只播報已有列時的重讀（背景輪詢不算重讀）。 -->
    @if (refreshing()) {
      <span class="sr-only" role="status">正在重新載入資料…</span>
    }
    <div
      class="ui-table-scroll"
      role="region"
      tabindex="0"
      [attr.aria-label]="label()"
      [class.is-refreshing]="refreshing()"
    >
      <ng-content select="table" />
      <ng-content select="nx-table-state" />
      @if (table() && !state()) {
        <nx-table-state />
      }
    </div>
    @if (table(); as model) {
      @if (!pagination() && paged()) {
        <div class="ui-table-footer">
          <nx-table-pagination [table]="model" [busy]="busy()" />
        </div>
      }
    }
    <ng-content select="[table-footer]" />
  </div>`,
})
export class DataTable {
  readonly label = input.required<string>();
  /** 讀取中（首次載入或重新讀取）；綁 `apiResource` 的 `refreshing()`，有防抖的搜尋再加上打字中。 */
  readonly busy = input(false);
  /**
   * 表格的資料、排序、篩選與分頁（`localTable`／`endpointTable`／`cursorTable`）。給了之後表頭用 `th[nxHeader]`，
   * 載入／空狀態、欄位篩選 chip 與分頁列自動顯示，頁面不必再投影 `nx-table-state` 與分頁列（要自訂文字時仍可投影）。
   */
  // eslint-disable-next-line @typescript-eslint/no-explicit-any -- 各頁的列型別不同
  readonly table = input<TableModel<any> | null>(null);
  /** 沒有 `table` 時的欄位顯示偏好；有 `table` 時取它的欄位（需要 `preferenceKey`）。 */
  readonly columns = input<TableColumn[]>([]);
  readonly preferenceKey = input('');
  /** 窄螢幕時列會變成卡片，細節見類別註解。 */
  readonly stacked = input(false);
  readonly hidden = signal<string[]>([]);
  readonly id = 'nx-table-columns-' + ++tableSequence;
  protected readonly state = contentChild(TableState);
  private readonly builtinState = viewChild(TableState);
  protected readonly pagination = contentChild(TablePagination, { descendants: true });
  /** 欄位選擇器列出的欄位。 */
  readonly pickerColumns = computed<readonly TableColumn[]>(() => {
    const model = this.table();
    return this.columns().length || !model || !this.preferenceKey()
      ? this.columns()
      : model.columns;
  });
  /** 讀取中：頁面給的 `busy` 或 `table` 的重新讀取。 */
  readonly working = computed(() => this.busy() || !!this.table()?.refreshing());
  /**
   * 淡化與細線只給已有列的表格：沒有列時 `nx-table-state` 正顯示骨架、空狀態或錯誤，
   * 骨架本身就是載入回饋，再疊上淡化會像停用。沒有 `nx-table-state` 的表格照 `busy` 顯示。
   */
  protected readonly refreshing = computed(
    () => this.working() && !(this.state() ?? this.builtinState())?.visible(),
  );
  /** 有資料就顯示分頁列：筆數只在頁尾出現（data-workspace），不到一頁也要看得到。 */
  protected readonly paged = computed(() => {
    const model = this.table();
    return !!model && (model.pageIndex() > 0 || model.rows().length > 0);
  });
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
  /** 放大成貼齊視窗；頁面與詳情抽屜裡都可以，對話框裡的表格用對話框自己的放大（CSS 隱藏按鈕）。 */
  readonly maximized = signal(false);
  /** 捲動區放不下內容（列多或欄寬）才顯示放大鈕，列少的表格不多一顆按鈕。 */
  protected readonly overflowing = signal(false);
  private readonly frame = viewChild.required<ElementRef<HTMLElement>>('frame');
  /** 放大時才取 `ThemeService`：單元測試沒有 matchMedia，也不跑動畫。 */
  private readonly injector = inject(Injector);
  private motion?: Animation;
  constructor() {
    const destroyRef = inject(DestroyRef);
    afterNextRender(() => {
      const region = this.scrollRegion();
      if (!region || typeof ResizeObserver === 'undefined') return;
      // 放大中（含還原的動畫）不量：外框貼齊視窗時多半放得下，按鈕會在還原那一刻消失、焦點跟著掉。
      const measure = () => {
        if (this.frame().nativeElement.classList.contains('is-maximized')) return;
        this.overflowing.set(
          region.scrollHeight > region.clientHeight + 1 ||
            region.scrollWidth > region.clientWidth + 1,
        );
      };
      // 捲動區本身與裡面的表格：列數變了捲動區不一定變大小，表格會。
      const observer = new ResizeObserver(measure);
      observer.observe(region);
      for (const child of region.children) observer.observe(child);
      destroyRef.onDestroy(() => observer.disconnect());
    });
    destroyRef.onDestroy(() => this.motion?.cancel());
    // 換頁、排序、篩選或換查詢條件後回到頂端；背景更新不改 revision，保留捲動位置。
    effect(() => {
      this.table()?.revision();
      untracked(() => this.resetScroll());
    });
    effect(() => {
      const columns = this.pickerColumns(),
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
    if (!this.pickerColumns().some((column) => column.id === id && column.hideable !== false))
      return;
    this.hidden.update((hidden) =>
      hidden.includes(id) ? hidden.filter((key) => key !== id) : [...hidden, id],
    );
    this.saveColumns();
  }
  resetColumns() {
    this.hidden.set(
      this.pickerColumns()
        .filter((column) => column.defaultHidden)
        .map((column) => column.id),
    );
    this.saveColumns();
  }
  private saveColumns() {
    if (!this.preferenceKey()) return;
    try {
      const preferences = Object.fromEntries(
        this.pickerColumns().map((column) => [column.id, this.isVisible(column.id)]),
      );
      localStorage.setItem('ai-nexus.table.' + this.preferenceKey(), JSON.stringify(preferences));
    } catch {
      /* 選用偏好。 */
    }
  }
  /**
   * 放大與還原都量好起訖兩個矩形，再讓外框（`position: fixed`）從起點過渡到終點；DOM 不搬動，捲動位置、
   * 欄位偏好與列內元件都保留。外框離開文件流時宿主以 `min-height` 佔住原本的高度，頁面不跳動也不改捲動。
   * 還原時先回到原位量出終點，再固定回去播完動畫，結束時才真的回到文件流（`fill: forwards` 避免最後一幀閃回全螢幕）。
   */
  setMaximized(on: boolean) {
    if (on === this.maximized()) return;
    const frame = this.frame().nativeElement,
      host = this.host.nativeElement;
    const place = (maximized: boolean) => {
      host.style.minHeight = maximized ? `${host.offsetHeight}px` : '';
      frame.classList.toggle('is-maximized', maximized);
    };
    const from = frame.getBoundingClientRect(),
      fromRadius = getComputedStyle(frame).borderRadius;
    this.motion?.cancel();
    place(false);
    const inline = frame.getBoundingClientRect(),
      radius = getComputedStyle(frame).borderRadius;
    place(true);
    const full = frame.getBoundingClientRect();
    this.maximized.set(on);
    if (typeof frame.animate !== 'function' || this.injector.get(ThemeService).reducedMotion()) {
      place(on);
      return;
    }
    const box = (rect: DOMRect, borderRadius: string) => ({
      top: `${rect.top}px`,
      left: `${rect.left}px`,
      width: `${rect.width}px`,
      height: `${rect.height}px`,
      borderRadius,
    });
    // 起點是當下的位置（動畫中途反向時也接得上），終點是原位或整個視窗。
    this.motion = frame.animate(
      [box(from, fromRadius), on ? box(full, '0px') : box(inline, radius)],
      { ...motionTiming(frame, '--motion-panel'), fill: 'forwards' },
    );
    this.motion.onfinish = () => {
      this.motion?.cancel();
      this.motion = undefined;
      if (!on) place(false);
    };
  }
  /**
   * 放大時 Esc 還原；疊在表格上的浮層、對話框或抽屜先處理自己的 Esc。
   * 包著表格的抽屜不算：放大的表格蓋在它上面，Esc 先還原表格，抽屜看到 `defaultPrevented` 就不關。
   */
  escapeMaximized(event: Event) {
    if (
      !this.maximized() ||
      event.defaultPrevented ||
      [...document.querySelectorAll('dialog[open], [popover]:popover-open:not(.toaster)')].some(
        (layer) => !layer.contains(this.host.nativeElement),
      )
    )
      return;
    event.preventDefault();
    this.setMaximized(false);
  }
  /**
   * 焦點移到被蓋住的元素（Tab 出表格）就還原；移進疊在上面的對話框、抽屜或浮層不算。
   * 包著表格的抽屜在放大的表格底下，焦點移到它的其他元素也要還原。
   */
  focusLeft(event: FocusEvent) {
    const to = event.relatedTarget;
    if (!this.maximized() || !(to instanceof Element) || this.frame().nativeElement.contains(to))
      return;
    const layer = to.closest('dialog, [popover]');
    if (!layer || layer.contains(this.host.nativeElement)) this.setMaximized(false);
  }
  resetScroll() {
    const region = this.scrollRegion();
    if (region) region.scrollTop = 0;
  }
  /** 最後一個欄位篩選 chip 移除後，焦點交給表格的捲動區。 */
  focusScroll() {
    this.scrollRegion()?.focus({ preventScroll: true });
  }
  private scrollRegion() {
    return this.host.nativeElement.querySelector<HTMLElement>('.ui-table-scroll');
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
      // label 會把點擊轉給它的控制項（列內的核取方塊），一樣算巢狀控制項。
      event.target.closest('button,a,input,label,select,textarea,[contenteditable="true"]') ||
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
 * 頁面自己顯示）。載入中畫成幾列骨架（列的形狀已知），文字只給輔助科技讀。
 *
 * 綁法：`loading` 綁首次載入（`apiResource` 的 `loading()`），只決定要不要畫骨架；`retrying`
 * 綁重讀（`refreshing()`），重試鈕在重讀期間轉圈。`apiResource` 的 `reload()` 不會讓 `loading()`
 * 變真，所以重試的回饋不能靠 `loading`。放在 `nx-data-table` 裡時也跟著表格的 `busy` 轉圈。
 * 表格有 `[table]` 時，沒綁的 `count`、`loading`、`error` 取自它，重試也直接重讀它。
 */
@Component({
  selector: 'nx-table-state',
  imports: [Icon, PendingButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    // 讀取失敗要立即讀出，與 nx-notice 的 danger 相同；載入中與空狀態只是狀態更新。
    '[attr.role]': 'shownError() ? "alert" : "status"',
    '[hidden]': '!visible()',
    '[attr.data-loading]': 'shownLoading() && !shownError() ? "" : null',
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
    :host([data-loading]) {
      justify-items: stretch;
      gap: var(--p-space-4);
      padding: var(--p-space-4);
    }
    .ui-table-skeleton {
      height: 0.875rem;
    }
    p {
      margin: 0;
    }
    :host > nx-icon {
      color: var(--muted);
    }
  `,
  template: `@if (shownError()) {
      <nx-icon name="info" />
      <strong>{{ errorTitle() }}</strong>
      <p>{{ shownError() }}</p>
      <button
        nxPending
        type="button"
        class="secondary-button"
        [pending]="retryPending()"
        (click)="retryRead()"
      >
        <nx-icon name="refresh" />{{ retryPending() ? '重試中…' : '重試' }}
      </button>
    } @else if (shownLoading()) {
      @for (width of skeleton; track $index) {
        <span class="skeleton ui-table-skeleton" aria-hidden="true" [style.width.%]="width"></span>
      }
      <p class="sr-only">{{ loadingText() }}</p>
    } @else {
      <nx-icon name="search" />
      <strong>{{ filtered() ? '沒有符合欄位篩選的資料' : emptyTitle() }}</strong>
      @if (emptyText()) {
        <p>{{ emptyText() }}</p>
      }
      <ng-content />
    }`,
})
export class TableState {
  /** 顯示中的列數；有列就隱藏。 */
  readonly count = input<number>();
  /** 首次載入：沒有列時畫骨架。 */
  readonly loading = input<boolean>();
  /** 失敗後重讀中：重試鈕轉圈並擋重複按下。 */
  readonly retrying = input(false);
  readonly error = input<string | null | undefined>();
  readonly loadingText = input('正在載入…');
  readonly errorTitle = input('無法載入資料');
  readonly emptyTitle = input('沒有符合條件的資料');
  readonly emptyText = input('');
  readonly retry = output<void>();
  private readonly table = inject(DataTable, { optional: true });
  private readonly model = computed(() => this.table?.table() ?? null);
  protected readonly shownCount = computed(() => this.count() ?? this.model()?.rows().length ?? 0);
  protected readonly shownLoading = computed(() => this.loading() ?? !!this.model()?.loading());
  /** 已有列時的重讀失敗由頁面的 Notice 顯示，這裡只顯示沒有列時的失敗。 */
  protected readonly shownError = computed(() => {
    const error = this.error();
    if (error !== undefined) return error;
    return this.shownCount() ? '' : (this.model()?.error() ?? '');
  });
  /** 空狀態是因為欄位篩選：換一句話，提示清除篩選。 */
  protected readonly filtered = computed(
    () => !!this.model() && Object.keys(this.model()!.filters()).length > 0,
  );
  readonly visible = computed(() => this.shownCount() === 0);
  protected readonly retryPending = computed(
    () => this.retrying() || this.shownLoading() || !!this.table?.working(),
  );
  retryRead() {
    this.retry.emit();
    if (this.error() === undefined) this.model()?.reload();
  }
  /** 骨架列的寬度（%），長短交錯才不像一整塊色塊。 */
  protected readonly skeleton = [92, 74, 86, 62, 80];
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
      @if (view().mode === 'offset') {
        <span role="status">
          @if (view().total) {
            <b>{{ span() }}</b> / {{ totalText() }} {{ view().unit }}
          } @else {
            0 {{ view().unit }}
          }
        </span>
      } @else {
        <span role="status"
          >本頁 <b>{{ view().count }}</b> {{ view().unit }}</span
        >
      }
      <span class="ui-pagination-size">
        <span aria-hidden="true">·</span>
        @if (view().pageSizes.length > 1) {
          <nx-select
            appearance="inline"
            label="每頁筆數"
            [options]="sizes()"
            [value]="view().pageSize.toString()"
            (valueChange)="changeSize(+$event)"
          />
        } @else {
          <span>每頁 {{ view().pageSize }} {{ view().unit }}</span>
        }
      </span>
    </div>
    <div class="ui-pagination-group" role="group" aria-label="分頁">
      @if (view().mode === 'cursor') {
        <button
          type="button"
          class="icon-button"
          aria-label="回到第一頁"
          title="回到第一頁"
          [attr.aria-disabled]="view().busy || view().pageIndex === 0 || null"
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
        [attr.aria-disabled]="view().busy || view().pageIndex === 0 || null"
        (click)="step(-1)"
      >
        <nx-icon name="left" />
      </button>
      @if (view().mode === 'offset') {
        <label class="ui-pagination-page"
          ><input
            type="number"
            inputmode="numeric"
            min="1"
            [max]="pageCount()"
            aria-label="跳到頁碼"
            title="輸入頁碼後按 Enter"
            [value]="view().pageIndex + 1"
            [readOnly]="view().busy"
            [disabled]="pageCount() <= 1"
            (keydown.enter)="jump($event)"
            (change)="jump($event)"
          />/ {{ pageCount() }}</label
        >
      } @else {
        <span class="ui-pagination-page"
          >第 <b>{{ view().pageIndex + 1 }}</b> 頁</span
        >
      }
      <button
        type="button"
        class="icon-button"
        aria-label="下一頁"
        title="下一頁"
        [attr.aria-disabled]="view().busy || !next() || null"
        (click)="step(1)"
      >
        <nx-icon name="chevron" />
      </button>
    </div>`,
})
export class TablePagination {
  /**
   * 表格模型：給了就從它取模式、頁碼、筆數與單位，翻頁直接呼叫它，其餘 input 不必綁
   * （表格以外的格線、選擇器也用同一個模型分頁）。`busy` 仍可另外給（例：搜尋防抖中）。
   */
  // eslint-disable-next-line @typescript-eslint/no-explicit-any -- 各頁的列型別不同
  readonly table = input<TableModel<any> | null>(null);
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
  /** 計數單位：依人分頁的表寫「人」。 */
  readonly unit = input('筆');
  readonly pageChange = output<-1 | 1>();
  readonly pageSelect = output<number>();
  readonly pageSizeChange = output<number>();
  /** 顯示用的值：有模型取模型，沒有取 input。 */
  protected readonly view = computed(() => {
    const table = this.table();
    return table
      ? {
          mode: table.mode(),
          pageIndex: table.pageIndex(),
          pageSize: table.pageSize(),
          pageSizes: table.pageSizes,
          count: table.rows().length,
          hasNext: table.hasNext(),
          total: table.total(),
          busy: this.busy() || table.refreshing(),
          unit: table.unit,
        }
      : {
          mode: this.mode(),
          pageIndex: this.pageIndex(),
          pageSize: this.pageSize(),
          pageSizes: this.pageSizes(),
          count: this.count(),
          hasNext: this.hasNext(),
          total: this.total(),
          busy: this.busy(),
          unit: this.unit(),
        };
  });
  readonly sizes = computed(() =>
    this.view().pageSizes.map((size) => ({
      value: String(size),
      label: `每頁 ${size} ${this.view().unit}`,
    })),
  );
  readonly pageCount = computed(() =>
    Math.max(1, Math.ceil(this.view().total / this.view().pageSize)),
  );
  readonly next = computed(() =>
    this.view().mode === 'offset'
      ? this.view().pageIndex < this.pageCount() - 1
      : this.view().hasNext,
  );
  /** 這一頁的起訖列號，例如 `101–150`。 */
  readonly span = computed(() => {
    const first = this.view().pageIndex * this.view().pageSize;
    return `${first + 1}–${first + this.view().count}`;
  });
  readonly totalText = computed(() => this.view().total.toLocaleString('zh-TW'));
  /** 已送出、尚未讀完的頁；Enter 之後失焦還會再觸發一次 change，靠它避免重複讀同一頁。 */
  private requested = -1;
  constructor() {
    // 讀完（或沒有 busy 的呼叫端換了頁）才清掉；讀取失敗時可以再送同一頁。
    effect(() => {
      const { pageIndex, busy } = this.view();
      void pageIndex;
      if (!busy) this.requested = -1;
    });
  }
  step(direction: -1 | 1) {
    const { busy, pageIndex } = this.view();
    if (busy || (direction === -1 ? pageIndex === 0 : !this.next())) return;
    const table = this.table();
    if (table) table.turn(direction);
    else this.pageChange.emit(direction);
  }
  select(index: number) {
    const { busy, pageIndex } = this.view();
    if (busy || index === pageIndex || index === this.requested) return;
    this.requested = index;
    const table = this.table();
    if (table) table.goTo(index);
    else this.pageSelect.emit(index);
  }
  changeSize(size: number) {
    const { busy, pageSize } = this.view();
    if (busy || size === pageSize) return;
    const table = this.table();
    if (table) table.resize(size);
    else this.pageSizeChange.emit(size);
  }
  jump(event: Event) {
    const input = event.target as HTMLInputElement;
    const page = Math.round(Number(input.value));
    if (!Number.isFinite(page) || page < 1 || page > this.pageCount()) {
      input.value = String(this.view().pageIndex + 1);
      return;
    }
    this.select(page - 1);
  }
}
