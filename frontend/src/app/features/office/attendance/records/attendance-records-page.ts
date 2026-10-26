import { NgTemplateOutlet } from '@angular/common';
import { RouterLink } from '@angular/router';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  linkedSignal,
  signal,
  untracked,
  type Signal,
} from '@angular/core';
import { apiResource, type ApiResource } from '@core/api/api-resource';
import type { AttendancePersonOptionDto } from '@core/api/schema';
import { routeOf } from '@core/api/catalog';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { FeaturePage } from '@core/layout/feature-page';
import { attachmentName, downloadBlob } from '@shared/browser/download';
import { ViewScope } from '@shared/browser/view-scope';
import { Checkbox } from '@shared/ui/checkbox';
import {
  DataTable,
  DataTableColumn,
  TablePagination,
  TableState,
  type TableColumn,
} from '@shared/ui/data-table';
import { Icon } from '@shared/ui/icon';
import { InfoPopover } from '@shared/ui/info-popover';
import { Notice } from '@shared/ui/notice';
import { PendingButton } from '@shared/ui/pending-button';
import { QueryBar, QueryField } from '@shared/ui/query-bar';
import { queryState } from '@shared/ui/query-state';
import { Select, type SelectOption } from '@shared/ui/select';
import { TreeSelect } from '@shared/ui/tree-select';
import { StatusBadge } from '@shared/ui/status-badge';
import { TimeRangePicker } from '@shared/ui/time-range-picker';
import { ViewSwitch } from '@shared/ui/view-switch';
import { OrganizationApi } from '@features/admin/organization/organization-api';
import { unitNodes } from '@features/admin/organization/unit-tree';
import { AttendanceApi, type AttendanceRecordFilters } from '../attendance-api';
import {
  attendanceProblem,
  presentRecord,
  presentSchedule,
  summarizePerson,
  weekOf,
} from '../attendance-presentation';
import { attendanceRecordsTour } from '../attendance-tour';
import { GdFormList } from './gd-form-list';
import {
  RECORD_COVERAGES,
  RECORD_RANGES,
  cardKey,
  recordFilters,
  recordsQueryOptions,
  type RecordsCoverage,
} from './records-query';

/** 每頁人數：一人一年約 250 列，頁面一次只畫這幾個人；上限與後端 `ListAttendanceRecords.MaxPageSize` 相同。 */
const PAGE_SIZES = [10, 20, 50];
const DEFAULT_PAGE_SIZE = 20;

/**
 * 出勤紀錄（`attendance.records`）：資料範圍內每人每天的判定、上下班刷卡、應下班、遲到早退與當天的假單。
 * 名單依人分組、依人分頁（區間最長一年，一次只畫一頁的人）；公文系統連不上時頁首標示，判定改為無法判定而不是異常。
 * 人員選單另外讀（範圍內持過卡的人，含還沒綁帳號的姓名持有人），不靠目前這一頁的名單；單位放在人員前面，選了單位後人員選單只列那個單位的人。
 * 沒有刷卡的放假日預設不列（只有表單的也一樣，勾「含放假日」才列），放假日附假日設定的備註。
 * 單位清單只有組織管理權限讀得到，沒有時不提供單位選單（連結帶入的單位照樣查詢）。
 * 判定規則放在頁首的說明面板，文字與時間由後端（`AttendanceRules`，依目前設定）提供。
 * 每人一段：最左的人員欄跨過那個人的每一天、捲動時黏住，段落交替底色，看任何一列都知道是誰；
 * 遲到的上班、早退的下班直接在時間上標色（舊系統的紅字），遲到、早退欄照樣列出時數。手機卡片改用段首的姓名列。
 * 匯出用已套用的條件，預設區間在按下時重算（與 chip 標籤一致），由伺服器產生與舊系統「出缺勤計算」一模一樣的 .xlsx（整段、全部人）。
 * 查詢列前方切換只看表單未補足或流程中的日子，段首標出每人有幾天要追；
 * 狀態欄以標籤寫當天發生什麼事（正常、遲到、早退、未刷卡…，可並列），顏色跟著補足狀態；補足欄緊鄰表單欄，以標籤寫補足狀態，接著還差多少與缺口時段，
 * 好通知補單或對照流程中假單的起訖。應下班下另列扣表單後的時間，表單欄每張單一張卡片（單號、假別、起訖、流程狀態與目前簽核人）。
 */
@Component({
  selector: 'nx-attendance-records-page',
  imports: [
    Checkbox,
    DataTable,
    DataTableColumn,
    FeaturePage,
    Icon,
    InfoPopover,
    GdFormList,
    NgTemplateOutlet,
    Notice,
    PendingButton,
    QueryBar,
    QueryField,
    Select,
    RouterLink,
    StatusBadge,
    TablePagination,
    TableState,
    TimeRangePicker,
    TreeSelect,
    ViewSwitch,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [ViewScope],
  styles: ':host { display: block; min-width: 0; }',
  styleUrl: './attendance-records-page.scss',
  templateUrl: './attendance-records-page.html',
})
export class AttendanceRecordsPage {
  readonly session = inject(WorkspaceSession);
  private readonly api = inject(AttendanceApi);
  private readonly organization = inject(OrganizationApi);
  private readonly view = inject(ViewScope);
  readonly ranges = RECORD_RANGES;
  readonly pageSizes = PAGE_SIZES;
  readonly coverages = RECORD_COVERAGES;
  /** 隱藏的卡在卡號對應頁維護；沒有那一頁的權限時不給連結。 */
  readonly cardsRoute = routeOf('attendance.cards');
  private readonly unitsRead = apiResource({
    feature: 'organization',
    params: () => (this.session.has('organization') ? true : undefined),
    loader: () => this.organization.units(),
  });
  /** 單位清單讀取中（有組織權限才會讀）；選單先佔位轉圈。 */
  readonly unitsLoading = this.unitsRead.refreshing;
  /** 停用的單位也能篩（看它過去的出勤），預設收起、面板底部可勾選列出。 */
  readonly unitNodes = computed(() => {
    const units = this.unitsRead.value();
    return units ? unitNodes(units) : null;
  });
  /**
   * 人員選單：範圍看得到、持有過卡的人，與名單同一套範圍（與目前這一頁無關）。跟著草稿的單位重讀（選單位就縮小、還沒按查詢也一樣），
   * 換單位時已選的人由查詢條件的連動清掉（`records-query` 的 `dependents`）。
   */
  // 標明型別：讀取依 `query` 的草稿，而 `query` 的 chip 名稱又來自這份選單，推論會繞成一圈。
  private readonly peopleRead: ApiResource<AttendancePersonOptionDto[]> = apiResource({
    feature: 'attendance.records',
    // 讀取在建構完成後才開始，這時 `query` 已經建立。
    params: () => ({ unitId: this.query.draft().unitId }),
    loader: (request, signal) => this.api.people(request.unitId, signal),
  });
  readonly peopleLoading = this.peopleRead.refreshing;
  /** 使用者以 ID、沒有帳號的資料以卡號（`card:卡號`）為值；說明列出卡號與狀態，搜尋也找得到卡號。 */
  readonly personOptions: Signal<SelectOption[]> = computed(() => [
    { value: '', label: this.query.draft().unitId ? '單位內所有人' : '所有人' },
    ...(this.peopleRead.value() ?? []).map((person) => ({
      value: person.userId ?? cardKey(person.cardNumber!),
      label: person.displayName,
      description: [
        person.userId ? person.account : `卡號 ${person.cardNumber}`,
        person.userId ? '' : '尚未綁帳號',
        person.holding ? '' : '已不持卡',
        person.hidden ? '已隱藏' : '',
      ]
        .filter(Boolean)
        .join('・'),
    })),
  ]);
  /** 讀過的人員名稱：已套用的人不在目前單位的選單裡時（草稿換了單位、還沒查詢），chip 仍寫得出名字。 */
  private readonly personNames = linkedSignal<SelectOption[], ReadonlyMap<string, string>>({
    source: this.personOptions,
    computation: (options, previous) =>
      new Map([
        ...(previous?.value ?? []),
        ...options
          .filter((option) => option.value)
          .map((option) => [option.value, option.label] as const),
      ]),
  });
  readonly query = queryState(
    recordsQueryOptions({
      person: (value) => this.personNames().get(value),
      unit: (id) => this.unitsRead.value()?.find((unit) => unit.id === id)?.name,
    }),
  );
  readonly draft = this.query.draft;
  readonly tour = computed(() => attendanceRecordsTour({ units: !!this.unitNodes() }));
  /** 已套用的條件（換算成起訖）；換頁與每頁人數沿用它，不寫進網址。 */
  private readonly filters = signal<AttendanceRecordFilters | undefined>(undefined);
  readonly pageIndex = signal(0);
  readonly pageSize = signal(DEFAULT_PAGE_SIZE);
  private readonly recordsRead = apiResource({
    feature: 'attendance.records',
    params: () => {
      const filters = this.filters();
      if (!filters) return undefined;
      return { filters, offset: this.pageIndex() * this.pageSize(), limit: this.pageSize() };
    },
    loader: (request, signal) =>
      this.api.records(request.filters, request.offset, request.limit, signal),
  });
  readonly records = this.recordsRead.value;
  /** 新條件的第一次讀取（還沒有這次的結果）。 */
  readonly loading = this.recordsRead.loading;
  /** 使用者感覺得到的讀取（查詢、換頁、重試）：表格淡化、查詢鈕轉圈。 */
  readonly refreshing = this.recordsRead.refreshing;
  readonly readError = this.recordsRead.error;
  readonly people = computed(() => this.records()?.people ?? []);
  readonly total = computed(() => this.records()?.total ?? 0);
  private readonly rulesRead = apiResource({
    feature: 'attendance.records',
    loader: (_: true, signal) => this.api.rules(signal),
  });
  /** 判定規則；讀不到時面板改顯示錯誤，不影響查詢。 */
  readonly rules = computed(() => {
    const rules = this.rulesRead.value();
    return rules ? { schedules: rules.schedules.map(presentSchedule), items: rules.rules } : null;
  });
  readonly rulesError = this.rulesRead.error;
  readonly exporting = signal(false);
  readonly exportError = signal('');
  /** 只看得到自己（選單只有一個人）時不顯示人員選單；已指定某人、選了單位（單位內可能只剩一人）或讀取中時佔住欄位。 */
  readonly showPeople = computed(
    () =>
      this.personOptions().length > 2 ||
      !!this.draft().person ||
      !!this.draft().unitId ||
      this.peopleLoading(),
  );
  readonly columns: TableColumn[] = [
    { id: 'date', label: '日期', hideable: false },
    { id: 'dayType', label: '日型' },
    { id: 'status', label: '狀態', hideable: false },
    { id: 'clockIn', label: '上班' },
    { id: 'clockOut', label: '下班' },
    { id: 'expected', label: '應下班' },
    { id: 'late', label: '遲到' },
    { id: 'early', label: '早退' },
    { id: 'coverage', label: '補足' },
    { id: 'forms', label: '表單' },
  ];
  readonly groups = computed(() => {
    const data = this.records();
    if (!data) return [];
    return data.people.map((person) => ({
      person,
      // 同一張卡先後有兩位只有姓名的持有人時是兩列。
      key: person.userId ?? `${cardKey(person.cardNumber!)}:${person.displayName}`,
      ...summarizePerson(person.days),
      // 換週的那一列上緣加分隔線；放假日不列時週一可能不在，所以比的是「哪一週」而不是星期幾。
      days: person.days.map((day, index, days) => ({
        ...presentRecord(day, data.today),
        weekStart: index > 0 && weekOf(day.date) !== weekOf(days[index - 1].date),
      })),
    }));
  });
  readonly period = computed(() => {
    const data = this.records();
    return data ? `${data.from.replaceAll('-', '/')} — ${data.to.slice(5).replace('-', '/')}` : '';
  });

  constructor() {
    // 每次套用條件都回到第一頁重讀；「本月」以套用當下的台北日期換算。
    effect(() => {
      const query = this.query.applied();
      untracked(() => {
        const filters = recordFilters(query);
        if (typeof filters === 'string') return;
        this.pageIndex.set(0);
        this.filters.set(filters);
      });
    });
  }

  /** 摘要磚塊與查詢列前方的切換共用：切換即套用，回到第一頁。 */
  showCoverage(coverage: RecordsCoverage) {
    this.query.commit({ coverage });
  }

  changePage(delta: -1 | 1) {
    this.pageIndex.update((index) => Math.max(0, index + delta));
  }
  changePageSize(size: number) {
    this.pageSize.set(size);
    this.pageIndex.set(0);
  }

  reload() {
    this.recordsRead.reload();
  }

  async export() {
    if (this.exporting()) return;
    // 「本月」等預設區間以按下匯出的當下重算，才與標籤相符；畫面上的結果可能是較早套用的。
    const filters = recordFilters(this.query.applied());
    if (typeof filters === 'string') {
      this.exportError.set(filters);
      return;
    }
    const guard = this.view.guard();
    this.exporting.set(true);
    this.exportError.set('');
    try {
      const response = await this.api.exportRecords(filters);
      const name = attachmentName(response, { title: '出缺勤計算', extension: 'xlsx' });
      const blob = await response.blob();
      if (guard()) downloadBlob(blob, name.title, name.extension);
    } catch (error) {
      if (guard()) this.exportError.set(attendanceProblem(error));
    } finally {
      if (guard()) this.exporting.set(false);
    }
  }
}
