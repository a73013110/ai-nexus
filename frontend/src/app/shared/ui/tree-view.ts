import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { Icon } from './icon';

/**
 * 樹的一個節點。`children` 為 undefined 表示還沒載入（展開時由使用端載入），空陣列表示沒有下層；
 * `count` 是節點旁的數量（例如人數），`badge` 是醒目的小計（例如已選人數），`muted` 讓不常用的節點淡一點。
 */
export interface TreeItem {
  id: string;
  label: string;
  icon?: string;
  title?: string;
  count?: number | null;
  /** 報讀用的數量說明（例：「含下層 12 人」）；沒給時只讀數字。 */
  countLabel?: string;
  badge?: number;
  badgeLabel?: string;
  muted?: boolean;
  hasChildren: boolean;
  loading?: boolean;
  children?: readonly TreeItem[];
}

interface TreeRow {
  item: TreeItem;
  level: number;
  parent: string | null;
  open: boolean;
}

/**
 * 可延遲載入的樹（WAI-ARIA tree）：展開狀態與選取由使用端持有，元件只負責畫面與鍵盤。
 * 上下鍵移動、右鍵展開或進到第一個子節點、左鍵收合或回到上層、Home／End、Enter／空白鍵選取；
 * 焦點只在一個節點上（roving tabindex），Tab 進出整棵樹一次。
 */
@Component({
  selector: 'nx-tree-view',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './tree-view.scss',
  template: `@if (busy() && !items().length) {
      <div class="ui-tree-skeleton" role="status" [attr.aria-label]="'正在載入' + label()">
        @for (row of skeleton; track $index) {
          <span [style.--tree-level]="row.level"
            ><i class="skeleton" [style.width.%]="row.width"></i
          ></span>
        }
      </div>
    }
    <ul class="ui-tree" role="tree" [attr.aria-label]="label()" [hidden]="!rows().length">
      @for (row of rows(); track row.item.id) {
        <li
          role="treeitem"
          class="ui-tree-item"
          [class.ui-tree-muted]="row.item.muted"
          [attr.data-id]="row.item.id"
          [attr.aria-level]="row.level + 1"
          [attr.aria-expanded]="row.item.hasChildren ? row.open : null"
          [attr.aria-selected]="row.item.id === selected()"
          [attr.aria-busy]="row.item.loading || null"
          [attr.tabindex]="row.item.id === focusId() ? 0 : -1"
          [attr.title]="row.item.title || null"
          [style.--tree-level]="row.level"
          (click)="click($event, row)"
          (keydown)="key($event)"
          (focus)="focused.set(row.item.id)"
        >
          <span
            class="ui-tree-twisty"
            [class.ui-tree-open]="row.open"
            [class.ui-tree-leaf]="!row.item.hasChildren"
            aria-hidden="true"
            ><nx-icon name="chevron"
          /></span>
          @if (row.item.icon) {
            <nx-icon class="ui-tree-icon" [name]="row.item.icon" />
          }
          <span class="ui-tree-label">{{ row.item.label }}</span>
          @if (row.item.loading) {
            <nx-icon class="ui-tree-spinner" name="loading" />
          } @else if (row.item.count !== undefined && row.item.count !== null) {
            <span class="ui-tree-count" [attr.aria-hidden]="row.item.countLabel ? true : null">{{
              row.item.count
            }}</span>
            @if (row.item.countLabel) {
              <span class="sr-only">{{ row.item.countLabel }}</span>
            }
          }
          @if (row.item.badge) {
            <!-- 一般 span 上的 aria-label 多數報讀器不讀，改用隱藏文字。 -->
            <span class="ui-tree-badge" [attr.aria-hidden]="row.item.badgeLabel ? true : null">{{
              row.item.badge
            }}</span>
            @if (row.item.badgeLabel) {
              <span class="sr-only">{{ row.item.badgeLabel }}</span>
            }
          }
        </li>
      }
    </ul>`,
})
export class TreeView {
  readonly label = input.required<string>();
  readonly items = input.required<readonly TreeItem[]>();
  readonly selected = input<string | null>(null);
  readonly expanded = input<ReadonlySet<string>>(new Set());
  /** 第一層還在載入：沒有節點時顯示骨架。下層的載入用節點自己的 `loading`。 */
  readonly busy = input(false);
  protected readonly skeleton = [
    { level: 0, width: 70 },
    { level: 1, width: 82 },
    { level: 1, width: 64 },
    { level: 1, width: 76 },
    { level: 1, width: 58 },
    { level: 1, width: 70 },
  ];
  readonly nodeSelect = output<string>();
  /** 使用者要展開或收合；`open` 是要變成的狀態。 */
  readonly nodeToggle = output<{ id: string; open: boolean }>();
  protected readonly focused = signal<string | null>(null);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  readonly rows = computed(() => {
    const rows: TreeRow[] = [];
    const expanded = this.expanded();
    const walk = (items: readonly TreeItem[], level: number, parent: string | null) => {
      for (const item of items) {
        const open = item.hasChildren && expanded.has(item.id);
        rows.push({ item, level, parent, open });
        if (open && item.children) walk(item.children, level + 1, item.id);
      }
    };
    walk(this.items(), 0, null);
    return rows;
  });

  /** 可聚焦的節點：上次聚焦的節點還在就沿用，否則是選取的節點或第一個。 */
  protected readonly focusId = computed(() => {
    const ids = this.rows().map((row) => row.item.id);
    const focused = this.focused();
    if (focused && ids.includes(focused)) return focused;
    const selected = this.selected();
    return selected && ids.includes(selected) ? selected : (ids[0] ?? null);
  });

  /** 點三角形只展開或收合，點其他地方選取節點；鍵盤由 `key` 處理。 */
  click(event: MouseEvent, row: TreeRow) {
    this.focused.set(row.item.id);
    if (event.target instanceof Element && event.target.closest('.ui-tree-twisty')) {
      if (row.item.hasChildren) this.nodeToggle.emit({ id: row.item.id, open: !row.open });
    } else this.nodeSelect.emit(row.item.id);
  }

  key(event: KeyboardEvent) {
    const rows = this.rows();
    const index = rows.findIndex((row) => row.item.id === this.focusId());
    const row = rows[index];
    if (!row) return;
    let target: number | null = null;
    switch (event.key) {
      case 'ArrowDown':
        target = Math.min(rows.length - 1, index + 1);
        break;
      case 'ArrowUp':
        target = Math.max(0, index - 1);
        break;
      case 'Home':
        target = 0;
        break;
      case 'End':
        target = rows.length - 1;
        break;
      case 'ArrowRight':
        if (row.item.hasChildren && !row.open)
          this.nodeToggle.emit({ id: row.item.id, open: true });
        else if (row.open && rows[index + 1]?.parent === row.item.id) target = index + 1;
        break;
      case 'ArrowLeft':
        if (row.open) this.nodeToggle.emit({ id: row.item.id, open: false });
        else if (row.parent) target = rows.findIndex((item) => item.item.id === row.parent);
        break;
      case 'Enter':
      case ' ':
        this.nodeSelect.emit(row.item.id);
        break;
      default:
        return;
    }
    event.preventDefault();
    if (target === null || target < 0) return;
    const id = rows[target].item.id;
    this.focused.set(id);
    this.host.nativeElement
      .querySelector<HTMLElement>(`[data-id="${CSS.escape(id)}"]`)
      ?.focus({ preventScroll: false });
  }
}
