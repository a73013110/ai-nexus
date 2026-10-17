import {
  ApplicationRef,
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  computed,
  inject,
  linkedSignal,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { GripVertical } from 'lucide';
import type { AdminFeatureDto, NavigationGroupDto } from '@core/api/schema';
import { safeMessage } from '@core/errors/safe-errors';
import {
  featureIcon,
  navigationSections,
  navigationTree,
  type NavigationSection,
} from '@core/layout/navigation-tree';
import { ThemeService } from '@core/preferences/theme-service';
import { ActionMenu, type MenuAction } from '@shared/ui/action-menu';
import { ConfirmDialog } from '@shared/ui/confirm-dialog';
import { CountBadge } from '@shared/ui/count-badge';
import { DataTable, DataTableRow } from '@shared/ui/data-table';
import { EXTRA_ICONS, Icon } from '@shared/ui/icon';
import { AdministrationApi } from '../administration-api';
import { AdministrationStore } from '../administration-store';
import { AccessStatus } from './access-status';
import { orderFeatures, reorderWrites, shiftId, type OrderShift } from './feature-order';

interface Drag {
  id: string;
  section: string;
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
 * 功能由模組註冊；名稱、開關、圖示、選單群組與位置都可以編輯。
 * 列表順序跟著側邊欄：最上層功能，接著是每個選單群組（包括空群組），群組的子群組排在其後。
 * 群組內的位置直接在列表上調整：拖曳把手，或在把手上按 ↑↓（Home/End），也可以用列的選單。
 * 每次移動都會立即透過功能端點儲存（見 `reorderWrites`）。移到其他群組與編輯群組則走編輯對話框；
 * 群組自己的選單可以在同層群組之間移動它，或刪除它。
 */
@Component({
  selector: 'nx-admin-features-tab',
  imports: [Icon, DataTable, DataTableRow, AccessStatus, ActionMenu, CountBadge, ConfirmDialog],
  providers: [{ provide: EXTRA_ICONS, useValue: { grip: GripVertical } }],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrls: ['../access-tabs.scss', './admin-features-tab.scss'],
  templateUrl: './admin-features-tab.html',
})
export class AdminFeaturesTab {
  readonly store = inject(AdministrationStore);
  private readonly api = inject(AdministrationApi);
  private readonly app = inject(ApplicationRef);
  private readonly theme = inject(ThemeService);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  readonly edit = output<AdminFeatureDto | undefined>();
  /** 要編輯的選單群組；`undefined` 代表新增。 */
  readonly editGroup = output<NavigationGroupDto | undefined>();
  private readonly confirmation = viewChild.required(ConfirmDialog);
  readonly icon = featureIcon;
  readonly features = computed(() => this.store.catalog()?.features ?? []);
  /** 已儲存或正在儲存的順序，早於確認它們的目錄讀取。 */
  private readonly pending = linkedSignal<readonly AdminFeatureDto[], Record<string, number>>({
    source: this.features,
    computation: () => ({}),
  });
  /** 拖曳列時某群組的順序；放手時才儲存。 */
  private readonly draft = signal<{ section: string; ids: string[] } | null>(null);
  private readonly ordered = computed<NavigationSection<AdminFeatureDto>[]>(() =>
    navigationSections(
      navigationTree(
        orderFeatures(this.features(), this.pending()),
        this.store.navigationGroups(),
        {
          keepEmpty: true,
        },
      ),
      { topName: '最上層（不分群）' },
    ),
  );
  readonly sections = computed(() => {
    const draft = this.draft();
    return this.ordered().map((section) =>
      section.id === draft?.section
        ? {
            ...section,
            features: draft.ids.map((id) => section.features.find((item) => item.id === id)!),
          }
        : section,
    );
  });
  readonly saving = signal(false);
  readonly dragging = signal<string | null>(null);
  readonly announcement = signal('');
  private drag: Drag | null = null;

  constructor() {
    inject(DestroyRef).onDestroy(() => this.end());
  }

  moves(index: number, count: number): MenuAction[] {
    const busy = this.saving();
    return [
      { id: 'up', label: '上移', icon: 'arrow', disabled: busy || index === 0 },
      { id: 'down', label: '下移', icon: 'down', disabled: busy || index === count - 1 },
      { id: 'first', label: '移到分群最前', icon: 'chevron-up', disabled: busy || index === 0 },
      {
        id: 'last',
        label: '移到分群最後',
        icon: 'chevron-down',
        disabled: busy || index === count - 1,
      },
      { id: 'edit', label: '移到其他分群或編輯…', icon: 'edit', disabled: busy },
    ];
  }
  menu(action: string, section: string, feature: AdminFeatureDto) {
    if (action === 'edit') return this.edit.emit(feature);
    const shift = ({ up: -1, down: 1, first: 'first', last: 'last' } as const)[
      action as 'up' | 'down' | 'first' | 'last'
    ];
    if (shift !== undefined) void this.shift(section, feature.id, shift, false);
  }

  /** 與 `id` 同層（同一上層）的選單群組，依選單順序排列。 */
  private siblings(id: string) {
    const groups = this.store.navigationGroups();
    const parent = groups.find((item) => item.id === id)?.parentId ?? null;
    return orderFeatures(groups.filter((item) => (item.parentId ?? null) === parent));
  }
  groupMoves(id: string): MenuAction[] {
    const busy = this.saving(),
      ids = this.siblings(id).map((item) => item.id),
      index = ids.indexOf(id);
    return [
      { id: 'edit', label: '重新命名或移動…', icon: 'edit', disabled: busy },
      { id: 'up', label: '分群上移', icon: 'arrow', disabled: busy || index <= 0 },
      { id: 'down', label: '分群下移', icon: 'down', disabled: busy || index === ids.length - 1 },
      { id: 'delete', label: '刪除分群', icon: 'trash', danger: true, disabled: busy },
    ];
  }
  async groupAction(action: string, section: NavigationSection<AdminFeatureDto>) {
    const group = this.store.navigationGroups().find((item) => item.id === section.id);
    if (!group || this.saving()) return;
    if (action === 'edit') return this.editGroup.emit(group);
    this.store.actionError.set('');
    if (action === 'delete') {
      const confirmed = await this.confirmation().ask({
        title: '刪除選單分群？',
        message: `刪除「${section.path}」。分群內仍有功能或子分群時無法刪除，請先移出。`,
        confirm: '刪除分群',
        danger: true,
      });
      if (!confirmed) return;
      return this.write(async () => {
        await this.api.deleteNavigationGroup(group.id);
        this.announcement.set(`已刪除選單分群：${section.path}`);
      });
    }
    const shift = ({ up: -1, down: 1 } as const)[action as 'up' | 'down'];
    if (shift === undefined) return;
    const siblings = this.siblings(group.id);
    const writes = reorderWrites(
      siblings,
      group.id,
      shiftId(
        siblings.map((item) => item.id),
        group.id,
        shift,
      ),
    );
    await this.write(async () => {
      for (const write of writes) {
        const value = siblings.find((item) => item.id === write.id)!;
        await this.api.updateNavigationGroup(value.id, {
          name: value.name,
          parentId: value.parentId,
          sortOrder: write.sortOrder,
        });
      }
      this.announcement.set(`${section.path}：已${shift < 0 ? '上移' : '下移'}`);
    });
  }

  /** 一次只處理一個群組變更；不論成功與否，目錄與側邊欄都會重新讀取。 */
  private async write(change: () => Promise<void>) {
    this.saving.set(true);
    try {
      await change();
    } catch (error) {
      this.store.actionError.set(safeMessage(error));
    } finally {
      this.saving.set(false);
      this.store.reordered();
    }
  }
  key(event: KeyboardEvent, section: string, id: string) {
    const shift = (
      { ArrowUp: -1, ArrowDown: 1, Home: 'first', End: 'last' } as Record<string, OrderShift>
    )[event.key];
    if (shift === undefined) return;
    event.preventDefault();
    void this.shift(section, id, shift, true);
  }

  private shift(section: string, id: string, shift: OrderShift, refocus: boolean) {
    const ids = this.ids(section);
    return this.save(section, id, shiftId(ids, id, shift), refocus);
  }

  /** 立即顯示新順序並寫入；寫入失敗時還原舊順序。 */
  private async save(section: string, id: string, ids: string[], refocus: boolean) {
    const current = this.ordered().find((item) => item.id === section)?.features ?? [];
    const writes = this.saving() ? [] : reorderWrites(current, id, ids);
    if (!writes.length) {
      this.flip(() => this.draft.set(null));
      return;
    }
    const before = this.pending();
    this.flip(() => {
      this.pending.set({
        ...before,
        ...Object.fromEntries(writes.map((item) => [item.id, item.sortOrder])),
      });
      this.draft.set(null);
    });
    if (refocus) this.handle(id)?.focus();
    const name = current.find((item) => item.id === id)?.name ?? '';
    const group = this.ordered().find((item) => item.id === section);
    this.announcement.set(
      `${name}：${group?.path ?? ''}第 ${ids.indexOf(id) + 1} 個，共 ${ids.length} 個`,
    );
    this.store.actionError.set('');
    this.saving.set(true);
    const written: { id: string; sortOrder: number }[] = [];
    try {
      for (const write of writes) {
        const feature = current.find((item) => item.id === write.id)!;
        await this.api.feature(write.id, {
          name: feature.name,
          enabled: feature.enabled,
          sortOrder: write.sortOrder,
          groupId: feature.groupId,
          icon: feature.icon,
        });
        written.push({ id: write.id, sortOrder: feature.sortOrder });
      }
      this.store.reordered();
    } catch (error) {
      // 把已寫入的部分還原，群組才不會停在移動一半的狀態。
      let restored = true;
      for (const write of written.reverse()) {
        const feature = current.find((item) => item.id === write.id)!;
        try {
          await this.api.feature(write.id, {
            name: feature.name,
            enabled: feature.enabled,
            sortOrder: write.sortOrder,
            groupId: feature.groupId,
            icon: feature.icon,
          });
        } catch {
          restored = false;
        }
      }
      this.flip(() => this.pending.set(before));
      this.store.actionError.set(safeMessage(error));
      this.announcement.set('無法儲存順序，已還原');
      if (!restored) this.store.reordered();
    } finally {
      this.saving.set(false);
    }
  }

  grab(event: PointerEvent, section: string, id: string) {
    if (event.button !== 0 || this.drag || this.saving() || this.ids(section).length < 2) return;
    const row = (event.currentTarget as HTMLElement).closest('tr');
    if (!row) return;
    event.preventDefault();
    this.drag = {
      id,
      section,
      row,
      pointer: event.pointerId,
      offset: event.clientY - row.getBoundingClientRect().top,
      last: event,
      scroll: 0,
    };
    this.draft.set({ section, ids: this.ids(section) });
    this.dragging.set(id);
    // 使用視窗監聽器而不是指標捕獲：在 DOM 中移動列會讓捕獲失效。
    window.addEventListener('pointermove', this.move);
    window.addEventListener('pointerup', this.drop);
    window.addEventListener('pointercancel', this.drop);
  }

  private readonly move = (event: PointerEvent) => {
    const drag = this.drag;
    if (!drag || event.pointerId !== drag.pointer) return;
    drag.last = event;
    drag.scroll ||= requestAnimationFrame(() => this.autoscroll());
    this.step(drag, event.clientY);
  };

  /** 指標越過同群組某列的中線時，與該列交換位置。 */
  private step(drag: Drag, pointY: number) {
    this.follow(drag, pointY);
    const ids = this.draft()?.ids ?? [];
    const from = ids.indexOf(drag.id);
    for (const [index, id] of ids.entries()) {
      if (id === drag.id) continue;
      const box = this.row(id)?.getBoundingClientRect();
      if (!box || pointY < box.top || pointY > box.bottom) continue;
      const middle = box.top + box.height / 2;
      if ((index > from && pointY > middle) || (index < from && pointY < middle)) {
        const next = shiftId(ids, drag.id, index - from);
        this.flip(() => this.draft.set({ section: drag.section, ids: next }), drag.id);
        this.follow(drag, pointY);
      }
      return;
    }
  }

  private autoscroll() {
    const drag = this.drag;
    if (!drag) return;
    drag.scroll = 0;
    const scroller = drag.row.closest<HTMLElement>('.ui-table-scroll');
    if (!scroller) return;
    const box = scroller.getBoundingClientRect(),
      pointY2 = drag.last.clientY;
    const speed =
      pointY2 < box.top + SCROLL_EDGE
        ? -SCROLL_SPEED
        : pointY2 > box.bottom - SCROLL_EDGE
          ? SCROLL_SPEED
          : 0;
    const before = scroller.scrollTop;
    scroller.scrollTop += speed;
    if (scroller.scrollTop === before) return;
    this.step(drag, pointY2);
    drag.scroll = requestAnimationFrame(() => this.autoscroll());
  }

  private readonly drop = (event: PointerEvent) => {
    const drag = this.drag;
    if (!drag || event.pointerId !== drag.pointer) return;
    const from = drag.row.style.translate || '0 0';
    this.end();
    if (!this.theme.reducedMotion())
      drag.row.animate([{ translate: from }, { translate: '0 0' }], motion());
    void this.save(drag.section, drag.id, this.draft()?.ids ?? this.ids(drag.section), false);
  };

  private end() {
    const drag = this.drag;
    if (!drag) return;
    cancelAnimationFrame(drag.scroll);
    window.removeEventListener('pointermove', this.move);
    window.removeEventListener('pointerup', this.drop);
    window.removeEventListener('pointercancel', this.drop);
    drag.row.style.removeProperty('translate');
    this.drag = null;
    this.dragging.set(null);
  }

  /** 讓被拖曳的列跟著指標走，不論它在表格中的位置如何。 */
  private follow(drag: Drag, pointY3: number) {
    drag.row.style.removeProperty('translate');
    drag.row.style.translate = `0 ${pointY3 - drag.offset - drag.row.getBoundingClientRect().top}px`;
  }

  /** 執行順序變更，並讓其他列從舊位置滑到新位置。 */
  private flip(change: () => void, except?: string) {
    const rows = [...this.host.querySelectorAll<HTMLElement>('tr[data-feature]')];
    const before = new Map(
      rows.map((row) => [row.dataset['feature'], row.getBoundingClientRect()]),
    );
    change();
    this.app.tick();
    if (this.theme.reducedMotion()) return;
    for (const row of this.host.querySelectorAll<HTMLElement>('tr[data-feature]')) {
      const id = row.dataset['feature'],
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

  private ids(section: string) {
    return (this.sections().find((item) => item.id === section)?.features ?? []).map(
      (item) => item.id,
    );
  }
  private row(id: string) {
    return this.host.querySelector<HTMLElement>(`tr[data-feature="${CSS.escape(id)}"]`);
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
