import {
  ApplicationRef,
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  Directive,
  ElementRef,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { GripVertical } from 'lucide';
import { ThemeService } from '@core/preferences/theme-service';
import { edgeScrollSpeed, flipRows, layoutBox, scrollParent } from '../browser/reorder-motion';
import { shiftId, type OrderShift } from '../browser/sort-order';
import { EXTRA_ICONS, Icon } from './icon';

/** 一次移動：`index` 是放下後在這組列（`nxSortableRows` 的 `ids`）中的位置，從 0 起算。 */
export interface RowMove {
  id: string;
  index: number;
}

interface Drag {
  id: string;
  from: number;
  /** 目前的落點；拖曳中只改畫面（其他列讓位），放手才送出。 */
  to: number;
  row: HTMLElement;
  rows: HTMLElement[];
  pointer: number;
  offset: number;
  last: PointerEvent;
  scroll: number;
}

const KEYS: Record<string, OrderShift> = {
  ArrowUp: -1,
  ArrowDown: 1,
  PageUp: -10,
  PageDown: 10,
  Home: 'first',
  End: 'last',
};

const KEY_HINT = '上下鍵移動一位，PageUp／PageDown 移動十位，Home／End 移到最前或最後';

/**
 * 讓既有的列（表格 `tbody`、清單 `ol`）可以手動排序，規範見 docs/architecture/manual-ordering.md。
 * 列標 `data-sort-id`，列內放 `nx-sort-handle`；指令不渲染列，只處理拖曳、方向鍵與動效，送出 `sortMove`。
 * 呼叫端在 `sortMove` 裡同步換掉列的順序（樂觀更新）再寫入，失敗時換回並提示；指令在換序後以 FLIP 讓列滑到新位置。
 * 拖曳中只位移畫面（其他列讓位），不改資料，所以不會因為重新渲染打斷拖曳。搜尋或篩選時給 `sortDisabled`：
 * 看不到鄰居，拖曳與方向鍵沒有意義，改用 `nx-sort-rank` 輸入整份名單的位置。
 */
@Directive({
  selector: '[nxSortableRows]',
  host: {
    class: 'sortable-rows',
    '[class.is-sorting]': 'dragging() !== null',
    '(pointerdown)': 'grab($event)',
    '(keydown)': 'key($event)',
  },
})
export class SortableRows {
  /** 這組列目前的順序。 */
  readonly ids = input.required<readonly string[]>({ alias: 'nxSortableRows' });
  readonly sortDisabled = input(false);
  /** 報讀用的名稱，例如使用者姓名。 */
  readonly sortName = input<(id: string) => string>((id) => id);
  readonly sortMove = output<RowMove>();

  private readonly app = inject(ApplicationRef);
  private readonly theme = inject(ThemeService);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  readonly dragging = signal<string | null>(null);
  private drag: Drag | null = null;
  /** 全頁共用一個報讀區；tbody 裡不能放其他元素。 */
  private readonly live = document.body.appendChild(
    Object.assign(document.createElement('p'), { className: 'sr-only' }),
  );

  constructor() {
    this.live.setAttribute('aria-live', 'polite');
    inject(DestroyRef).onDestroy(() => {
      this.end();
      this.live.remove();
    });
  }

  key(event: KeyboardEvent) {
    const id = this.handleOf(event.target);
    const shift = KEYS[event.key];
    if (!id || shift === undefined) return;
    event.preventDefault();
    if (this.sortDisabled() || this.drag) return;
    this.place(id, shiftId([...this.ids()], id, shift).indexOf(id), true);
  }

  /** 移到 `index` 並讓其他列滑到新位置；`reveal` 時捲到該列、焦點回到把手並閃一下。 */
  private place(id: string, index: number, reveal: boolean, before?: Map<string, number>) {
    const from = this.ids().indexOf(id);
    if (from >= 0 && from !== index) {
      this.flip(() => this.sortMove.emit({ id, index }), before);
      this.live.textContent = `「${this.sortName()(id)}」移到第 ${index + 1} 列，共 ${this.ids().length} 列`;
    } else if (before) this.flip(() => undefined, before);
    if (!reveal) return;
    const row = this.row(id);
    row?.scrollIntoView({
      block: 'nearest',
      behavior: this.theme.reducedMotion() ? 'auto' : 'smooth',
    });
    this.handle(id)?.focus({ preventScroll: true });
    if (row && from !== index && !this.theme.reducedMotion()) flash(row);
  }

  // ── 拖曳 ──
  grab(event: PointerEvent) {
    const id = this.handleOf(event.target);
    if (!id || event.button !== 0 || this.drag || this.sortDisabled() || this.ids().length < 2)
      return;
    const row = this.row(id);
    if (!row) return;
    event.preventDefault();
    const rows = this.rows();
    const from = rows.indexOf(row);
    this.drag = {
      id,
      from,
      to: from,
      row,
      rows,
      pointer: event.pointerId,
      offset: event.clientY - layoutBox(row).top,
      last: event,
      scroll: 0,
    };
    row.classList.add('is-dragging');
    this.dragging.set(id);
    // 用視窗監聽器而不是指標捕獲：捲動與換位時指標下的元素一直在變。
    window.addEventListener('pointermove', this.pointerMove);
    window.addEventListener('pointerup', this.drop);
    window.addEventListener('pointercancel', this.cancel);
    window.addEventListener('keydown', this.escape, true);
  }

  private readonly pointerMove = (event: PointerEvent) => {
    const drag = this.drag;
    if (!drag || event.pointerId !== drag.pointer) return;
    drag.last = event;
    drag.scroll ||= requestAnimationFrame(() => this.autoscroll());
    this.step(drag, event.clientY);
  };

  /**
   * 被拖曳的列跟著指標；落點是「中線在指標上方的其他列」的數量。落點與原位之間的列讓出一列高。
   * 位置一律用 `layoutBox`（不含位移），讓位動畫進行中落點才不會來回跳。
   */
  private step(drag: Drag, pointY: number) {
    const others = drag.rows.filter((row) => row !== drag.row);
    const to = others.filter((row) => {
      const box = layoutBox(row);
      return pointY > box.top + box.height / 2;
    }).length;
    const lift = drag.row.offsetHeight;
    drag.to = to;
    others.forEach((row, index) => {
      const shift =
        drag.from < to && index >= drag.from && index < to
          ? -lift
          : to < drag.from && index >= to && index < drag.from
            ? lift
            : 0;
      row.style.translate = shift ? `0 ${shift}px` : '';
    });
    drag.row.style.translate = `0 ${pointY - drag.offset - layoutBox(drag.row).top}px`;
  }

  private autoscroll() {
    const drag = this.drag;
    if (!drag) return;
    drag.scroll = 0;
    const scroller = scrollParent(this.host);
    if (!scroller) return;
    const before = scroller.scrollTop;
    scroller.scrollTop += edgeScrollSpeed(scroller.getBoundingClientRect(), drag.last.clientY);
    if (scroller.scrollTop === before) return;
    this.step(drag, drag.last.clientY);
    drag.scroll = requestAnimationFrame(() => this.autoscroll());
  }

  private readonly drop = (event: PointerEvent) => {
    const drag = this.drag;
    if (!drag || event.pointerId !== drag.pointer) return;
    const before = this.end();
    this.place(drag.id, drag.to, false, before);
  };
  private readonly cancel = (event: PointerEvent) => {
    if (this.drag && event.pointerId === this.drag.pointer) this.abort();
  };
  /** 拖曳中按 Esc：放回原位，不送出。 */
  private readonly escape = (event: KeyboardEvent) => {
    if (event.key !== 'Escape' || !this.drag) return;
    event.preventDefault();
    event.stopPropagation();
    this.abort();
  };
  private abort() {
    this.flip(() => undefined, this.end());
    this.live.textContent = '已取消移動';
  }

  /** 結束拖曳並清掉位移；回傳清掉前各列在畫面上的位置，讓落定動畫從那裡開始。 */
  private end() {
    const drag = this.drag;
    if (!drag) return undefined;
    cancelAnimationFrame(drag.scroll);
    window.removeEventListener('pointermove', this.pointerMove);
    window.removeEventListener('pointerup', this.drop);
    window.removeEventListener('pointercancel', this.cancel);
    window.removeEventListener('keydown', this.escape, true);
    const before = positions(drag.rows);
    // 先拿掉 is-sorting（讓位的 transition 在它底下），清位移時才不會又滑一次。
    this.host.classList.remove('is-sorting');
    drag.row.classList.remove('is-dragging');
    for (const row of drag.rows) row.style.removeProperty('translate');
    this.drag = null;
    this.dragging.set(null);
    return before;
  }

  /** 執行換序並讓列從 `before`（省略時為換序前的位置）滑到新位置。 */
  private flip(change: () => void, before?: Map<string, number>) {
    flipRows(
      () => this.rows(),
      (row) => row.dataset['sortId'],
      () => {
        change();
        this.app.tick();
      },
      { reduced: this.theme.reducedMotion(), before },
    );
  }
  private rows() {
    return [...this.host.querySelectorAll<HTMLElement>(':scope > [data-sort-id]')];
  }
  private row(id: string) {
    return this.host.querySelector<HTMLElement>(`:scope > [data-sort-id="${CSS.escape(id)}"]`);
  }
  private handle(id: string) {
    return this.row(id)?.querySelector<HTMLElement>('[data-sort-handle]') ?? null;
  }
  private handleOf(target: EventTarget | null) {
    const handle =
      target instanceof Element ? target.closest<HTMLElement>('[data-sort-handle]') : null;
    return handle && this.host.contains(handle) ? (handle.dataset['sortHandle'] ?? null) : null;
  }
}

function positions(rows: HTMLElement[]) {
  return new Map(rows.map((row) => [row.dataset['sortId']!, row.getBoundingClientRect().top]));
}

function flash(row: HTMLElement) {
  row.animate([{ backgroundColor: 'var(--accent-soft)' }, { backgroundColor: 'transparent' }], {
    duration: 900,
    easing: 'ease-out',
  });
}

/**
 * 列的拖曳把手：放在 `nxSortableRows` 的列裡。拖曳或聚焦後按方向鍵移動；`locked` 時只說明為什麼不能拖。
 * 名稱帶目前位置，讓報讀知道這列在第幾位。
 */
@Component({
  selector: 'nx-sort-handle',
  imports: [Icon],
  styleUrl: './sortable-rows.scss',
  providers: [{ provide: EXTRA_ICONS, useValue: { grip: GripVertical } }],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<button
    type="button"
    class="icon-button sort-handle"
    [attr.data-sort-handle]="id()"
    [title]="locked() ? lockedHint() : '拖曳或按方向鍵調整順序'"
    [attr.aria-label]="
      '移動「' +
      name() +
      '」，目前第 ' +
      position() +
      ' 位' +
      (count() ? '，共 ' + count() + ' 位' : '')
    "
    [attr.aria-description]="locked() ? lockedHint() : hint"
    [attr.aria-disabled]="locked() || null"
  >
    <nx-icon name="grip" />
  </button>`,
})
export class SortHandle {
  readonly id = input.required<string>();
  readonly name = input.required<string>();
  /** 在整份清單中的位置，從 1 起算。 */
  readonly position = input.required<number>();
  /** 整份清單的筆數；不知道（例如搜尋中）時給 null。 */
  readonly count = input.required<number | null>();
  readonly locked = input(false);
  readonly lockedHint = input('清除搜尋後可拖曳；或按編號輸入位置');
  readonly hint = KEY_HINT;
}

/**
 * 位置編號：平常是安靜的數字，點了換成輸入框，Enter 移到第幾位（超過人數就放最後），Esc 取消。
 * 搜尋、分頁時也能用：位置是整份清單的，由呼叫端換算或交給伺服器。`unsorted` 的編號淡化。
 */
@Component({
  selector: 'nx-sort-rank',
  styleUrl: './sortable-rows.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'sort-rank-cell' },
  template: `@if (editing()) {
      <input
        class="sort-rank sort-rank-input"
        type="number"
        inputmode="numeric"
        min="1"
        [max]="count()"
        [value]="position()"
        [attr.aria-label]="
          '把「' + name() + '」移到第幾位' + (count() ? '（1 至 ' + count() + '）' : '')
        "
        (keydown)="key($event)"
        (blur)="editing.set(false)"
      />
    } @else {
      <button
        type="button"
        class="sort-rank"
        [class.is-unsorted]="unsorted()"
        [title]="unsorted() ? '未排序：依姓名排列。輸入位置即可排序' : '輸入位置'"
        [attr.aria-label]="
          '第 ' + position() + ' 位' + (unsorted() ? '（未排序）' : '') + '，輸入新位置：' + name()
        "
        [disabled]="disabled()"
        (click)="edit()"
      >
        {{ position() }}
      </button>
    }`,
})
export class SortRank {
  readonly name = input.required<string>();
  readonly position = input.required<number>();
  /** 整份清單的筆數，輸入超過時放到最後；不知道（例如搜尋中）時給 null，交給伺服器限制。 */
  readonly count = input.required<number | null>();
  readonly unsorted = input(false);
  readonly disabled = input(false);
  /** 新位置，從 1 起算，已限制在 `count` 以內；與目前相同時不送出。 */
  readonly place = output<number>();
  readonly editing = signal(false);
  private readonly app = inject(ApplicationRef);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;

  edit() {
    this.editing.set(true);
    this.app.tick();
    const field = this.host.querySelector<HTMLInputElement>('input');
    field?.focus();
    field?.select();
  }
  key(event: KeyboardEvent) {
    if (event.key === 'Escape') {
      event.preventDefault();
      event.stopPropagation();
      this.close();
    }
    if (event.key !== 'Enter') return;
    event.preventDefault();
    const value = Math.round(Number((event.target as HTMLInputElement).value));
    this.close();
    if (!Number.isFinite(value) || value < 1) return;
    const position = Math.min(value, this.count() ?? value);
    if (position !== this.position()) this.place.emit(position);
  }
  private close() {
    this.editing.set(false);
    this.app.tick();
    this.host.querySelector<HTMLButtonElement>('button')?.focus();
  }
}
