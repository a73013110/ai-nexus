import { Injectable, computed, inject, linkedSignal, signal } from '@angular/core';
import { apiResource } from '@core/api/api-resource';
import type {
  CardAssignmentCreateRequest,
  CardAssignmentDto,
  CardAssignmentUpdateRequest,
  CardCreateRequest,
  CardDto,
  CardPunchDto,
} from '@core/api/schema';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { safeMessage } from '@core/errors/safe-errors';
import { ViewScope } from '@shared/browser/view-scope';
import { ToastService } from '@shared/ui/toast-service';
import { AttendanceApi } from '../attendance-api';
import { errorCode } from '../attendance-presentation';

/** 刷卡紀錄每頁筆數；與後端 `ListCardPunches.PageSize` 相同。 */
export const PUNCH_PAGE = 50;

/** 寫入的結果：失敗時 `error` 是審核過的訊息；使用者取消接手時兩者皆空。 */
export type WriteOutcome<T = unknown> = { saved: true; value: T } | { saved: false; error: string };

/** 新增持有期間；接手旗標由 `assign` 在使用者確認後才帶上。 */
export type AssignRequest = Omit<CardAssignmentCreateRequest, 'takeOver'>;

/** 持有期間 `[startsAt, endsAt)` 是否涵蓋某個時間點。 */
export const covers = (item: CardAssignmentDto, at: number) =>
  Date.parse(item.startsAt) <= at && (!item.endsAt || Date.parse(item.endsAt) > at);
export const holderName = (item: CardAssignmentDto) =>
  item.userDisplayName ?? item.holderName ?? '（未填持有人）';

/**
 * 卡號對應頁提供（`providers`）的狀態：卡號清單、選取卡片的持有歷史，以及寫入。
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
  /** 刷卡紀錄的游標（已載入最後一筆的 ID）；換卡時回到第一頁。 */
  private readonly punchesBefore = linkedSignal<string | null, number | undefined>({
    source: this.selectedId,
    computation: () => undefined,
  });
  private readonly punchesRead = apiResource({
    feature: 'attendance.cards',
    params: () => {
      const id = this.selectedId();
      return id ? { id, before: this.punchesBefore() } : undefined;
    },
    loader: async (request, signal) => ({
      ...request,
      items: await this.api.cardPunches(request.id, request.before, signal),
    }),
  });
  /** 選取卡片已載入的刷卡（各頁接起來）；還沒讀到這張卡的第一頁時為 null，不顯示上一張的紀錄。 */
  readonly punches = linkedSignal<
    { id: string; before?: number; items: CardPunchDto[] } | undefined,
    CardPunchDto[] | null
  >({
    source: () => this.punchesRead.value(),
    computation: (read, previous) => {
      if (!read || read.id !== this.selectedId()) return null;
      if (read.before === undefined) return read.items;
      const seen = new Set(previous?.value?.map((item) => item.id));
      return [...(previous?.value ?? []), ...read.items.filter((item) => !seen.has(item.id))];
    },
  });
  /** 游標分頁不算總數：最後一頁滿 50 筆，才表示可能還有更早的刷卡。 */
  readonly punchesHasMore = computed(() => this.punchesRead.value()?.items.length === PUNCH_PAGE);
  readonly punchesError = this.punchesRead.error;
  /** 重讀第一頁（寫入後）：已有的紀錄淡化。載入更多只轉按鈕。 */
  readonly punchesRefreshing = computed(
    () => this.punchesRead.refreshing() && this.punchesBefore() === undefined,
  );
  readonly punchesLoadingMore = computed(
    () => this.punchesRead.refreshing() && this.punchesBefore() !== undefined,
  );
  /** 寫入按鈕只給有 `attendance.cards.maintain` 的人。 */
  readonly canMaintain = computed(() => this.session.has('attendance.cards.maintain'));
  readonly busy = signal(false);
  /** 抽屜裡按下的刪除（`card:<id>`、`assignment:<id>`）；只讓那顆按鈕轉，其他寫入鈕照 `busy` 停用。 */
  readonly working = signal('');

  reloadCards() {
    this.cardsRead.reload();
  }
  reloadAssignments() {
    this.assignmentsRead.reload();
  }
  reloadPunches() {
    this.punchesRead.reload();
  }
  morePunches() {
    const items = this.punches();
    if (!items?.length || this.punchesRead.refreshing()) return;
    this.punchesBefore.set(items[items.length - 1].id);
  }

  createCard(body: CardCreateRequest) {
    return this.write(() => this.api.createCard(body), `已新增卡號 ${body.cardNumber.trim()}`);
  }
  saveNote(card: CardDto, note: string | null) {
    return this.write(
      () => this.api.updateCard(card.id, { hiddenFromRecords: card.hiddenFromRecords, note }),
      '已儲存備註',
    );
  }
  /** 切換是否在出勤紀錄顯示；開關旁轉圈（`working` 為 `hidden:<id>`），備註照舊送回。 */
  setHidden(card: CardDto, hidden: boolean) {
    return this.write(
      () => this.api.updateCard(card.id, { hiddenFromRecords: hidden, note: card.note ?? null }),
      hidden
        ? `卡號 ${card.cardNumber} 已在出勤紀錄隱藏`
        : `卡號 ${card.cardNumber} 已在出勤紀錄顯示`,
      `hidden:${card.id}`,
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
    if (this.busy()) return { saved: false, error: '' };
    const alive = this.scope.guard();
    this.busy.set(true);
    try {
      const body: CardAssignmentCreateRequest = { ...request, takeOver: false };
      let saved: CardAssignmentDto;
      try {
        saved = await this.api.createAssignment(body);
      } catch (error) {
        if (errorCode(error) !== 'attendance_card_held' || !alive()) throw error;
        const history = await this.api.assignments(body.cardId);
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

  /** 持有變動會改變目前持有人、待指派筆數、持有歷史與每筆刷卡的歸戶，全部重讀；刷卡回到第一頁。 */
  private afterWrite() {
    this.cardsRead.reload();
    this.assignmentsRead.reload();
    if (this.punchesBefore() === undefined) this.punchesRead.reload();
    else this.punchesBefore.set(undefined);
  }

  /** 寫入共用的忙碌、錯誤與提示；離開頁面或換帳號後才回來的結果不再套用。 */
  private async write<T>(run: () => Promise<T>, done: string, key = ''): Promise<WriteOutcome<T>> {
    if (this.busy()) return { saved: false, error: '' };
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
