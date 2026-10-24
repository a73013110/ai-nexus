import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import type { CardAssignmentDto, CardDto, CardPunchDto } from '@core/api/schema';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { FeaturePage } from '@core/layout/feature-page';
import { formatDate, formatDateTimeInput, formatDay, nameInitial } from '@shared/browser/format';
import { ViewScope } from '@shared/browser/view-scope';
import { ActionMenu, type MenuAction } from '@shared/ui/action-menu';
import { ConfirmDialog } from '@shared/ui/confirm-dialog';
import {
  DataTable,
  DataTableColumn,
  DataTableRow,
  TableState,
  type TableColumn,
} from '@shared/ui/data-table';
import { DetailDrawer } from '@shared/ui/detail-drawer';
import { Field } from '@shared/ui/field';
import { Icon } from '@shared/ui/icon';
import { Notice } from '@shared/ui/notice';
import { PendingButton } from '@shared/ui/pending-button';
import { QueryBar } from '@shared/ui/query-bar';
import { queryState } from '@shared/ui/query-state';
import { SettingRow } from '@shared/ui/setting-row';
import { StatusBadge } from '@shared/ui/status-badge';
import { ViewSwitch } from '@shared/ui/view-switch';
import { clockTime, weekday } from '../attendance-presentation';
import { attendanceCardsTour, cardDetailTour } from '../attendance-tour';
import { AssignmentDialog } from './assignment-dialog';
import { AttendanceCardsStore, holderName } from './attendance-cards-store';
import { CardDialog } from './card-dialog';
import { CARD_SEARCH_MAX, CARD_STATUSES, type CardStatus, cardsQueryOptions } from './cards-query';

const EMPTY_TEXT: Record<CardStatus, string> = {
  '': '移除搜尋條件，或新增還沒刷過的新卡。',
  unassigned: '所有刷卡都已有持有人。',
  held: '目前沒有人持有卡片。',
  hidden: '沒有在出勤紀錄隱藏的卡。',
};

/**
 * 卡號對應（`attendance.cards`）：一張卡號清單。打卡檔出現過的卡號匯入時自動建立，
 * 有沒有持有人涵蓋的刷卡就是「待指派」；HR 不想在全公司出勤看到的卡（訪客卡、董事）改為隱藏。
 * 寫入另需 `attendance.cards.maintain`，沒有時不顯示寫入按鈕；測試身分顯示但停用（伺服器回 `test_admin_read_only`）。
 */
@Component({
  selector: 'nx-attendance-cards-page',
  imports: [
    ActionMenu,
    AssignmentDialog,
    CardDialog,
    ConfirmDialog,
    DataTable,
    DataTableColumn,
    DataTableRow,
    DetailDrawer,
    FeaturePage,
    Field,
    Icon,
    Notice,
    PendingButton,
    QueryBar,
    SettingRow,
    StatusBadge,
    TableState,
    ViewSwitch,
  ],
  providers: [AttendanceCardsStore, ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: ':host { display: block; min-width: 0; }',
  styleUrl: './attendance-cards-page.scss',
  templateUrl: './attendance-cards-page.html',
})
export class AttendanceCardsPage {
  readonly session = inject(WorkspaceSession);
  readonly store = inject(AttendanceCardsStore);
  readonly query = queryState(cardsQueryOptions);
  readonly draft = this.query.draft;
  readonly searchMax = CARD_SEARCH_MAX;
  readonly statuses = CARD_STATUSES;
  readonly holderName = holderName;
  protected readonly initial = nameInitial;
  readonly tour = computed(() =>
    attendanceCardsTour({ maintain: this.store.canMaintain(), testing: this.session.testing() }),
  );
  /** 選取卡片時才有抽屜的導覽。 */
  readonly drawerTour = computed(() =>
    this.store.selected()
      ? cardDetailTour({ maintain: this.store.canMaintain(), testing: this.session.testing() })
      : null,
  );
  readonly drawer = viewChild(DetailDrawer);
  private readonly cardDialog = viewChild.required(CardDialog);
  private readonly assignmentDialog = viewChild.required(AssignmentDialog);
  private readonly confirmation = viewChild.required(ConfirmDialog);
  /** 抽屜裡的動作（刪除、隱藏）失敗；對話框的失敗留在對話框。 */
  readonly drawerError = signal('');
  readonly columns: TableColumn[] = [
    { id: 'number', label: '卡號', hideable: false },
    { id: 'holder', label: '目前持有人' },
    { id: 'since', label: '持有起' },
    { id: 'punched', label: '最後刷卡' },
    { id: 'status', label: '狀態' },
    { id: 'note', label: '備註' },
  ];
  readonly rows = computed(() =>
    this.store.cards().map((card) => ({
      card,
      holder: card.current ? holderName(card.current) : '',
      since: card.current ? formatDate(card.current.startsAt) : '',
      punched: card.lastPunchedAt ? formatDate(card.lastPunchedAt) : '',
      // 隱藏的卡是 HR 刻意不管的，不提示待指派。
      pending: card.hiddenFromRecords ? 0 : card.unattributedCount,
    })),
  );
  readonly emptyText = computed(() => EMPTY_TEXT[this.query.applied().status]);
  /** 持有歷史與刷卡紀錄骨架的寬度（%）。 */
  protected readonly skeleton = [72, 54, 64];
  readonly history = computed(() => {
    const now = Date.now();
    return (this.store.assignments() ?? []).map((item) => ({
      item,
      holder: holderName(item),
      unbound: !item.userId,
      starts: formatDate(item.startsAt),
      ends: item.endsAt ? formatDate(item.endsAt) : '',
      current: Date.parse(item.startsAt) <= now && (!item.endsAt || Date.parse(item.endsAt) > now),
      actions: this.entryActions(item),
    }));
  });
  /** 無人持有時名字下方那行：最後一次刷卡，讓 HR 判斷這張卡是不是還在用。 */
  readonly idleMeta = computed(() => {
    const punched = this.store.selected()?.lastPunchedAt;
    return punched ? `最後刷卡 ${formatDate(punched)}` : '還沒有刷卡紀錄';
  });
  /**
   * 刷卡紀錄依台北日期分組（新到舊）。日期取刷卡時間在工作區時區的日期，與出勤日一致；
   * 機器與事件代號原樣列出、不解讀。
   */
  readonly punchDays = computed(() => {
    const days: {
      date: string;
      label: string;
      punches: { punch: CardPunchDto; time: string; codes: string; codesTitle: string }[];
    }[] = [];
    for (const punch of this.store.punches() ?? []) {
      const date = formatDateTimeInput(new Date(punch.punchedAt)).slice(0, 10);
      let day = days.at(-1);
      if (day?.date !== date) {
        day = { date, label: `${formatDay(punch.punchedAt)}（${weekday(date)}）`, punches: [] };
        days.push(day);
      }
      day.punches.push({
        punch,
        time: clockTime(punch.punchedAt),
        codes: `${punch.machineCode}・${punch.eventCode}`,
        codesTitle: `機器代號 ${punch.machineCode}、事件代號 ${punch.eventCode}`,
      });
    }
    return days;
  });
  /** 選取的卡有沒有持有人涵蓋的刷卡（隱藏的卡不提示）：「N 筆刷卡沒有持有人（起–迄）」。 */
  readonly pending = computed(() => {
    const card = this.store.selected();
    if (!card || card.hiddenFromRecords || !card.unattributedCount) return null;
    const first = formatDate(card.firstUnattributedAt!),
      last = formatDate(card.lastUnattributedAt!);
    return {
      count: card.unattributedCount,
      span: first === last ? first : `${first} – ${last}`,
    };
  });

  /** 抽屜標題列的卡片操作；刷過卡或有持有歷史的卡不能刪，選單直接寫明原因。 */
  readonly cardActions = computed<MenuAction[]>(() => {
    const card = this.store.selected(),
      locked = this.store.locked(),
      used = !!this.history().length || !!card?.lastPunchedAt;
    return [
      { id: 'note', label: '編輯備註', icon: 'edit', disabled: locked },
      {
        id: 'delete',
        label: used ? '刪除卡片（已有刷卡或持有歷史）' : '刪除卡片',
        icon: 'trash',
        danger: true,
        // 持有歷史還沒讀到前不知道能不能刪，先停用。
        disabled: locked || this.store.assignments() === null || used,
      },
    ];
  });

  constructor() {
    effect(() => {
      const query = this.query.applied();
      untracked(() => this.store.filters.set({ search: query.search, status: query.status }));
    });
  }

  open(card: CardDto) {
    this.drawerError.set('');
    this.store.selectedId.set(card.id);
    this.drawer()?.open();
  }
  closed() {
    this.store.selectedId.set(null);
    this.drawerError.set('');
  }
  createCard() {
    this.cardDialog().create();
  }
  /** 新增的卡符合目前篩選時直接開啟，接著指派持有人。 */
  created(card: CardDto) {
    const query = this.query.applied();
    const shown = query.status === '' || (query.status === 'hidden' && card.hiddenFromRecords);
    if (shown && !query.search.trim()) this.open(card);
  }
  async deleteCard(card: CardDto) {
    if (
      !(await this.confirmation().ask({
        title: `刪除卡號 ${card.cardNumber}？`,
        message: '刪除後無法復原。只有沒刷過卡、也沒指派過持有人的卡可以刪除。',
        confirm: '刪除',
        danger: true,
      }))
    )
      return;
    const outcome = await this.store.deleteCard(card);
    if (outcome.saved) this.drawer()?.close();
    else this.drawerError.set(outcome.error);
  }
  cardAction(card: CardDto, action: string) {
    if (action === 'note') this.cardDialog().editNote(card);
    else if (action === 'delete') void this.deleteCard(card);
  }
  async setVisible(card: CardDto, visible: boolean) {
    this.drawerError.set('');
    const outcome = await this.store.setHidden(card, !visible);
    if (!outcome.saved) this.drawerError.set(outcome.error);
  }
  /** 卡片目前有人持有時就是轉給其他人，對話框會寫明前一位的期間在何時結束。 */
  assign(card: CardDto) {
    this.assignmentDialog().create({
      id: card.id,
      cardNumber: card.cardNumber,
      holder: card.current ? holderName(card.current) : null,
    });
  }
  /**
   * 補指派沒有持有人的刷卡：開始帶第一筆，結束帶它之後最早一段期間的開始，新期間才不會與既有的重疊。
   * 刷卡分散在多個空檔時一次補一段，存檔後清單會重算剩下的筆數。
   */
  assignPending(card: CardDto) {
    const start = card.firstUnattributedAt!;
    const next = (this.store.assignments() ?? [])
      .map((item) => item.startsAt)
      .filter((at) => Date.parse(at) > Date.parse(start))
      .sort((a, b) => Date.parse(a) - Date.parse(b))[0];
    this.assignmentDialog().create({ id: card.id, cardNumber: card.cardNumber }, start, next);
  }
  /** 「自 2023/08/01 起・已持有 3 年 2 個月」；開始時間在未來時寫明尚未開始。 */
  tenure(item: CardAssignmentDto) {
    const start = new Date(item.startsAt),
      now = new Date();
    const ends = item.endsAt ? `，${formatDay(item.endsAt)} 結束` : '';
    if (start > now) return `${formatDay(item.startsAt)} 起${ends}（尚未開始）`;
    const months =
      (now.getFullYear() - start.getFullYear()) * 12 +
      now.getMonth() -
      start.getMonth() -
      (now.getDate() < start.getDate() ? 1 : 0);
    const days = Math.floor((now.getTime() - start.getTime()) / 86_400_000);
    const span =
      months >= 12
        ? `${Math.floor(months / 12)} 年${months % 12 ? ` ${months % 12} 個月` : ''}`
        : months >= 1
          ? `${months} 個月`
          : `${days} 天`;
    return `${formatDay(item.startsAt)} 起・已持有 ${span}${ends}`;
  }
  private entryActions(item: CardAssignmentDto): MenuAction[] {
    const disabled = this.store.locked();
    return [
      ...(item.endsAt ? [] : [{ id: 'end', label: '結束持有', icon: 'clock', disabled }]),
      { id: 'edit', label: '編輯期間', icon: 'edit', disabled },
      { id: 'delete', label: '刪除期間', icon: 'trash', danger: true, disabled },
    ];
  }
  entryAction(item: CardAssignmentDto, action: string) {
    if (action === 'delete') void this.deleteAssignment(item);
    else this.editAssignment(item, action === 'end');
  }
  editAssignment(item: CardAssignmentDto, ending = false) {
    this.assignmentDialog().edit(item, ending);
  }
  async deleteAssignment(item: CardAssignmentDto) {
    if (
      !(await this.confirmation().ask({
        title: `刪除 ${holderName(item)} 的持有期間？`,
        message:
          '刪除後這段期間的刷卡不再屬於這位持有人，會變成待指派。只是不再持有時，請改用「結束持有」。',
        confirm: '刪除',
        danger: true,
      }))
    )
      return;
    const outcome = await this.store.deleteAssignment(item);
    if (!outcome.saved) this.drawerError.set(outcome.error);
  }
}
