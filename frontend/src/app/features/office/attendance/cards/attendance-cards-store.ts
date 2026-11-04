import { Injectable, computed, inject, signal } from '@angular/core';
import { apiResource } from '@core/api/api-resource';
import type {
  CardAssignmentCreateRequest,
  CardAssignmentDto,
  CardAssignmentUpdateRequest,
  CardCreateRequest,
  CardDto,
} from '@core/api/schema';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { ACTION_SKIPPED, action, type ActionOutcome } from '@shared/browser/action';
import { ViewScope } from '@shared/browser/view-scope';
import { ToastService } from '@shared/ui/toast-service';
import { AttendanceApi } from '../attendance-api';
import { errorCode } from '@core/errors/safe-errors';
import { punchTable } from '../punches/punch-table';

/** 新增持有期間；要接手的那一段的版本由 `assign` 在使用者確認後才帶上。 */
export type AssignRequest = Omit<CardAssignmentCreateRequest, 'takeOverRowVersion'>;

/** 持有期間 `[startsAt, endsAt)` 是否涵蓋某個時間點。 */
export const covers = (item: CardAssignmentDto, at: number) =>
  Date.parse(item.startsAt) <= at && (!item.endsAt || Date.parse(item.endsAt) > at);
export const holderName = (item: CardAssignmentDto) =>
  item.userDisplayName ?? item.holderName ?? '（未填持有人）';

/**
 * 卡號對應頁提供（`providers`）的狀態：卡號清單、選取卡片的持有歷史與刷卡紀錄，以及寫入。
 * 寫入成功後重讀伺服器的值（目前持有人、待指派筆數都會跟著變）；持有歷史以卡片 ID 為鍵，切換卡片時不會短暫顯示上一張的歷史。
 */
@Injectable()
export class AttendanceCardsStore {
  private readonly api = inject(AttendanceApi);
  private readonly scope = inject(ViewScope);
  private readonly session = inject(WorkspaceSession);
  private readonly toast = inject(ToastService);
  /** 已套用的卡片篩選；頁面依網址設定，`undefined` 時不讀。 */
  readonly filters = signal<{ search: string; status: string } | undefined>(undefined);
  private readonly cardsRead = apiResource({
    feature: 'attendance.cards',
    params: this.filters,
    loader: (filters, signal) =>
      this.api.cards(
        { search: filters.search.trim() || undefined, status: filters.status || undefined },
        signal,
      ),
  });
  readonly cards = computed(() => this.cardsRead.value() ?? []);
  readonly cardsLoaded = computed(() => this.cardsRead.value() !== undefined);
  readonly cardsLoading = this.cardsRead.loading;
  /** 查詢、重試與寫入後的重讀：表格淡化、查詢鈕轉圈。 */
  readonly cardsRefreshing = this.cardsRead.refreshing;
  readonly cardsError = this.cardsRead.error;
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
  /** 選取卡片的刷卡紀錄；換卡時從第一頁重讀，寫入後跟著重讀（歸戶會變）。 */
  readonly punches = punchTable({
    feature: 'attendance.cards',
    params: () => this.selectedId() ?? undefined,
    load: (id, table, signal) => this.api.cardPunches(id, table, signal),
    card: true,
  });
  /** 寫入按鈕只給有 `attendance.cards.maintain` 的人。 */
  readonly canMaintain = computed(() => this.session.has('attendance.cards.maintain'));
  private readonly writing = action();
  /** 寫入中或資料重讀中：重讀完之前畫面上的版本是舊的，這時送出只會被拒，所以寫入鈕一起停用。 */
  readonly busy = computed(
    () => this.writing.pending() || this.cardsRefreshing() || this.assignmentsRefreshing(),
  );
  /** 抽屜裡按下的那一項（`card:<id>`、`assignment:<id>`）；只讓那顆按鈕轉，其他寫入鈕照 `busy` 停用。 */
  working(key: string) {
    return this.writing.pendingFor(key);
  }

  reloadCards() {
    this.cardsRead.reload();
  }
  reloadAssignments() {
    this.assignmentsRead.reload();
  }

  createCard(body: CardCreateRequest) {
    return this.write(() => this.api.createCard(body), `已新增卡號 ${body.cardNumber.trim()}`);
  }
  saveNote(card: CardDto, note: string | null) {
    return this.write(
      () =>
        this.api.updateCard(card.id, {
          hiddenFromRecords: card.hiddenFromRecords,
          note,
          rowVersion: card.rowVersion,
        }),
      '已儲存備註',
    );
  }
  /** 切換是否在出勤紀錄顯示；開關旁轉圈（`working('hidden:<id>')`），備註照舊送回。 */
  setHidden(card: CardDto, hidden: boolean) {
    return this.write(
      () =>
        this.api.updateCard(card.id, {
          hiddenFromRecords: hidden,
          note: card.note ?? null,
          rowVersion: card.rowVersion,
        }),
      hidden
        ? `卡號 ${card.cardNumber} 已在出勤紀錄隱藏`
        : `卡號 ${card.cardNumber} 已在出勤紀錄顯示`,
      `hidden:${card.id}`,
    );
  }
  async deleteCard(card: CardDto) {
    const outcome = await this.write(
      () => this.api.deleteCard(card.id, card.rowVersion),
      `已刪除卡號 ${card.cardNumber}`,
      `card:${card.id}`,
    );
    if (outcome.ok && this.selectedId() === card.id) this.selectedId.set(null);
    return outcome;
  }

  /**
   * 指派持有人。卡片已有人持有時伺服器回 409 `attendance_card_held`（不帶持有人）：
   * 讀這張卡的持有歷史找出新開始時間落在誰的期間，交給 `confirmTakeOver` 問使用者，同意才帶那一段的版本重送；
   * 確認期間那一段又被改過時，伺服器回 409，不會結束使用者沒看過的內容。
   */
  async assign(
    request: AssignRequest,
    confirmTakeOver: (holder: string) => Promise<boolean>,
  ): Promise<ActionOutcome<CardAssignmentDto>> {
    if (this.busy()) return ACTION_SKIPPED;
    const body: CardAssignmentCreateRequest = { ...request, takeOverRowVersion: null };
    const first = await this.writing.run(async () => {
      try {
        return { held: false as const, saved: await this.api.createAssignment(body) };
      } catch (error) {
        if (errorCode(error) !== 'attendance_card_held') throw error;
        const at = Date.parse(body.startsAt);
        const history = await this.api.assignments(body.cardId);
        return { held: true as const, current: history.find((item) => covers(item, at)) };
      }
    });
    if (!first.ok) return this.settle<CardAssignmentDto>(first, assigned);
    if (!first.value.held) return this.settle({ ok: true, value: first.value.saved }, assigned);
    // 問使用者時不算送出中：確認對話框的按鈕要能按。
    const current = first.value.current;
    const alive = this.scope.guard();
    const agreed = await confirmTakeOver(current ? holderName(current) : '目前的持有人');
    if (!alive()) return ACTION_SKIPPED;
    if (!agreed) {
      this.afterWrite();
      return ACTION_SKIPPED;
    }
    return this.write(
      () => this.api.createAssignment({ ...body, takeOverRowVersion: current?.rowVersion ?? null }),
      assigned,
    );
  }
  updateAssignment(id: string, body: CardAssignmentUpdateRequest, done = '已儲存持有期間') {
    return this.write(() => this.api.updateAssignment(id, body), done);
  }
  deleteAssignment(item: CardAssignmentDto) {
    return this.write(
      () => this.api.deleteAssignment(item.id, item.rowVersion),
      `已刪除 ${holderName(item)} 的持有期間`,
      `assignment:${item.id}`,
    );
  }

  /** 持有變動會改變目前持有人、待指派筆數、持有歷史與每筆刷卡的歸戶，全部重讀；刷卡留在目前這一頁。 */
  private afterWrite() {
    this.cardsRead.reload();
    this.assignmentsRead.reload();
    this.punches.reload();
  }

  /** 寫入中或重讀中略過；成功提示 `done`。成功或失敗都重讀（例如期間有人改了同一張卡），讓畫面回到伺服器的狀態。 */
  private async write<T>(
    run: () => Promise<T>,
    done: string | ((value: T) => string),
    key = '',
  ): Promise<ActionOutcome<T>> {
    if (this.busy()) return ACTION_SKIPPED;
    return this.settle(await this.writing.run(run, { key }), done);
  }

  private settle<T>(outcome: ActionOutcome<T>, done: string | ((value: T) => string)) {
    if (outcome.ok) this.toast.success(typeof done === 'string' ? done : done(outcome.value));
    if (outcome.ok || outcome.error) this.afterWrite();
    return outcome;
  }
}

const assigned = (saved: CardAssignmentDto) =>
  `已將卡號 ${saved.cardNumber} 指派給 ${holderName(saved)}`;
