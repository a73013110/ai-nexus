import { Injectable, computed, inject, signal } from '@angular/core';
import { apiResource } from '@core/api/api-resource';
import type { UnitDto, UnitMemberDto } from '@core/api/schema';
import { STALE_WRITE_MESSAGE } from '@core/errors/safe-errors';
import { action } from '@shared/browser/action';
import { ToastService } from '@shared/ui/toast-service';
import { OrganizationApi } from './organization-api';
import { headsOf, moved, unitRows } from './unit-tree';

/**
 * 組織頁提供（`providers`）的狀態：全部單位（平面清單）、選取的單位與它的直屬成員。
 * 寫入成功後重讀伺服器的值；成員讀取以單位 ID 為鍵，切換單位時不會短暫顯示上一個單位的成員。
 */
@Injectable()
export class OrganizationStore {
  private readonly api = inject(OrganizationApi);
  readonly toast = inject(ToastService);
  private readonly unitsRead = apiResource({
    feature: 'organization',
    loader: () => this.api.units(),
  });
  readonly units = computed(() => this.unitsRead.value() ?? []);
  readonly ready = computed(() => this.unitsRead.value() !== undefined);
  readonly loading = this.unitsRead.loading;
  /** 單位清單重讀中（寫入後、重試）：樹淡化。 */
  readonly refreshing = this.unitsRead.refreshing;
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
  /** 帶領選取單位的首長單位：有的話主管由它們擔任，成員不能勾選主管。 */
  readonly selectedHeads = computed(() => {
    const id = this.selectedId();
    return id ? headsOf(this.units(), id) : [];
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
  /** 成員重讀中（寫入後、重試）：清單淡化。 */
  readonly membersRefreshing = this.membersRead.refreshing;
  private readonly writing = action();
  /** 寫入中或重讀中：重讀完之前列上的版本還是舊的，此時送出修改或刪除只會版本衝突。 */
  readonly busy = computed(() => this.writing.pending() || this.unitsRead.refreshing());
  /**
   * 正在寫入的對象（例：`unit:<id>`、`primary:<userId>`）：只有那個控制項轉圈，
   * 其他寫入仍由 `busy` 擋下。
   */
  readonly working = this.writing.key;
  readonly error = computed(() => this.writing.error() || this.unitsRead.error());
  /**
   * 成員的勾選要等寫入後的重讀回來才解除：重讀前成員資料仍是舊的，此時切換另一個勾選會帶著舊值
   * 覆寫剛存好的那一個；剛寫入的那一項在重讀期間也繼續轉圈，方框不會先跳回舊狀態。
   */
  readonly membersLocked = computed(() => this.busy() || this.membersRead.refreshing());
  private readonly settling = signal('');
  /** `key` 的成員寫入正在送出，或已送出、正等重讀確認。 */
  memberPending(key: string) {
    return (
      (this.writing.pending() && this.working() === key) ||
      (this.membersRead.refreshing() && this.settling() === key)
    );
  }

  load() {
    this.writing.error.set('');
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
    this.writing.error.set('');
    this.unitsRead.reload();
  }
  /** 送出的版本已過期：由頁面的 Notice 說明，單位重讀成最新。 */
  stale() {
    this.writing.fail(STALE_WRITE_MESSAGE);
    this.unitsRead.reload();
  }

  move(unit: UnitDto, delta: -1 | 1) {
    const order = moved(this.units(), unit.id, delta);
    if (!order) return Promise.resolve(false);
    return this.run(
      () => this.api.orderUnits(order),
      '',
      () => this.unitsRead.reload(),
      `unit:${unit.id}`,
    );
  }
  remove(unit: UnitDto) {
    return this.run(
      () => this.api.deleteUnit(unit.id, unit.rowVersion),
      `已刪除「${unit.name}」`,
      () => {
        if (this.selectedId() === unit.id) this.selectedId.set(null);
        this.unitsRead.reload();
      },
      `unit:${unit.id}`,
    );
  }
  /** 修改成員的主要所屬或主管；`key` 標出觸發寫入的控制項（例：`primary:<userId>`），讓它轉圈。加入成員見 `MemberDialog`。 */
  saveMember(userId: string, isPrimary: boolean, isManager: boolean, key = '') {
    const unit = this.selected();
    if (!unit) return Promise.resolve(false);
    return this.run(
      () => this.api.saveMember(unit.id, userId, { isPrimary, isManager }),
      '',
      () => this.membersChanged(),
      key,
    );
  }
  removeMember(member: UnitMemberDto) {
    const unit = this.selected();
    if (!unit) return Promise.resolve(false);
    return this.run(
      () => this.api.removeMember(unit.id, member.userId),
      `已將 ${member.displayName} 移出「${unit.name}」`,
      () => this.membersChanged(),
      `remove:${member.userId}`,
    );
  }
  /** 成員已寫入（含對話框）：重讀成員與單位，成員數跟著更新。 */
  membersChanged() {
    this.membersRead.reload();
    this.unitsRead.reload();
  }

  /** 寫入中或重讀中略過；失敗時重讀（例如期間有人改了同一層），讓畫面回到伺服器的狀態。 */
  private async run(write: () => Promise<unknown>, done: string, after: () => void, key: string) {
    if (this.busy()) return false;
    this.settling.set('');
    const outcome = await this.writing.run(write, key);
    if (outcome.ok) {
      if (done) this.toast.success(done);
      this.settling.set(key);
      after();
    } else if (outcome.error) this.unitsRead.reload();
    return outcome.ok;
  }
}
