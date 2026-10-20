import { Injectable, computed, inject, signal } from '@angular/core';
import { apiResource } from '@core/api/api-resource';
import type { UnitDto, UnitMemberDto } from '@core/api/schema';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { safeMessage } from '@core/errors/safe-errors';
import { ViewScope } from '@shared/browser/view-scope';
import { ToastService } from '@shared/ui/toast-service';
import { OrganizationApi } from './organization-api';
import { moved, unitRows } from './unit-tree';

/**
 * 組織頁提供（`providers`）的狀態：全部單位（平面清單）、選取的單位與它的直屬成員。
 * 寫入成功後重讀伺服器的值；成員讀取以單位 ID 為鍵，切換單位時不會短暫顯示上一個單位的成員。
 */
@Injectable()
export class OrganizationStore {
  private readonly api = inject(OrganizationApi);
  private readonly scope = inject(ViewScope);
  private readonly session = inject(WorkspaceSession);
  readonly toast = inject(ToastService);
  private readonly unitsRead = apiResource({
    feature: 'organization',
    loader: () => this.api.units(),
  });
  readonly units = computed(() => this.unitsRead.value() ?? []);
  readonly ready = computed(() => this.unitsRead.value() !== undefined);
  readonly loading = this.unitsRead.loading;
  readonly collapsed = signal<ReadonlySet<string>>(new Set());
  readonly rows = computed(() => unitRows(this.units(), this.collapsed()));
  readonly selectedId = signal<string | null>(null);
  /** 選取的單位；被別人刪除後自動變成未選取。 */
  readonly selected = computed(() => {
    const id = this.selectedId();
    return this.units().find((unit) => unit.id === id) ?? null;
  });
  readonly selectedRow = computed(() => {
    const id = this.selectedId();
    return unitRows(this.units()).find((row) => row.unit.id === id) ?? null;
  });
  private readonly membersRead = apiResource({
    feature: 'organization',
    params: () => this.selected()?.id,
    loader: async (id) => ({ id, members: await this.api.members(id) }),
  });
  readonly members = computed<UnitMemberDto[] | null>(() => {
    const loaded = this.membersRead.value();
    return loaded && loaded.id === this.selected()?.id ? loaded.members : null;
  });
  readonly membersLoading = computed(() => !!this.selected() && this.members() === null);
  readonly membersError = this.membersRead.error;
  readonly busy = signal(false);
  readonly actionError = signal('');
  readonly error = computed(() => this.actionError() || this.unitsRead.error());
  readonly locked = computed(() => this.busy() || this.session.testing());

  load() {
    this.actionError.set('');
    this.unitsRead.reload();
    this.membersRead.reload();
  }
  select(unit: UnitDto | null) {
    this.selectedId.set(unit?.id ?? null);
  }
  toggle(id: string) {
    this.collapsed.update((current) => {
      const next = new Set(current);
      if (!next.delete(id)) next.add(id);
      return next;
    });
  }
  /** 單位已寫入（對話框）：重讀清單，成員數跟著更新。 */
  changed() {
    this.unitsRead.reload();
  }

  move(unit: UnitDto, delta: -1 | 1) {
    const order = moved(this.units(), unit.id, delta);
    if (!order) return Promise.resolve(false);
    return this.run(
      () => this.api.orderUnits(order),
      '',
      () => this.unitsRead.reload(),
    );
  }
  remove(unit: UnitDto) {
    return this.run(
      () => this.api.deleteUnit(unit.id),
      `已刪除「${unit.name}」`,
      () => {
        if (this.selectedId() === unit.id) this.selectedId.set(null);
        this.unitsRead.reload();
      },
    );
  }
  saveMember(userId: string, isPrimary: boolean, isManager: boolean, added = false) {
    const unit = this.selected();
    if (!unit) return Promise.resolve(false);
    return this.run(
      () => this.api.saveMember(unit.id, userId, { isPrimary, isManager }),
      added ? `已加入「${unit.name}」` : '',
      () => this.memberChanged(),
    );
  }
  removeMember(member: UnitMemberDto) {
    const unit = this.selected();
    if (!unit) return Promise.resolve(false);
    return this.run(
      () => this.api.removeMember(unit.id, member.userId),
      `已將 ${member.displayName} 移出「${unit.name}」`,
      () => this.memberChanged(),
    );
  }
  private memberChanged() {
    this.membersRead.reload();
    this.unitsRead.reload();
  }

  /** 寫入共用的忙碌、錯誤與提示；離開頁面或換帳號後才回來的結果不再套用。 */
  private async run(write: () => Promise<unknown>, done: string, after: () => void) {
    if (this.locked()) return false;
    const alive = this.scope.guard();
    this.busy.set(true);
    this.actionError.set('');
    try {
      await write();
      if (!alive()) return false;
      if (done) this.toast.success(done);
      after();
      return true;
    } catch (error) {
      if (!alive()) return false;
      this.actionError.set(safeMessage(error));
      // 例如期間有人改了同一層：重讀，讓畫面回到伺服器的狀態。
      this.unitsRead.reload();
      return false;
    } finally {
      if (alive()) this.busy.set(false);
    }
  }
}
