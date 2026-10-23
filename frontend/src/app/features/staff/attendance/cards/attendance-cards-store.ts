import { Injectable, computed, inject, signal } from '@angular/core';
import { apiResource } from '@core/api/api-resource';
import type {
  CardAssignmentCreateRequest,
  CardAssignmentDto,
  CardAssignmentUpdateRequest,
  CardDto,
  CardRequest,
} from '@core/api/schema';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { safeMessage } from '@core/errors/safe-errors';
import { ViewScope } from '@shared/browser/view-scope';
import { ToastService } from '@shared/ui/toast-service';
import { AttendanceApi } from '../attendance-api';
import { errorCode } from '../attendance-presentation';

/** 寫入的結果：失敗時 `error` 是審核過的訊息；使用者取消接手時兩者皆空。 */
export type WriteOutcome<T = unknown> = { saved: true; value: T } | { saved: false; error: string };

/** 新增持有期間；`cardId` 為 null 表示卡號尚未登錄（未歸戶卡號），儲存時先登錄成員工卡。 */
export type AssignRequest = Omit<CardAssignmentCreateRequest, 'cardId' | 'takeOver'> & {
  cardId: string | null;
  cardNumber: string;
};

/** 持有期間 `[startsAt, endsAt)` 是否涵蓋某個時間點。 */
export const covers = (item: CardAssignmentDto, at: number) =>
  Date.parse(item.startsAt) <= at && (!item.endsAt || Date.parse(item.endsAt) > at);
export const holderName = (item: CardAssignmentDto) =>
  item.userDisplayName ?? item.holderName ?? '（未填持有人）';

/**
 * 卡號對應頁提供（`providers`）的狀態：卡片清單、選取卡片的持有歷史與未歸戶卡號，以及寫入。
 * 寫入成功後重讀伺服器的值（目前持有人、未歸戶都會跟著變）；持有歷史以卡片 ID 為鍵，切換卡片時不會短暫顯示上一張的歷史。
 */
@Injectable()
export class AttendanceCardsStore {
  private readonly api = inject(AttendanceApi);
  private readonly scope = inject(ViewScope);
  private readonly session = inject(WorkspaceSession);
  private readonly toast = inject(ToastService);
  /** 已套用的卡片篩選；頁面依網址設定，`undefined` 時不讀。 */
  readonly filters = signal<{ search: string; kind: string } | undefined>(undefined);
  private readonly cardsRead = apiResource({
    feature: 'attendance.cards',
    params: this.filters,
    loader: (filters, signal) =>
      this.api.cards(
        { search: filters.search.trim() || undefined, kind: filters.kind || undefined },
        signal,
      ),
  });
  readonly cards = computed(() => this.cardsRead.value() ?? []);
  readonly cardsLoaded = computed(() => this.cardsRead.value() !== undefined);
  readonly cardsLoading = this.cardsRead.loading;
  /** 查詢、重試與寫入後的重讀：表格淡化、查詢鈕轉圈。 */
  readonly cardsRefreshing = this.cardsRead.refreshing;
  readonly cardsError = this.cardsRead.error;
  /** 切到未歸戶卡號時才讀。 */
  readonly showUnattributed = signal(false);
  private readonly unattributedRead = apiResource({
    feature: 'attendance.cards',
    params: () => (this.showUnattributed() ? true : undefined),
    loader: (_params, signal) => this.api.unattributed(signal),
  });
  readonly unattributed = computed(() => this.unattributedRead.value() ?? []);
  readonly unattributedLoading = this.unattributedRead.loading;
  readonly unattributedRefreshing = this.unattributedRead.refreshing;
  readonly unattributedError = this.unattributedRead.error;
  readonly selectedId = signal<string | null>(null);
  /** 選取的卡片；被別人刪除或不在目前篩選結果時變成 null。 */
  readonly selected = computed(
    () => this.cards().find((card) => card.id === this.selectedId()) ?? null,
  );
  private readonly assignmentsRead = apiResource({
    feature: 'attendance.cards',
    params: () => this.selectedId() ?? undefined,
    loader: async (id, signal) => ({ id, items: await this.api.assignments(id, signal) }),
  });
  readonly assignments = computed<CardAssignmentDto[] | null>(() => {
    const loaded = this.assignmentsRead.value();
    return loaded && loaded.id === this.selectedId() ? loaded.items : null;
  });
  readonly assignmentsError = this.assignmentsRead.error;
  /** 寫入後重讀持有歷史：已有的歷史保留並淡化。 */
  readonly assignmentsRefreshing = this.assignmentsRead.refreshing;
  /** 寫入按鈕只給有 `attendance.cards.maintain` 的人；測試身分看得到但停用（伺服器回 `test_admin_read_only`）。 */
  readonly canMaintain = computed(() => this.session.has('attendance.cards.maintain'));
  readonly busy = signal(false);
  /** 抽屜裡按下的刪除（`card:<id>`、`assignment:<id>`）；只讓那顆按鈕轉，其他寫入鈕照 `locked` 停用。 */
  readonly working = signal('');
  readonly actionError = signal('');
  readonly locked = computed(() => this.busy() || this.session.testing());

  reloadCards() {
    this.actionError.set('');
    this.cardsRead.reload();
  }
  reloadUnattributed() {
    this.unattributedRead.reload();
  }
  reloadAssignments() {
    this.assignmentsRead.reload();
  }

  saveCard(id: string | null, body: CardRequest) {
    return this.write(
      () => (id ? this.api.updateCard(id, body) : this.api.createCard(body)),
      id ? '已儲存卡片' : `已登錄卡號 ${body.cardNumber.trim()}`,
    );
  }
  async deleteCard(card: CardDto) {
    const outcome = await this.write(
      () => this.api.deleteCard(card.id),
      `已刪除卡號 ${card.cardNumber}`,
      `card:${card.id}`,
    );
    if (outcome.saved && this.selectedId() === card.id) this.selectedId.set(null);
    return outcome;
  }

  /**
   * 指派持有人。卡片已有人持有時伺服器回 409 `attendance_card_held`（不帶持有人）：
   * 讀這張卡的持有歷史找出新開始時間落在誰的期間，交給 `confirmTakeOver` 問使用者，同意才以 `takeOver` 重送。
   */
  async assign(
    request: AssignRequest,
    confirmTakeOver: (holder: string) => Promise<boolean>,
  ): Promise<WriteOutcome<CardAssignmentDto>> {
    if (this.locked()) return { saved: false, error: '' };
    const alive = this.scope.guard();
    this.busy.set(true);
    try {
      let cardId = request.cardId;
      if (!cardId) {
        // 未登錄的卡號先登錄成員工卡；之後指派失敗時卡片仍保留，清單會顯示為已登錄。
        cardId = (
          await this.api.createCard({
            cardNumber: request.cardNumber,
            kind: 'employee',
            note: null,
          })
        ).id;
      }
      const body: CardAssignmentCreateRequest = {
        cardId,
        userId: request.userId,
        holderName: request.holderName,
        startsAt: request.startsAt,
        endsAt: request.endsAt,
        note: request.note,
        takeOver: false,
      };
      let saved: CardAssignmentDto;
      try {
        saved = await this.api.createAssignment(body);
      } catch (error) {
        if (errorCode(error) !== 'attendance_card_held' || !alive()) throw error;
        const history = await this.api.assignments(cardId);
        const at = Date.parse(body.startsAt);
        const current = history.find((item) => covers(item, at));
        this.busy.set(false);
        const agreed = await confirmTakeOver(current ? holderName(current) : '目前的持有人');
        if (!alive() || !agreed) {
          this.afterWrite();
          return { saved: false, error: '' };
        }
        this.busy.set(true);
        saved = await this.api.createAssignment({ ...body, takeOver: true });
      }
      if (!alive()) return { saved: false, error: '' };
      this.toast.success(`已將卡號 ${saved.cardNumber} 指派給 ${holderName(saved)}`);
      this.afterWrite();
      return { saved: true, value: saved };
    } catch (error) {
      if (!alive()) return { saved: false, error: '' };
      this.afterWrite();
      return { saved: false, error: safeMessage(error) };
    } finally {
      if (alive()) this.busy.set(false);
    }
  }
  updateAssignment(id: string, body: CardAssignmentUpdateRequest, done = '已儲存持有期間') {
    return this.write(() => this.api.updateAssignment(id, body), done);
  }
  deleteAssignment(item: CardAssignmentDto) {
    return this.write(
      () => this.api.deleteAssignment(item.id),
      `已刪除 ${holderName(item)} 的持有期間`,
      `assignment:${item.id}`,
    );
  }

  /** 持有變動會改變目前持有人、持有歷史與未歸戶卡號，三者都重讀。 */
  private afterWrite() {
    this.cardsRead.reload();
    this.assignmentsRead.reload();
    if (this.showUnattributed()) this.unattributedRead.reload();
  }

  /** 寫入共用的忙碌、錯誤與提示；離開頁面或換帳號後才回來的結果不再套用。 */
  private async write<T>(run: () => Promise<T>, done: string, key = ''): Promise<WriteOutcome<T>> {
    if (this.locked()) return { saved: false, error: '' };
    const alive = this.scope.guard();
    this.busy.set(true);
    this.working.set(key);
    try {
      const value = await run();
      if (!alive()) return { saved: false, error: '' };
      this.toast.success(done);
      this.afterWrite();
      return { saved: true, value };
    } catch (error) {
      if (!alive()) return { saved: false, error: '' };
      // 例如期間有人改了同一張卡：重讀，讓畫面回到伺服器的狀態。
      this.afterWrite();
      return { saved: false, error: safeMessage(error) };
    } finally {
      if (alive()) {
        this.busy.set(false);
        this.working.set('');
      }
    }
  }
}
