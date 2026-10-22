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
import type { CardAssignmentDto, CardDto, UnattributedCardDto } from '@core/api/schema';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { FeaturePage } from '@core/layout/feature-page';
import { formatDate } from '@shared/browser/format';
import { ViewScope } from '@shared/browser/view-scope';
import { ConfirmDialog } from '@shared/ui/confirm-dialog';
import {
  DataTable,
  DataTableColumn,
  DataTableRow,
  TableState,
  type TableColumn,
} from '@shared/ui/data-table';
import { DetailDrawer } from '@shared/ui/detail-drawer';
import { Icon } from '@shared/ui/icon';
import { Notice } from '@shared/ui/notice';
import { QueryBar } from '@shared/ui/query-bar';
import { queryState } from '@shared/ui/query-state';
import { Select, type SelectOption } from '@shared/ui/select';
import { StatusBadge } from '@shared/ui/status-badge';
import { ViewSwitch } from '@shared/ui/view-switch';
import { Field } from '@shared/ui/field';
import { AttendanceCardsStore, holderName } from './attendance-cards-store';
import { CARD_KINDS, cardKindLabel } from '../attendance-presentation';
import { attendanceCardsTour, cardDetailTour } from '../attendance-tour';
import { AssignmentDialog } from './assignment-dialog';
import { CardDialog } from './card-dialog';
import { CARD_SEARCH_MAX, cardsQueryOptions } from './cards-query';

/**
 * 卡號對應（`attendance.cards`）：卡片與持有期間（歸戶依據），以及還沒有持有期間涵蓋的未歸戶卡號。
 * 寫入另需 `attendance.cards.maintain`，沒有時不顯示寫入按鈕；測試身分顯示但停用（伺服器回 `test_admin_read_only`）。
 */
@Component({
  selector: 'nx-attendance-cards-page',
  imports: [
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
    QueryBar,
    Select,
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
  readonly holderName = holderName;
  readonly kindOptions: SelectOption[] = [{ value: '', label: '全部種類' }, ...CARD_KINDS];
  readonly views = [
    { value: 'cards', label: '卡片' },
    { value: 'unattributed', label: '未歸戶卡號' },
  ];
  readonly tour = computed(() =>
    attendanceCardsTour({
      view: this.query.applied().view,
      maintain: this.store.canMaintain(),
      testing: this.session.testing(),
    }),
  );
  /** 選取卡片時才有抽屜的導覽；訪客卡沒有指派步驟。 */
  readonly drawerTour = computed(() => {
    const card = this.store.selected();
    return card
      ? cardDetailTour({
          visitor: card.kind === 'visitor',
          maintain: this.store.canMaintain(),
          testing: this.session.testing(),
        })
      : null;
  });
  readonly drawer = viewChild(DetailDrawer);
  private readonly cardDialog = viewChild.required(CardDialog);
  private readonly assignmentDialog = viewChild.required(AssignmentDialog);
  private readonly confirmation = viewChild.required(ConfirmDialog);
  /** 抽屜裡的動作（刪除）失敗；對話框的失敗留在對話框。 */
  readonly drawerError = signal('');
  readonly cardColumns: TableColumn[] = [
    { id: 'number', label: '卡號', hideable: false },
    { id: 'kind', label: '種類' },
    { id: 'holder', label: '目前持有人' },
    { id: 'since', label: '持有起' },
    { id: 'note', label: '備註' },
  ];
  readonly unattributedColumns: TableColumn[] = [
    { id: 'number', label: '卡號', hideable: false },
    { id: 'count', label: '未歸戶刷卡' },
    { id: 'first', label: '第一筆' },
    { id: 'last', label: '最後一筆' },
    { id: 'registered', label: '登錄' },
  ];
  readonly rows = computed(() =>
    this.store.cards().map((card) => ({
      card,
      kind: cardKindLabel(card.kind),
      holder: card.current ? holderName(card.current) : '',
      since: card.current ? formatDate(card.current.startsAt) : '',
    })),
  );
  readonly unattributedRows = computed(() =>
    this.store.unattributed().map((item) => ({
      item,
      first: formatDate(item.firstPunchedAt),
      last: formatDate(item.lastPunchedAt),
    })),
  );
  readonly history = computed(() => {
    const now = Date.now();
    return (this.store.assignments() ?? []).map((item) => ({
      item,
      holder: holderName(item),
      unbound: !item.userId,
      starts: formatDate(item.startsAt),
      ends: item.endsAt ? formatDate(item.endsAt) : '',
      current: Date.parse(item.startsAt) <= now && (!item.endsAt || Date.parse(item.endsAt) > now),
    }));
  });

  constructor() {
    effect(() => {
      const query = this.query.applied();
      untracked(() => {
        this.store.filters.set({ search: query.search, kind: query.kind });
        this.store.showUnattributed.set(query.view === 'unattributed');
        if (query.view !== 'cards') this.drawer()?.close();
      });
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
  /** 新登錄的卡符合目前篩選時直接開啟，接著指派持有人。 */
  created(card: CardDto) {
    const query = this.query.applied();
    if (query.view === 'cards' && !query.search.trim() && (!query.kind || query.kind === card.kind))
      this.open(card);
  }
  editCard(card: CardDto) {
    this.cardDialog().edit(card);
  }
  async deleteCard(card: CardDto) {
    if (
      !(await this.confirmation().ask({
        title: `刪除卡號 ${card.cardNumber}？`,
        message: '刪除後無法復原。只有從未指派過持有人的卡可以刪除。',
        confirm: '刪除',
        danger: true,
      }))
    )
      return;
    const outcome = await this.store.deleteCard(card);
    if (outcome.saved) this.drawer()?.close();
    else this.drawerError.set(outcome.error);
  }
  assign(card: CardDto) {
    this.assignmentDialog().create({ id: card.id, cardNumber: card.cardNumber });
  }
  /** 從未歸戶卡號指派：起始時間預設第一筆未歸戶刷卡，之後的刷卡都會歸戶。 */
  assignUnattributed(item: UnattributedCardDto) {
    this.assignmentDialog().create(
      { id: item.cardId, cardNumber: item.cardNumber },
      item.firstPunchedAt,
    );
  }
  editAssignment(item: CardAssignmentDto, ending = false) {
    this.assignmentDialog().edit(item, ending);
  }
  async deleteAssignment(item: CardAssignmentDto) {
    if (
      !(await this.confirmation().ask({
        title: `刪除 ${holderName(item)} 的持有期間？`,
        message:
          '刪除後這段期間的刷卡不再歸戶給這位持有人，會變成未歸戶。只是不再持有時，請改用「結束持有」。',
        confirm: '刪除',
        danger: true,
      }))
    )
      return;
    const outcome = await this.store.deleteAssignment(item);
    if (!outcome.saved) this.drawerError.set(outcome.error);
  }
}
