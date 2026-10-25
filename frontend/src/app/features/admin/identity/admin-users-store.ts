import { Injectable, computed, inject, signal } from '@angular/core';
import { apiResource } from '@core/api/api-resource';
import type { AdminUserDto } from '@core/api/schema';
import { safeMessage } from '@core/errors/safe-errors';
import { ViewScope } from '@shared/browser/view-scope';
import { AccessControlStore } from '@features/admin/access-control/access-control-store';
import { IdentityApi } from './identity-api';

/**
 * 使用者頁提供的使用者列表（搜尋與分頁）。角色與群組名稱來自同頁提供的 `AccessControlStore`，
 * 所以列表在目錄讀到後才讀，名稱不會先顯示成 ID。
 */
@Injectable()
export class AdminUsersStore {
  private readonly api = inject(IdentityApi);
  private readonly scope = inject(ViewScope);
  readonly access = inject(AccessControlStore);
  private readonly ready = computed(() => !!this.access.catalog());
  readonly search = signal('');
  private readonly query = signal('');
  private readonly typing = signal(false);
  readonly offset = signal(0);
  /** 伺服器固定每頁 100 筆（`ListAdminUsers`），分頁只能照它換算。 */
  readonly pageSize = 100;
  /**
   * 目前要看的那一頁（搜尋字與起點）；目錄讀到前為 undefined。換條件或換頁才會變，
   * 同一頁重讀（排序寫入後、帳號異動後）不變，表格據此只在換頁或換條件時捲回頂端。
   */
  readonly request = computed(() =>
    this.ready() ? { search: this.query(), offset: this.offset() } : undefined,
  );
  readonly usersRead = apiResource({
    feature: 'identity.users',
    params: this.request,
    loader: ({ search, offset }) => this.api.users(search, offset),
  });
  readonly users = computed(() => this.usersRead.value() ?? null);
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
   * 表格的讀取狀態：搜尋的防抖等待、重讀，以及列表還在等目錄（目錄讀到前列表不會開始讀）。
   * 沒有列時表格畫骨架，有列時淡化。
   */
  readonly loadingUsers = computed(
    () => this.typing() || this.usersRead.refreshing() || this.access.loading(),
  );
  readonly actionError = this.access.actionError;
  readonly toast = this.access.toast;
  /** 使用者列表沒有資料時讀取失敗由表格狀態顯示（附重試），頁首只提示已有資料時的失敗。 */
  readonly error = computed(
    () => this.access.error() || (this.users()?.users.length ? this.usersRead.error() : ''),
  );

  load() {
    this.access.load();
    this.usersRead.reload();
    this.reviewsRead.reload();
  }
  /** 這位使用者的帳號待辦；沒有時為空陣列。 */
  reviewsOf(id: string) {
    return this.reviewsById().get(id) ?? [];
  }
  find(value: string) {
    this.search.set(value);
    this.typing.set(true);
    this.scope.later(
      () => {
        this.typing.set(false);
        this.offset.set(0);
        this.query.set(value);
      },
      250,
      'admin-users-search',
    );
  }
  /** 在 `offset` 顯示使用者頁面；若已顯示，則重新讀取。 */
  readUsers(offset = this.offset()) {
    if (offset === this.offset()) this.usersRead.reload();
    else this.offset.set(offset);
  }
  /** 帳號或 AD 資料已寫入：重新讀取顯示中的使用者與帳號待辦。 */
  changed(offset = this.offset()) {
    this.readUsers(offset);
    this.reviewsRead.reload();
  }
  /** 授權已改變：重新讀取目錄、顯示中的使用者、帳號待辦與此帳號的授權。 */
  saved(title: string, description?: string) {
    this.access.saved(title, description);
    this.changed();
  }
  /** 正在寫入順序；期間不能再拖，避免第二次移動以舊的位置計算。 */
  readonly ordering = signal(false);
  /** 未搜尋時本頁是整份名單連續的一段，可以先在本頁換序；搜尋時只能等重讀。 */
  readonly reorderable = computed(() => !this.query() && !this.typing());
  /**
   * 把使用者移到整份名單第 `position` 位：先在本頁換序（畫面立即反應，`SortableRows` 接著播放落定動畫），
   * 寫入後重讀本頁取得伺服器算出的排序值與編號；失敗時換回原本的列並提示。
   */
  async move(id: string, position: number) {
    const page = this.users();
    if (!page || this.ordering()) return;
    if (this.reorderable()) this.usersRead.value.set(reordered(page, id, position));
    this.ordering.set(true);
    try {
      await this.api.moveUser(id, position);
    } catch (error) {
      this.usersRead.value.set(page);
      this.actionError.set(safeMessage(error));
    } finally {
      this.ordering.set(false);
      this.usersRead.reload();
    }
  }
  /** 清除所有人的順序，回到依姓名、帳號排列。 */
  async resetOrder() {
    this.ordering.set(true);
    try {
      await this.api.resetUserOrder();
      this.toast.success('已清除使用者順序', '出勤紀錄等列表改回依姓名排列。');
    } catch (error) {
      this.actionError.set(safeMessage(error));
    } finally {
      this.ordering.set(false);
      this.usersRead.reload();
    }
  }
  roleNames(ids: string[]) {
    return this.access.roleNames(ids);
  }
}

/**
 * 本頁的樂觀換序：未搜尋時本頁的編號連續（`offset + 1` 起），移到頁外的人先從本頁拿掉。
 * 被移動的人與它越過的未排序者先標成已排序（值由伺服器決定，重讀後換成真的值）。
 */
function reordered(
  page: { users: AdminUserDto[]; offset: number; sorted: number; total: number },
  id: string,
  position: number,
) {
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
