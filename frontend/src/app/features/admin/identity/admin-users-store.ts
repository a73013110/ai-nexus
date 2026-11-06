import { Injectable, computed, inject, linkedSignal } from '@angular/core';
import { apiResource } from '@core/api/api-resource';
import type { AdminUserDto } from '@core/api/schema';
import { STALE_WRITE_MESSAGE } from '@core/errors/safe-errors';
import { optionTags } from '@shared/ui/option-tags';
import { queryState } from '@shared/ui/query-state';
import type { SelectOption } from '@shared/ui/select';
import { endpointTable } from '@shared/ui/table-model';
import { AccessControlStore } from '@features/admin/access-control/access-control-store';
import { IdentityApi } from './identity-api';
import { userFilters, usersQueryOptions } from './users/users-query';

/**
 * 使用者頁的使用者列表（查詢條件與分頁）。角色與群組名稱來自同頁提供的 `AccessControlStore`，
 * 所以列表在目錄讀到後才讀，名稱不會先顯示成 ID。
 */
@Injectable()
export class AdminUsersStore {
  private readonly api = inject(IdentityApi);
  readonly access = inject(AccessControlStore);
  private readonly ready = computed(() => !!this.access.catalog());
  /** 查詢條件（狀態、搜尋、角色）；已套用的寫進網址，角色 chip 的名稱取自存取目錄。 */
  readonly query = queryState(
    usersQueryOptions((id) => this.access.catalog()?.roles.find((role) => role.id === id)?.name),
  );
  /** 角色條件的選項：停用的角色也能篩（看還掛著它的人），以標籤標示。 */
  readonly roleOptions = computed<SelectOption[]>(() =>
    (this.access.catalog()?.roles ?? []).map((role) => ({
      value: role.id,
      label: role.name,
      description: role.id,
      tags: optionTags(!role.enabled && '停用'),
    })),
  );
  /**
   * 使用者列表：伺服器固定每頁 100 筆（`ListAdminUsers`），順序是手動排序（沒有欄位排序與篩選）。
   * 目錄讀到前不讀；每次套用條件都是新參考，模型回到第一頁，同一頁重讀（排序寫入後、帳號異動後）不改 revision，表格保留捲動位置。
   */
  readonly table = endpointTable({
    feature: 'identity.users',
    params: computed(() => (this.ready() ? userFilters(this.query.applied()) : undefined)),
    load: async (filters, page, signal) => {
      const data = await this.api.users(filters, page.offset, signal);
      return { ...data, items: data.users, complete: false };
    },
    columns: [],
    pageSize: 100,
    pageSizes: [100],
    unit: '人',
  });
  /** 顯示中的頁；拖曳排序時先換成本頁換序後的結果，下一次讀到就換回伺服器的。 */
  readonly users = linkedSignal(() => this.table.data() ?? null);
  /** 帳號待辦不需要角色名稱，不等目錄；帳號有異動時跟著列表重讀。 */
  readonly reviewsRead = apiResource({
    feature: 'identity.users',
    loader: () => this.api.accountReviews(),
  });
  readonly reviews = computed(() => this.reviewsRead.value() ?? null);
  private readonly reviewsById = computed(
    () => new Map((this.reviews()?.users ?? []).map((user) => [user.id, user.items])),
  );
  readonly loading = this.access.loading;
  /**
   * 表格的讀取狀態：重讀，以及列表還在等目錄（目錄讀到前列表不會開始讀）。
   * 沒有列時表格畫骨架，有列時淡化。
   */
  readonly loadingUsers = computed(() => this.table.refreshing() || this.access.loading());
  /** 頁面上的寫入（排序、列上的刪除與從 AD 更新）與存取目錄共用，失敗都顯示在頁首的 Notice。 */
  readonly writing = this.access.writing;
  readonly toast = this.access.toast;
  /** 使用者列表沒有資料時讀取失敗由表格狀態顯示（附重試），頁首只提示已有資料時的失敗。 */
  readonly error = computed(
    () => this.access.error() || (this.users()?.users.length ? this.table.error() : ''),
  );

  load() {
    this.access.load();
    this.table.reload();
    this.reviewsRead.reload();
  }
  /** 這位使用者的帳號待辦；沒有時為空陣列。 */
  reviewsOf(id: string) {
    return this.reviewsById().get(id) ?? [];
  }
  /** 列表只找這個帳號（帳號待辦的捷徑）：其他條件回到預設，人才不會被狀態或角色濾掉。 */
  findAccount(account: string) {
    this.query.commit({ ...this.query.defaults(), search: account });
  }
  /** 重新讀取顯示中的使用者頁面。 */
  readUsers() {
    this.table.reload();
  }
  /** 帳號或 AD 資料已寫入：重新讀取顯示中的使用者與帳號待辦；`first` 時回到第一頁（例：刪除後）。 */
  changed(first = false) {
    if (first && this.table.pageIndex()) this.table.goTo(0);
    else this.readUsers();
    this.reviewsRead.reload();
  }
  /** 送出的版本已過期：由頁面的 Notice 說明，顯示中的使用者重讀成最新。 */
  stale() {
    this.writing.fail(STALE_WRITE_MESSAGE);
    this.changed();
  }
  /** 授權已改變：重新讀取目錄、顯示中的使用者、帳號待辦與此帳號的授權。 */
  saved(title: string, description?: string) {
    this.access.saved(title, description);
    this.changed();
  }
  /** 正在寫入順序；期間不能再拖，避免第二次移動以舊的位置計算。 */
  readonly ordering = computed(() => this.writing.pendingFor('order'));
  /** 沒有條件時本頁是整份名單連續的一段，可以先在本頁換序；有條件時只能等重讀。 */
  readonly reorderable = computed(() => !this.query.filtered());
  /**
   * 把使用者移到整份名單第 `position` 位：先在本頁換序（畫面立即反應，`SortableRows` 接著播放落定動畫），
   * 寫入後重讀本頁取得伺服器算出的排序值與編號；失敗時換回原本的列並提示。
   */
  async move(id: string, position: number) {
    const page = this.users();
    if (!page || this.ordering()) return;
    if (this.reorderable()) this.users.set(reordered(page, id, position));
    const outcome = await this.writing.run(() => this.api.moveUser(id, position), {
      key: 'order',
    });
    if (!outcome.ok && !outcome.error) return;
    if (!outcome.ok) this.users.set(page);
    this.table.reload();
  }
  /** 清除所有人的順序，回到依姓名、帳號排列。 */
  async resetOrder() {
    const outcome = await this.writing.run(() => this.api.resetUserOrder(), { key: 'order' });
    if (outcome.ok) this.toast.success('已清除使用者順序', '出勤紀錄等列表改回依姓名排列。');
    if (outcome.ok || outcome.error) this.table.reload();
  }
  roleNames(ids: string[]) {
    return this.access.roleNames(ids);
  }
}

/**
 * 本頁的樂觀換序：未搜尋時本頁的編號連續（`offset + 1` 起），移到頁外的人先從本頁拿掉。
 * 被移動的人與它越過的未排序者先標成已排序（值由伺服器決定，重讀後換成真的值）。
 */
function reordered<
  Page extends { users: AdminUserDto[]; offset: number; sorted: number; total: number },
>(page: Page, id: string, position: number): Page {
  const users = [...page.users];
  const from = users.findIndex((user) => user.id === id);
  if (from < 0) return page;
  const [moved] = users.splice(from, 1);
  const index = position - page.offset - 1;
  if (index >= 0 && index <= users.length) users.splice(index, 0, moved);
  let sorted = page.sorted;
  const at = users.indexOf(moved);
  const next = users.map((user, i) => {
    const passed = at >= 0 && i <= at && user.sortOrder == null;
    if (passed) sorted++;
    return { ...user, position: page.offset + i + 1, sortOrder: passed ? 0 : user.sortOrder };
  });
  return { ...page, users: next, sorted };
}
