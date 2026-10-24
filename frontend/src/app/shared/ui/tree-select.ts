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
import { anchoredPopover } from '../browser/anchored-popover';
import { positionPopover } from '../browser/popover-position';
import { uniqueId } from '../browser/unique-id';
import { Checkbox } from './checkbox';
import { Icon } from './icon';

/** 樹的一個節點；清單依同層順序排列，`parent` 不在清單裡的當成最上層。 */
export interface TreeSelectNode {
  value: string;
  label: string;
  parent?: string | null;
  /** 自己停用；下層一併視為停用。 */
  inactive?: boolean;
  /** 列出但不能選（例如放不下的位置）；不影響下層。 */
  disabled?: boolean;
}

/**
 * 可選的節點狀態，由開發者決定、使用者不能改：`any` 都可選（停用的預設收起，面板底部可勾選列出）、
 * `active` 只列啟用的、`inactive` 只能選停用的（啟用的上層仍列出當路徑，但不能選）。
 */
export type TreeSelectStatus = 'any' | 'active' | 'inactive';

interface TreeSelectRow {
  node: TreeSelectNode;
  depth: number;
  /** 由最上層到上一層的名稱。 */
  path: string[];
  inactive: boolean;
  selectable: boolean;
  children: number;
}

/**
 * 從階層挑一個節點（單位、分類）：觸發鈕顯示「上層路徑 › 名稱」，路徑淡色、放不下先截路徑；
 * 面板是可展開收合的樹，搜尋時改成平面清單並在名稱上方標出所屬路徑。
 * `allLabel` 是釘在最上方、值為空字串的選項（「所有單位」「最上層」）。
 * 鍵盤：上下移動、右鍵展開／進入下層、左鍵收合／回上層、Enter 選取、Esc 關閉；面板有搜尋框時焦點在搜尋框。
 */
@Component({
  selector: 'nx-tree-select',
  imports: [Checkbox, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './tree-select.scss',
  template: `<button
      #trigger
      type="button"
      class="tree-select-trigger"
      role="combobox"
      [attr.aria-label]="label()"
      [attr.aria-expanded]="opened()"
      [attr.aria-haspopup]="search() ? 'dialog' : 'tree'"
      [attr.aria-controls]="search() ? id + '-panel' : id"
      [attr.aria-activedescendant]="!search() && opened() ? activeId() : null"
      [attr.aria-busy]="loading() || null"
      [attr.title]="summary().title"
      [disabled]="disabled() || (!nodes().length && !allLabel() && !loading())"
      (click)="toggle()"
      (keydown)="triggerKey($event)"
    >
      <span class="tree-select-value">
        @if (summary().path) {
          <span class="tree-select-path">{{ summary().path }}</span
          ><span class="tree-select-sep" aria-hidden="true">›</span>
        }
        <span class="tree-select-name">{{ summary().name }}</span>
      </span>
      @if (loading()) {
        <nx-icon name="loading" />
      } @else {
        <nx-icon name="chevron" class="tree-select-chevron" />
      }
    </button>
    <!-- 焦點在搜尋框或觸發鈕（aria-activedescendant），面板只轉送 footer 勾選框上的 Escape。 -->
    <!-- eslint-disable-next-line @angular-eslint/template/interactive-supports-focus -->
    <div
      #panel
      [id]="id + '-panel'"
      popover="auto"
      class="popover-panel tree-select-panel"
      [attr.role]="search() ? 'dialog' : null"
      [attr.aria-label]="label()"
      (toggle)="popover.sync($event)"
      (keydown.escape)="dismiss($event)"
    >
      @if (search()) {
        <div class="tree-select-search">
          <nx-icon name="search" /><input
            #searchBox
            type="search"
            role="combobox"
            aria-expanded="true"
            aria-haspopup="tree"
            [attr.aria-controls]="id"
            [attr.aria-activedescendant]="activeId()"
            [attr.aria-label]="'搜尋' + noun()"
            [placeholder]="'搜尋' + noun() + '…'"
            [value]="query()"
            (input)="filter($any($event.target).value)"
            (keydown)="searchKey($event)"
          />
        </div>
        <p class="sr-only" role="status">{{ query() ? entries().length + ' 個結果' : '' }}</p>
      }
      <div class="tree-select-body" role="tree" [id]="id" [attr.aria-label]="label()">
        @for (entry of entries(); track entry.key; let i = $index) {
          @let row = entry.row;
          <!-- 指標點選的便利；鍵盤由搜尋框或觸發鈕處理（aria-activedescendant）。 -->
          <!-- eslint-disable-next-line @angular-eslint/template/click-events-have-key-events -->
          <div
            role="treeitem"
            tabindex="-1"
            class="tree-select-row"
            [id]="id + '-' + i"
            [class.is-active]="active() === i"
            [class.is-root]="!query() && row?.depth === 1"
            [attr.title]="row ? [...row.path, row.node.label].join(' › ') : null"
            [class.is-inactive]="row?.inactive"
            [class.is-context]="row ? !row.selectable : allDisabled()"
            [style.--tree-depth]="query() ? 1 : (row?.depth ?? 1)"
            [attr.aria-level]="query() || !row ? 1 : row.depth"
            [attr.aria-expanded]="
              !query() && row?.children ? !collapsed().has(row.node.value) : null
            "
            [attr.aria-selected]="isSelected(entry)"
            [attr.aria-disabled]="(row ? !row.selectable : allDisabled()) || null"
            (pointermove)="active.set(i)"
            (mousedown)="$event.preventDefault()"
            (click)="choose(i)"
          >
            @if (row && !query()) {
              <span
                class="tree-select-twisty"
                aria-hidden="true"
                [class.is-leaf]="!row.children"
                [class.is-open]="!collapsed().has(row.node.value)"
                (click)="$event.stopPropagation(); expand(row, collapsed().has(row.node.value))"
                ><nx-icon name="chevron"
              /></span>
            } @else if (!row && !query()) {
              <span class="tree-select-twisty is-leaf" aria-hidden="true"></span>
            }
            <span class="tree-select-label">
              @if (query() && row) {
                <small>{{ row.path.length ? row.path.join(' › ') : '最上層' }}</small>
              }
              @let parts = highlight(row?.node?.label ?? allLabel());
              <strong
                >{{ parts.before }}<mark>{{ parts.hit }}</mark
                >{{ parts.after }}</strong
              >
            </span>
            @if (row?.node?.inactive) {
              <span class="tree-select-tag">停用</span>
            }
            @if (isSelected(entry)) {
              <nx-icon class="tree-select-check" name="check" />
            }
          </div>
          @if (!row && !query()) {
            <div class="tree-select-divider" role="none"></div>
          }
        } @empty {
          <p class="tree-select-empty" [attr.role]="loading() ? 'status' : null">
            @if (loading()) {
              正在載入選項…
            } @else if (query()) {
              沒有符合「{{ query() }}」的{{ noun() }}
              @if (hiddenMatches()) {
                <br />另有 {{ hiddenMatches() }} 個停用的{{ noun() }}，勾選下方即可列出
              }
            } @else {
              沒有可選的{{ noun() }}
            }
          </p>
        }
      </div>
      @if (canToggle()) {
        <div class="tree-select-footer">
          <nx-checkbox
            [label]="'顯示停用的' + noun()"
            [checked]="showInactive()"
            (checkedChange)="setShowInactive($event)"
          />
        </div>
      }
    </div>`,
})
export class TreeSelect {
  readonly label = input.required<string>();
  readonly nodes = input.required<readonly TreeSelectNode[]>();
  readonly value = input('');
  readonly placeholder = input('請選擇');
  /** 釘在最上方的「全部／最上層」選項，值為空字串；不給就沒有。 */
  readonly allLabel = input('');
  /** `allLabel` 列出但不能選。 */
  readonly allDisabled = input(false);
  /** 搜尋框、空狀態與停用切換的名詞，例如「單位」。 */
  readonly noun = input('項目');
  readonly status = input<TreeSelectStatus>('any');
  readonly disabled = input(false);
  /** 節點讀取中；綁 `apiResource` 的 `refreshing()`。 */
  readonly loading = input(false);
  /** 節點達到這個數量才顯示搜尋框。 */
  readonly searchFrom = input(8);
  readonly valueChange = output<string>();

  readonly id = uniqueId('nx-tree-select');
  readonly query = signal('');
  readonly active = signal(-1);
  readonly collapsed = signal<ReadonlySet<string>>(new Set());
  readonly showInactive = signal(false);
  readonly search = computed(() => this.nodes().length >= this.searchFrom());
  readonly canToggle = computed(
    () => this.status() === 'any' && this.allRows().some((row) => row.inactive),
  );

  /** 全部節點依樹的先序排列，並算出停用（繼承上層）與是否可選。 */
  private readonly allRows = computed(() => {
    const nodes = this.nodes();
    const values = new Set(nodes.map((node) => node.value));
    const children = new Map<string | null, TreeSelectNode[]>();
    for (const node of nodes) {
      const key = node.parent && values.has(node.parent) ? node.parent : null;
      children.set(key, [...(children.get(key) ?? []), node]);
    }
    const status = this.status();
    const rows: TreeSelectRow[] = [];
    const visit = (parent: string | null, path: string[], inactive: boolean, seen: Set<string>) => {
      for (const node of children.get(parent) ?? []) {
        if (seen.has(node.value)) continue;
        const off = inactive || !!node.inactive;
        const selectable = !node.disabled && (status === 'any' || (status === 'inactive') === off);
        rows.push({ node, depth: path.length + 1, path, inactive: off, selectable, children: 0 });
        // `seen` 擋掉資料錯誤造成的循環（A 的上層是 B、B 的上層是 A）。
        visit(node.value, [...path, node.label], off, new Set(seen).add(node.value));
      }
    };
    visit(null, [], false, new Set());
    return this.recount(rows);
  });

  /** 依 `status` 與「顯示停用」留下的列；`inactive` 模式保留通往停用節點的啟用上層。 */
  private readonly shownRows = computed(() => {
    const rows = this.allRows();
    const status = this.status();
    const show = this.showInactive();
    const keep = (row: TreeSelectRow) =>
      status === 'active' || (status === 'any' && !show) ? !row.inactive : true;
    const shown = rows.filter(keep);
    if (status !== 'inactive') return this.recount(shown);
    const needed = new Set<string>();
    for (const row of rows.filter((item) => item.selectable))
      for (const name of [...row.path.keys(), row.path.length])
        needed.add(this.ancestorAt(rows, row, name));
    return this.recount(rows.filter((row) => needed.has(row.node.value)));
  });

  readonly entries = computed(() => {
    const query = this.query().trim().toLocaleLowerCase();
    const all = this.allLabel() ? [{ key: '', row: undefined }] : [];
    if (query)
      return this.shownRows()
        .filter((row) => row.selectable && row.node.label.toLocaleLowerCase().includes(query))
        .map((row) => ({ key: row.node.value, row: row as TreeSelectRow | undefined }));
    const collapsed = this.collapsed();
    const entries: { key: string; row: TreeSelectRow | undefined }[] = [...all];
    let hideBelow = Infinity;
    for (const row of this.shownRows()) {
      if (row.depth > hideBelow) continue;
      hideBelow = collapsed.has(row.node.value) ? row.depth : Infinity;
      entries.push({ key: row.node.value, row });
    }
    return entries;
  });

  /** 搜尋時被「顯示停用」藏起來的符合項目。 */
  readonly hiddenMatches = computed(() => {
    const query = this.query().trim().toLocaleLowerCase();
    if (!query || !this.canToggle() || this.showInactive()) return 0;
    return this.allRows().filter(
      (row) => row.inactive && row.node.label.toLocaleLowerCase().includes(query),
    ).length;
  });

  private readonly selectedRow = computed(() =>
    this.allRows().find((row) => row.node.value === this.value()),
  );
  readonly summary = computed(() => {
    const row = this.selectedRow();
    if (row)
      return {
        path: row.path.join(' › '),
        name: row.node.label,
        title: [...row.path, row.node.label].join(' › '),
      };
    const name =
      !this.value() && this.allLabel()
        ? this.allLabel()
        : this.loading()
          ? '正在載入…'
          : this.placeholder();
    return { path: '', name, title: name };
  });
  readonly activeId = computed(() => (this.active() >= 0 ? `${this.id}-${this.active()}` : null));

  private readonly trigger = viewChild.required<ElementRef<HTMLButtonElement>>('trigger');
  private readonly panel = viewChild.required<ElementRef<HTMLDivElement>>('panel');
  private readonly searchBox = viewChild<ElementRef<HTMLInputElement>>('searchBox');
  protected readonly popover = anchoredPopover({
    panel: () => this.panel().nativeElement,
    trigger: () => this.trigger().nativeElement,
    closeOnFocusOut: true,
    place: (panel) => positionPopover(this.trigger().nativeElement, panel, 300, 'left', 420),
  });
  readonly opened = this.popover.opened;
  private frame = 0;

  constructor() {
    inject(DestroyRef).onDestroy(() => cancelAnimationFrame(this.frame));
    // 載入中打開的面板，節點到了之後高度會變，重新對齊錨點。
    afterRenderEffect(() => {
      this.entries();
      this.popover.reposition();
    });
  }

  isSelected(entry: { row?: TreeSelectRow }) {
    return (entry.row?.node.value ?? '') === this.value();
  }

  toggle() {
    if (this.opened()) this.close();
    else this.open();
  }
  private open(typed = '') {
    if (this.disabled()) return;
    const selected = this.selectedRow();
    // 已選的是停用節點時直接列出停用的，否則打開看不到自己選的。
    if (selected?.inactive && this.status() === 'any') this.showInactive.set(true);
    if (selected) {
      const ancestors = new Set(this.ancestors(selected));
      this.collapsed.update((set) => new Set([...set].filter((value) => !ancestors.has(value))));
    }
    this.query.set(typed);
    this.popover.show();
    // 沒選或選的看不到時，停在第一個能選的（`inactive` 模式的第一列可能只是路徑）。
    const entries = this.entries();
    const chosen = entries.findIndex((entry) => this.isSelected(entry));
    const first = entries.findIndex((entry) =>
      entry.row ? entry.row.selectable : !this.allDisabled(),
    );
    this.active.set(Math.max(0, chosen >= 0 ? chosen : first));
    this.searchBox()?.nativeElement.focus();
    this.reveal();
  }
  close() {
    this.popover.hide();
  }

  choose(index: number) {
    const entry = this.entries()[index];
    if (!entry || (entry.row ? !entry.row.selectable : this.allDisabled())) return;
    this.valueChange.emit(entry.row?.node.value ?? '');
    this.close();
    this.trigger().nativeElement.focus();
  }

  /** 展開（`open`）或收合一個節點；收合時焦點若在它的下層，移回它自己。 */
  expand(row: TreeSelectRow, open: boolean) {
    this.collapsed.update((set) => {
      const next = new Set(set);
      if (open) next.delete(row.node.value);
      else next.add(row.node.value);
      return next;
    });
    if (!open) this.active.set(this.entries().findIndex((entry) => entry.row === row));
  }

  setShowInactive(show: boolean) {
    this.showInactive.set(show);
    this.active.set(Math.min(this.active(), this.entries().length - 1));
  }

  triggerKey(event: KeyboardEvent) {
    if (event.isComposing) return;
    const printable =
      event.key.length === 1 &&
      event.key !== ' ' &&
      !event.ctrlKey &&
      !event.metaKey &&
      !event.altKey;
    // 有搜尋框時，在觸發鈕上打字直接開啟並搜尋。
    if (!this.opened() && printable && this.search()) {
      event.preventDefault();
      this.open(event.key);
      return;
    }
    if (!this.opened() && ['ArrowDown', 'ArrowUp', 'Enter', ' '].includes(event.key)) {
      event.preventDefault();
      this.open();
      return;
    }
    if (this.opened()) this.navigate(event);
  }

  searchKey(event: KeyboardEvent) {
    if (event.isComposing) return;
    // 有字時左右鍵移動游標，不操作樹。
    if (this.query() && ['ArrowLeft', 'ArrowRight'].includes(event.key)) return;
    this.navigate(event);
  }

  private navigate(event: KeyboardEvent) {
    const entries = this.entries();
    const current = entries[this.active()];
    const move = (index: number) => {
      event.preventDefault();
      this.active.set(Math.max(0, Math.min(entries.length - 1, index)));
      this.reveal();
    };
    switch (event.key) {
      case 'ArrowDown':
        return move(this.active() + 1);
      case 'ArrowUp':
        return move(this.active() - 1);
      case 'Home':
        return move(0);
      case 'End':
        return move(entries.length - 1);
      case 'ArrowRight': {
        const row = current?.row;
        if (!row?.children) return;
        event.preventDefault();
        if (this.collapsed().has(row.node.value)) this.expand(row, true);
        else move(this.active() + 1);
        return;
      }
      case 'ArrowLeft': {
        const row = current?.row;
        if (!row) return;
        event.preventDefault();
        if (row.children && !this.collapsed().has(row.node.value)) return this.expand(row, false);
        const parent = entries.findIndex(
          (entry) => entry.row && entry.row.node.value === this.ancestors(row).at(-1),
        );
        if (parent >= 0) move(parent);
        return;
      }
      case 'Enter':
        event.preventDefault();
        return this.choose(this.active());
      case 'Escape':
        event.preventDefault();
        event.stopPropagation();
        this.close();
        this.trigger().nativeElement.focus();
        return;
      case 'Tab':
        if (!event.shiftKey && this.canToggle() && event.target !== this.trigger().nativeElement)
          return;
        this.close();
    }
  }

  filter(query: string) {
    this.query.set(query);
    this.active.set(this.entries().length ? 0 : -1);
    this.popover.reposition();
  }

  dismiss(event: Event) {
    event.preventDefault();
    this.close();
    this.trigger().nativeElement.focus();
  }

  highlight(text: string) {
    const term = this.query().trim();
    const index = term ? text.toLocaleLowerCase().indexOf(term.toLocaleLowerCase()) : -1;
    if (index < 0) return { before: text, hit: '', after: '' };
    const end = index + term.length;
    return { before: text.slice(0, index), hit: text.slice(index, end), after: text.slice(end) };
  }

  private ancestors(row: TreeSelectRow) {
    return row.path.map((_, depth) => this.ancestorAt(this.allRows(), row, depth));
  }
  /** `row` 在第 `depth` 層（0 起算）的祖先；`depth` 等於自己的層數時是自己。 */
  private ancestorAt(rows: readonly TreeSelectRow[], row: TreeSelectRow, depth: number) {
    const index = rows.findIndex((item) => item.node.value === row.node.value);
    if (depth >= row.path.length) return row.node.value;
    for (let i = index - 1; i >= 0; i--) if (rows[i].depth === depth + 1) return rows[i].node.value;
    return row.node.value;
  }
  /** 篩掉部分列後重算下層數，沒有可見下層的節點不顯示展開箭頭。 */
  private recount(rows: TreeSelectRow[]) {
    return rows.map((row, index) => {
      let children = 0;
      for (let i = index + 1; i < rows.length && rows[i].depth > row.depth; i++)
        if (rows[i].depth === row.depth + 1) children++;
      return children === row.children ? row : { ...row, children };
    });
  }
  private reveal() {
    cancelAnimationFrame(this.frame);
    this.frame = requestAnimationFrame(() =>
      document.getElementById(`${this.id}-${this.active()}`)?.scrollIntoView({ block: 'nearest' }),
    );
  }
}
