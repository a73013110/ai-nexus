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
} from '@angular/core';
import { GripVertical } from 'lucide';
import type { AdminFeatureDto } from '../../../core/api/schema';
import { safeMessage } from '../../../core/errors/safe-errors';
import { FEATURE_ICONS, groupFeatures } from '../../../core/layout/feature-groups';
import { ThemeService } from '../../../core/preferences/theme-service';
import { ActionMenu, type MenuAction } from '../../../shared/ui/action-menu';
import { DataTable, DataTableRow } from '../../../shared/ui/data-table';
import { EXTRA_ICONS, Icon } from '../../../shared/ui/icon';
import { AdminApi } from '../admin-api';
import { AdminStore } from '../admin-store';
import { AccessStatus } from './access-status';
import { orderFeatures, reorderWrites, shiftId, type OrderShift } from './feature-order';

interface Drag {
  id: string;
  section: string;
  row: HTMLElement;
  pointer: number;
  /** Pointer position minus the row's untranslated top, kept while the row changes place. */
  offset: number;
  last: PointerEvent;
  scroll: number;
}
const SCROLL_EDGE = 48;
const SCROLL_SPEED = 12;

/**
 * Features are registered by modules; only their name, switch and place are editable. The place is
 * changed on the list itself, within a sidebar group: drag the handle, press ↑↓ (Home/End) on it, or
 * use the row's menu. Each move saves at once through the feature endpoint (see `reorderWrites`).
 */
@Component({
  selector: 'nx-admin-features-tab',
  imports: [Icon, DataTable, DataTableRow, AccessStatus, ActionMenu],
  providers: [{ provide: EXTRA_ICONS, useValue: { grip: GripVertical } }],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrls: ['./access-tabs.scss', './admin-features-tab.scss'],
  templateUrl: './admin-features-tab.html',
})
export class AdminFeaturesTab {
  readonly store = inject(AdminStore);
  private readonly api = inject(AdminApi);
  private readonly app = inject(ApplicationRef);
  private readonly theme = inject(ThemeService);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  readonly edit = output<AdminFeatureDto | undefined>();
  readonly featureIcons = FEATURE_ICONS;
  readonly features = computed(() => this.store.catalog()?.features ?? []);
  /** Orders saved, or being saved, ahead of the catalog read that confirms them. */
  private readonly pending = linkedSignal<readonly AdminFeatureDto[], Record<string, number>>({
    source: this.features,
    computation: () => ({}),
  });
  /** One group's order while its row is dragged; saved on drop. */
  private readonly draft = signal<{ section: string; ids: string[] } | null>(null);
  private readonly ordered = computed(() =>
    groupFeatures(orderFeatures(this.features(), this.pending())),
  );
  readonly sections = computed(() => {
    const draft = this.draft();
    return this.ordered().map((section) =>
      section.id === draft?.section
        ? {
            ...section,
            features: draft.ids.map((id) => section.features.find((x) => x.id === id)!),
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
    ];
  }
  menu(action: string, section: string, id: string) {
    const shift = ({ up: -1, down: 1, first: 'first', last: 'last' } as const)[
      action as 'up' | 'down' | 'first' | 'last'
    ];
    if (shift !== undefined) void this.shift(section, id, shift, false);
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

  /** Shows the new order at once, writes it, and puts the old order back when a write fails. */
  private async save(section: string, id: string, ids: string[], refocus: boolean) {
    const current = this.ordered().find((x) => x.id === section)?.features ?? [];
    const writes = this.saving() ? [] : reorderWrites(current, id, ids);
    if (!writes.length) {
      this.flip(() => this.draft.set(null));
      return;
    }
    const before = this.pending();
    this.flip(() => {
      this.pending.set({
        ...before,
        ...Object.fromEntries(writes.map((x) => [x.id, x.sortOrder])),
      });
      this.draft.set(null);
    });
    if (refocus) this.handle(id)?.focus();
    const name = current.find((x) => x.id === id)?.name ?? '';
    const group = this.ordered().find((x) => x.id === section);
    this.announcement.set(
      `${name}：${group?.name ?? ''}第 ${ids.indexOf(id) + 1} 個，共 ${ids.length} 個`,
    );
    this.store.actionError.set('');
    this.saving.set(true);
    const written: { id: string; sortOrder: number }[] = [];
    try {
      for (const write of writes) {
        const feature = current.find((x) => x.id === write.id)!;
        await this.api.feature(write.id, {
          name: feature.name,
          enabled: feature.enabled,
          sortOrder: write.sortOrder,
        });
        written.push({ id: write.id, sortOrder: feature.sortOrder });
      }
      this.store.reordered();
    } catch (error) {
      // Put back what was already written, so the group never stays half moved.
      let restored = true;
      for (const write of written.reverse()) {
        const feature = current.find((x) => x.id === write.id)!;
        try {
          await this.api.feature(write.id, {
            name: feature.name,
            enabled: feature.enabled,
            sortOrder: write.sortOrder,
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
    // Window listeners, not pointer capture: moving the row in the DOM drops capture.
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

  /** Swaps with a row of the same group once the pointer passes that row's middle. */
  private step(drag: Drag, y: number) {
    this.follow(drag, y);
    const ids = this.draft()?.ids ?? [];
    const from = ids.indexOf(drag.id);
    for (const [index, id] of ids.entries()) {
      if (id === drag.id) continue;
      const box = this.row(id)?.getBoundingClientRect();
      if (!box || y < box.top || y > box.bottom) continue;
      const middle = box.top + box.height / 2;
      if ((index > from && y > middle) || (index < from && y < middle)) {
        const next = shiftId(ids, drag.id, index - from);
        this.flip(() => this.draft.set({ section: drag.section, ids: next }), drag.id);
        this.follow(drag, y);
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
      y = drag.last.clientY;
    const speed =
      y < box.top + SCROLL_EDGE ? -SCROLL_SPEED : y > box.bottom - SCROLL_EDGE ? SCROLL_SPEED : 0;
    const before = scroller.scrollTop;
    scroller.scrollTop += speed;
    if (scroller.scrollTop === before) return;
    this.step(drag, y);
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

  /** Keeps the dragged row under the pointer, wherever its place in the table now is. */
  private follow(drag: Drag, y: number) {
    drag.row.style.removeProperty('translate');
    drag.row.style.translate = `0 ${y - drag.offset - drag.row.getBoundingClientRect().top}px`;
  }

  /** Runs an order change and lets the other rows glide from their old places to the new ones. */
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
    return (this.sections().find((x) => x.id === section)?.features ?? []).map((x) => x.id);
  }
  private row(id: string) {
    return this.host.querySelector<HTMLElement>(`tr[data-feature="${CSS.escape(id)}"]`);
  }
  private handle(id: string) {
    return this.host.querySelector<HTMLElement>(`[data-handle="${CSS.escape(id)}"]`);
  }
}

/** The spring that settles a moved row, read from the motion tokens. */
function motion(): KeyframeAnimationOptions {
  const style = getComputedStyle(document.documentElement);
  return {
    duration: parseFloat(style.getPropertyValue('--motion-morph')) || 460,
    easing: style.getPropertyValue('--ease-spring').trim() || 'ease-out',
  };
}
