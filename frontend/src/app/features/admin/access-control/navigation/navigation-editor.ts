import {
  ApplicationRef,
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  computed,
  inject,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { GripVertical } from 'lucide';
import { catalogFeature } from '@core/api/catalog';
import type { NavigationItemDto, NavigationOrderItem } from '@core/api/schema';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { safeMessage } from '@core/errors/safe-errors';
import { featureIcon, type NavigationKind } from '@core/layout/navigation-tree';
import { ThemeService } from '@core/preferences/theme-service';
import { ActionMenu, type MenuAction } from '@shared/ui/action-menu';
import { CompactDialog } from '@shared/ui/compact-dialog';
import { ConfirmDialog } from '@shared/ui/confirm-dialog';
import { DataTable, DataTableRow, TableState } from '@shared/ui/data-table';
import { DialogHeader } from '@shared/ui/dialog-header';
import { EXTRA_ICONS, ICON_NAMES, Icon } from '@shared/ui/icon';
import { MenuPanel } from '@shared/ui/menu-panel';
import { PendingButton } from '@shared/ui/pending-button';
import { Select, type SelectOption } from '@shared/ui/select';
import { StatusBadge } from '@shared/ui/status-badge';
import { AccessControlApi } from '../access-control-api';
import { AccessControlStore } from '../access-control-store';
import { NavigationStore } from '../navigation-store';
import {
  MAX_DEPTH,
  applyWrites,
  canPlace,
  depthOf,
  editorTree,
  level,
  moveWrites,
  shiftId,
  type EditorNode,
  type OrderShift,
  type Placement,
} from './navigation-order';
import { NAVIGATION_KINDS, navigationKindLabel } from './navigation-kinds';

/** 表格的一列：樹以深度優先攤平，`depth` 從 0 起算，`index`／`count` 是在同層的位置。 */
export interface NavigationRow {
  item: NavigationItemDto;
  name: string;
  depth: number;
  parentId: string | null;
  index: number;
  count: number;
  /** 祖先名稱，供報讀「移到哪裡」。 */
  path: string;
  children: number;
}
interface Drag {
  id: string;
  row: HTMLElement;
  pointer: number;
  /** 指標相對於列的未轉換頂端位置；列換位置時保留。 */
  offset: number;
  last: PointerEvent;
  scroll: number;
}
const SCROLL_EDGE = 48;
const SCROLL_SPEED = 12;

/**
 * 選單樹的編輯表：每列一個節點，依層級內縮。位置直接在表上調整：拖曳把手（放在列的上半或下半＝插到它前後，
 * 拖到分群列的中段＝移入該分群），或在把手上按 ↑↓（Home／End）在同層移動、← 移出分群、→ 移入上方的分群；
 * 列的選單有同樣的動作與「移到…」。每次移動立即以 `PUT .../order` 一次送出（全部成功或全部不寫），
 * 失敗時還原畫面並顯示錯誤。新增與修改內容走 `NavigationItemDialog`。
 */
@Component({
  selector: 'nx-navigation-editor',
  imports: [
    ActionMenu,
    CompactDialog,
    ConfirmDialog,
    DataTable,
    DataTableRow,
    DialogHeader,
    Icon,
    MenuPanel,
    PendingButton,
    Select,
    StatusBadge,
    TableState,
  ],
  providers: [{ provide: EXTRA_ICONS, useValue: { grip: GripVertical } }],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrls: ['../../../../shared/ui/management-table-page.scss', './navigation-editor.scss'],
  templateUrl: './navigation-editor.html',
})
export class NavigationEditor {
  readonly store = inject(NavigationStore);
  readonly session = inject(WorkspaceSession);
  private readonly access = inject(AccessControlStore);
  private readonly api = inject(AccessControlApi);
  private readonly app = inject(ApplicationRef);
  private readonly theme = inject(ThemeService);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  readonly create = output<{ kind: NavigationKind; parentId: string | null }>();
  readonly edit = output<NavigationItemDto>();
  private readonly confirmation = viewChild.required(ConfirmDialog);
  private readonly menu = viewChild.required(MenuPanel);
  private readonly moveDialog = viewChild.required<ElementRef<HTMLDialogElement>>('moveDialog');
  readonly additions: MenuAction[] = NAVIGATION_KINDS.map(({ kind, label, icon }) => ({
    id: kind,
    label,
    icon,
  }));
  readonly saving = signal(false);
  /** 刪除中的節點：確認後選單已關閉，由該列的操作鈕轉圈；其他列照常可操作選單（寫入仍由 `busy` 擋）。 */
  readonly deleting = signal<string | null>(null);
  readonly dragging = signal<string | null>(null);
  /** 拖曳中的暫定位置（只改畫面）；放手才送出。 */
  private readonly draft = signal<Placement | null>(null);
  /** 拖到分群列中段時要移入的分群。 */
  readonly dropInto = signal<string | null>(null);
  readonly announcement = signal('');
  private drag: Drag | null = null;
  readonly busy = computed(() => this.saving() || this.session.testing());

  readonly rows = computed<NavigationRow[]>(() => {
    const rows: NavigationRow[] = [];
    const hidden = this.dragging();
    const visit = (nodes: EditorNode[], depth: number, parentId: string | null, trail: string[]) =>
      nodes.forEach((node, index) => {
        const name = this.store.name(node.item);
        rows.push({
          item: node.item,
          name,
          depth,
          parentId,
          index,
          count: nodes.length,
          path: trail.join(' › '),
          children: node.children.length,
        });
        // 拖曳分群時先收起它的子孫：整個子樹跟著移動，也不能放到自己底下。
        if (node.item.id !== hidden)
          visit(node.children, depth + 1, node.item.id, [...trail, name]);
      });
    visit(editorTree(this.store.items(), this.draft()), 0, null, []);
    return rows;
  });
  readonly groupNames = computed(
    () => new Map((this.access.catalog()?.groups ?? []).map((group) => [group.id, group.name])),
  );

  constructor() {
    inject(DestroyRef).onDestroy(() => this.end());
  }

  readonly kindLabel = navigationKindLabel;
  /** 頁面用目錄圖示；外部連結沒設定時用外部連結圖示，分群沒設定時用格狀圖示。 */
  icon(item: NavigationItemDto) {
    if (item.kind === 'page') return featureIcon(catalogFeature(item.featureId ?? '')?.icon);
    if (item.icon && ICON_NAMES.has(item.icon)) return item.icon;
    return item.kind === 'link' ? 'external' : 'apps';
  }
  audience(item: NavigationItemDto) {
    return item.audienceGroupIds.length
      ? item.audienceGroupIds.map((id) => this.groupNames().get(id) || id).join('、')
      : '所有登入者';
  }

  // ── 列的選單（一個面板服務所有列） ──
  private readonly chosen = signal<NavigationRow | null>(null);
  readonly actions = computed<MenuAction[]>(() => {
    const row = this.chosen();
    if (!row) return [];
    const busy = this.busy(),
      items = this.store.items(),
      id = row.item.id;
    const grandParent = row.parentId
      ? (items.find((item) => item.id === row.parentId)?.parentId ?? null)
      : null;
    const above = this.previousGroup(row);
    const group = row.item.kind === 'group';
    return [
      { id: 'edit', label: '編輯…', icon: 'edit', disabled: busy },
      { id: 'up', label: '上移', icon: 'arrow', disabled: busy || row.index === 0 },
      { id: 'down', label: '下移', icon: 'down', disabled: busy || row.index === row.count - 1 },
      { id: 'first', label: '移到同層最前', icon: 'chevron-up', disabled: busy || row.index === 0 },
      {
        id: 'last',
        label: '移到同層最後',
        icon: 'chevron-down',
        disabled: busy || row.index === row.count - 1,
      },
      {
        id: 'outdent',
        label: '移出分群',
        icon: 'left',
        disabled: busy || !row.parentId || !canPlace(items, id, grandParent),
      },
      {
        id: 'indent',
        label: above ? `移入上方分群「${this.store.name(above)}」` : '移入上方分群',
        icon: 'chevron',
        disabled: busy || !above || !canPlace(items, id, above.id),
      },
      { id: 'move', label: '移到…', icon: 'workspace', disabled: busy },
      ...(group
        ? [
            {
              id: 'add',
              label: '在此分群新增…',
              icon: 'plus',
              disabled: busy || depthOf(items, id) >= MAX_DEPTH,
            },
          ]
        : []),
      {
        id: 'delete',
        label: group && row.children ? '刪除（請先清空分群）' : '刪除',
        icon: 'trash',
        danger: true,
        disabled: busy || (group && row.children > 0),
      },
    ];
  });
  openMenu(row: NavigationRow, trigger: HTMLElement) {
    if (this.menu().isOpenFor(trigger)) return this.menu().close();
    this.chosen.set(row);
    this.menu().open(trigger);
  }
  isMenuOpen(trigger: HTMLElement) {
    return this.menu().isOpenFor(trigger);
  }
  act(action: string) {
    const row = this.chosen();
    if (!row || (this.busy() && action !== 'edit')) return;
    const shift = ({ up: -1, down: 1, first: 'first', last: 'last' } as const)[
      action as 'up' | 'down' | 'first' | 'last'
    ];
    if (shift !== undefined) return void this.shift(row, shift, false);
    if (action === 'edit') this.edit.emit(row.item);
    else if (action === 'outdent') void this.outdent(row, false);
    else if (action === 'indent') void this.indent(row, false);
    else if (action === 'move') this.openMove(row);
    else if (action === 'add') this.create.emit({ kind: 'page', parentId: row.item.id });
    else if (action === 'delete') void this.remove(row);
  }
  add(kind: string) {
    this.create.emit({ kind: kind as NavigationKind, parentId: null });
  }

  // ── 鍵盤 ──
  key(event: KeyboardEvent, row: NavigationRow) {
    const shift = (
      { ArrowUp: -1, ArrowDown: 1, Home: 'first', End: 'last' } as Record<string, OrderShift>
    )[event.key];
    if (shift === undefined && event.key !== 'ArrowLeft' && event.key !== 'ArrowRight') return;
    event.preventDefault();
    if (this.busy()) return;
    if (shift !== undefined) void this.shift(row, shift, true);
    else if (event.key === 'ArrowLeft') void this.outdent(row, true);
    else void this.indent(row, true);
  }
  private shift(row: NavigationRow, shift: OrderShift, refocus: boolean) {
    const ids = level(this.store.items(), row.parentId).map((item) => item.id);
    const next = shiftId(ids, row.item.id, shift);
    return this.place(row, row.parentId, next.indexOf(row.item.id), refocus);
  }
  /** 移到上層分群中、緊接在原分群之後。 */
  private outdent(row: NavigationRow, refocus: boolean) {
    const items = this.store.items();
    const parent = items.find((item) => item.id === row.parentId);
    if (!parent) return;
    const target = parent.parentId ?? null;
    if (!canPlace(items, row.item.id, target)) return this.refuse(row, '這裡放不下');
    const index = level(items, target).findIndex((item) => item.id === parent.id) + 1;
    return this.place(row, target, index, refocus);
  }
  /** 移入同層上方最近的分群，放在最後。 */
  private indent(row: NavigationRow, refocus: boolean) {
    const items = this.store.items();
    const above = this.previousGroup(row);
    if (!above || !canPlace(items, row.item.id, above.id))
      return this.refuse(row, '上方沒有可以移入的分群');
    return this.place(row, above.id, level(items, above.id).length, refocus);
  }
  private previousGroup(row: NavigationRow) {
    const siblings = level(this.store.items(), row.parentId);
    return siblings
      .slice(
        0,
        siblings.findIndex((item) => item.id === row.item.id),
      )
      .reverse()
      .find((item) => item.kind === 'group');
  }
  private refuse(row: NavigationRow, reason: string) {
    this.announcement.set(`${row.name}：${reason}`);
  }

  // ── 移到… ──
  readonly moving = signal<{ row: NavigationRow; parentId: string } | null>(null);
  readonly destinations = computed<SelectOption[]>(() => {
    const moving = this.moving();
    if (!moving) return [];
    const items = this.store.items(),
      id = moving.row.item.id;
    const options: SelectOption[] = [
      { value: '', label: '最上層', disabled: !canPlace(items, id, null) },
    ];
    const visit = (nodes: EditorNode[], trail: string[]) => {
      for (const node of nodes) {
        if (node.item.kind !== 'group' || node.item.id === id) continue;
        const path = [...trail, this.store.name(node.item)];
        options.push({
          value: node.item.id,
          label: path.join(' › '),
          disabled: !canPlace(items, id, node.item.id),
        });
        visit(node.children, path);
      }
    };
    visit(editorTree(items), []);
    return options;
  });
  openMove(row: NavigationRow) {
    this.moving.set({ row, parentId: row.parentId ?? '' });
    this.moveDialog().nativeElement.showModal();
  }
  async confirmMove(event: Event) {
    event.preventDefault();
    const moving = this.moving();
    if (!moving) return;
    const parentId = moving.parentId || null;
    this.moveDialog().nativeElement.close();
    if (parentId === moving.row.parentId) return;
    // 移到其他層時排在最後；同層的順序再用拖曳或方向鍵調整。
    const index = level(this.store.items(), parentId).length;
    await this.place(moving.row, parentId, index, false);
  }

  // ── 刪除 ──
  private async remove(row: NavigationRow) {
    const label = this.kindLabel(row.item.kind);
    const confirmed = await this.confirmation().ask({
      title: `刪除${label}？`,
      message: `刪除「${row.path ? row.path + ' › ' : ''}${row.name}」。只移除選單項目，不影響功能授權。`,
      confirm: '刪除' + label,
      danger: true,
    });
    if (!confirmed) return;
    this.saving.set(true);
    this.deleting.set(row.item.id);
    this.store.actionError.set('');
    try {
      await this.api.deleteNavigationItem(row.item.id);
      this.store.replace(this.store.items().filter((item) => item.id !== row.item.id));
      this.announcement.set(`已刪除${label}：${row.name}`);
      this.store.toast.success(`已刪除${label}`, '側欄選單已更新。');
      this.store.changed();
    } catch (error) {
      this.store.actionError.set(safeMessage(error));
    } finally {
      this.saving.set(false);
      this.deleting.set(null);
    }
  }

  // ── 寫入位置 ──
  /** 立即顯示新位置並一次送出；伺服器拒絕時還原，錯誤代碼的提示放在頁面的 Notice。 */
  private async place(
    row: NavigationRow,
    parentId: string | null,
    index: number,
    refocus: boolean,
  ) {
    const before = this.store.items();
    const writes: NavigationOrderItem[] = this.saving()
      ? []
      : moveWrites(before, row.item.id, parentId, index);
    if (!writes.length) {
      this.flip(() => this.draft.set(null));
      return;
    }
    this.flip(() => {
      this.store.replace(applyWrites(before, writes));
      this.draft.set(null);
    });
    if (refocus) this.handle(row.item.id)?.focus();
    const moved = this.rows().find((item) => item.item.id === row.item.id);
    this.announcement.set(
      moved
        ? `${row.name}：${moved.path || '最上層'}第 ${moved.index + 1} 個，共 ${moved.count} 個`
        : '',
    );
    this.store.actionError.set('');
    this.saving.set(true);
    try {
      await this.api.orderNavigationItems({ items: writes });
      this.store.changed();
    } catch (error) {
      this.flip(() => this.store.replace(before));
      this.store.actionError.set(safeMessage(error));
      this.announcement.set('無法儲存位置，已還原');
      if (refocus) this.handle(row.item.id)?.focus();
    } finally {
      this.saving.set(false);
    }
  }

  // ── 拖曳 ──
  grab(event: PointerEvent, row: NavigationRow) {
    if (event.button !== 0 || this.drag || this.busy() || this.store.items().length < 2) return;
    const element = (event.currentTarget as HTMLElement).closest('tr');
    if (!element) return;
    event.preventDefault();
    this.drag = {
      id: row.item.id,
      row: element,
      pointer: event.pointerId,
      offset: event.clientY - element.getBoundingClientRect().top,
      last: event,
      scroll: 0,
    };
    this.flip(() => {
      this.draft.set({ id: row.item.id, parentId: row.parentId, index: row.index });
      this.dragging.set(row.item.id);
    }, row.item.id);
    // 使用視窗監聽器而不是指標捕獲：在 DOM 中移動列會讓捕獲失效。
    window.addEventListener('pointermove', this.pointerMove);
    window.addEventListener('pointerup', this.drop);
    window.addEventListener('pointercancel', this.drop);
  }

  private readonly pointerMove = (event: PointerEvent) => {
    const drag = this.drag;
    if (!drag || event.pointerId !== drag.pointer) return;
    drag.last = event;
    drag.scroll ||= requestAnimationFrame(() => this.autoscroll());
    this.step(drag, event.clientY);
  };

  /**
   * 依指標落在哪一列的哪一段決定暫定位置：上半＝插到該列之前；下半＝之後（展開的分群則是它的第一個）；
   * 分群列的中段＝移入該分群（放手時排到最後）。伺服器不接受的位置（層數、同層重複頁面）直接略過。
   */
  private step(drag: Drag, pointY: number) {
    this.follow(drag, pointY);
    const items = this.store.items();
    for (const row of this.rows()) {
      if (row.item.id === drag.id) continue;
      const box = this.row(row.item.id)?.getBoundingClientRect();
      if (!box || pointY < box.top || pointY > box.bottom) continue;
      const ratio = (pointY - box.top) / (box.height || 1);
      const group = row.item.kind === 'group';
      if (group && ratio > 0.25 && ratio < 0.75) {
        this.dropInto.set(canPlace(items, drag.id, row.item.id) ? row.item.id : null);
        return;
      }
      this.dropInto.set(null);
      const after = ratio >= 0.5;
      const target: Placement =
        after && group && row.children > 0
          ? { id: drag.id, parentId: row.item.id, index: 0 }
          : {
              id: drag.id,
              parentId: row.parentId,
              index: this.indexWithout(row, drag.id) + (after ? 1 : 0),
            };
      const current = this.draft();
      if (
        (current?.parentId === target.parentId && current.index === target.index) ||
        !canPlace(items, drag.id, target.parentId)
      )
        return;
      this.flip(() => this.draft.set(target), drag.id);
      this.follow(drag, pointY);
      return;
    }
  }
  /** 列在同層中、扣掉被拖曳節點之後的位置。 */
  private indexWithout(row: NavigationRow, dragged: string) {
    const siblings = this.rows().filter(
      (item) => item.parentId === row.parentId && item.item.id !== dragged,
    );
    return siblings.findIndex((item) => item.item.id === row.item.id);
  }

  private autoscroll() {
    const drag = this.drag;
    if (!drag) return;
    drag.scroll = 0;
    const scroller = drag.row.closest<HTMLElement>('.ui-table-scroll');
    if (!scroller) return;
    const box = scroller.getBoundingClientRect(),
      pointY = drag.last.clientY;
    const speed =
      pointY < box.top + SCROLL_EDGE
        ? -SCROLL_SPEED
        : pointY > box.bottom - SCROLL_EDGE
          ? SCROLL_SPEED
          : 0;
    const before = scroller.scrollTop;
    scroller.scrollTop += speed;
    if (scroller.scrollTop === before) return;
    this.step(drag, pointY);
    drag.scroll = requestAnimationFrame(() => this.autoscroll());
  }

  private readonly drop = (event: PointerEvent) => {
    const drag = this.drag;
    if (!drag || event.pointerId !== drag.pointer) return;
    const from = drag.row.style.translate || '0 0';
    const into = this.dropInto();
    const draft = this.draft();
    const row = this.rows().find((item) => item.item.id === drag.id);
    this.end();
    if (!this.theme.reducedMotion())
      drag.row.animate([{ translate: from }, { translate: '0 0' }], motion());
    if (!row) return this.draft.set(null);
    if (into) void this.place(row, into, level(this.store.items(), into).length, false);
    else if (draft) {
      // 暫定位置只是畫面；送出時改用真實資料算寫入（`moveWrites` 以不含自己的位置計算）。
      void this.place(row, draft.parentId, draft.index, false);
    }
  };

  private end() {
    const drag = this.drag;
    if (!drag) return;
    cancelAnimationFrame(drag.scroll);
    window.removeEventListener('pointermove', this.pointerMove);
    window.removeEventListener('pointerup', this.drop);
    window.removeEventListener('pointercancel', this.drop);
    drag.row.style.removeProperty('translate');
    this.drag = null;
    this.dropInto.set(null);
    this.dragging.set(null);
  }

  /** 讓被拖曳的列跟著指標走，不論它在表格中的位置如何。 */
  private follow(drag: Drag, pointY: number) {
    drag.row.style.removeProperty('translate');
    drag.row.style.translate = `0 ${pointY - drag.offset - drag.row.getBoundingClientRect().top}px`;
  }

  /** 執行位置變更，並讓其他列從舊位置滑到新位置。 */
  private flip(change: () => void, except?: string) {
    const rows = [...this.host.querySelectorAll<HTMLElement>('tr[data-item]')];
    const before = new Map(rows.map((row) => [row.dataset['item'], row.getBoundingClientRect()]));
    change();
    this.app.tick();
    if (this.theme.reducedMotion()) return;
    for (const row of this.host.querySelectorAll<HTMLElement>('tr[data-item]')) {
      const id = row.dataset['item'],
        old = before.get(id);
      if (!old || id === except) continue;
      const dy = old.top - row.getBoundingClientRect().top;
      if (Math.abs(dy) >= 1)
        row.animate([{ translate: `0 ${dy}px` }, { translate: '0 0' }], {
          ...motion(),
          composite: 'add',
        });
    }
  }

  private row(id: string) {
    return this.host.querySelector<HTMLElement>(`tr[data-item="${CSS.escape(id)}"]`);
  }
  private handle(id: string) {
    return this.host.querySelector<HTMLElement>(`[data-handle="${CSS.escape(id)}"]`);
  }
}

/** 落定移動列的彈簧參數，取自動效權杖。 */
function motion(): KeyframeAnimationOptions {
  const style = getComputedStyle(document.documentElement);
  return {
    duration: parseFloat(style.getPropertyValue('--motion-morph')) || 460,
    easing: style.getPropertyValue('--ease-spring').trim() || 'ease-out',
  };
}
