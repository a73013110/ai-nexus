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
  ViewEncapsulation,
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
import { DataTable, DataTableColumn, TableState, type TableColumn } from '@shared/ui/data-table';
import { endpointTable } from '@shared/ui/table-model';
import { Icon } from '@shared/ui/icon';
import { InfoPopover } from '@shared/ui/info-popover';
import { Notice } from '@shared/ui/notice';
import { PendingButton } from '@shared/ui/pending-button';
import { QueryBar, QueryField } from '@shared/ui/query-bar';
import { queryState } from '@shared/ui/query-state';
import { optionTags } from '@shared/ui/option-tags';
import { Select, type SelectOption } from '@shared/ui/select';
import { TreeSelect } from '@shared/ui/tree-select';
import { StatusBadge } from '@shared/ui/status-badge';
import { TimeRangePicker } from '@shared/ui/time-range-picker';
import { ViewSwitch } from '@shared/ui/view-switch';
import { OrganizationApi } from '@features/admin/organization/organization-api';
import { unitNodes } from '@features/admin/organization/unit-tree';
import { AttendanceApi, type AttendanceRecordFilters } from '../attendance-api';
import { attendanceProblem, presentSchedule } from '../attendance-presentation';
import { attendanceRecordsTour } from '../attendance-tour';
import { GdFormDialog } from '../forms/gd-form-dialog';
import { RecordsPersonGroup, presentGroup } from './records-person-group';
import {
  RECORD_COVERAGES,
  RECORD_RANGES,
  cardKey,
  recordFilters,
  recordsQueryOptions,
  type RecordsCoverage,
} from './records-query';

/** 每頁人數：一人一年約 250 列（捲到附近才畫）；上限與後端 `ListAttendanceRecords.MaxPageSize` 相同。 */
const PAGE_SIZES = [10, 20, 50];
const DEFAULT_PAGE_SIZE = 20;

/**
 * 出勤紀錄（`attendance.records`）：資料範圍內每人每天的判定、上下班刷卡、應下班、遲到早退與當天的假單。
 * 名單依人分組、依人分頁（區間最長一年）；每人的每一天都列出，一頁可達數千列，每人的列捲到附近才建立
 * （`RecordsPersonGroup`），讀完不會卡住。公文系統連不上時頁首標示，判定改為無法判定而不是異常。
 * 人員選單另外讀（範圍內持過卡的人，含還沒綁帳號的姓名持有人），不靠目前這一頁的名單；單位放在人員前面，選了單位後人員選單只列那個單位的人。
 * 沒有刷卡的放假日預設不列（只有表單的也一樣，勾「含放假日」才列），放假日附假日設定的備註。
 * 單位與人員選單只給資料範圍看得到別人的人；單位清單另外要組織管理權限。
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
    GdFormDialog,
    Notice,
    PendingButton,
    QueryBar,
    QueryField,
    RecordsPersonGroup,
    Select,
    RouterLink,
    StatusBadge,
    TableState,
    TimeRangePicker,
    TreeSelect,
    ViewSwitch,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [ViewScope],
  // 不封裝：每人一段的 `RecordsPersonGroup` 共用這份樣式（class 一律 `records-` 開頭，見樣式檔開頭）。
  encapsulation: ViewEncapsulation.None,
  styleUrl: './attendance-records-page.scss',
  templateUrl: './attendance-records-page.html',
})
export class AttendanceRecordsPage {
  readonly session = inject(WorkspaceSession);
  private readonly api = inject(AttendanceApi);
  private readonly organization = inject(OrganizationApi);
  private readonly view = inject(ViewScope);
  readonly ranges = RECORD_RANGES;
  readonly coverages = RECORD_COVERAGES;
  /** 隱藏的卡在卡號對應頁維護；沒有那一頁的權限時不給連結。 */
  readonly cardsRoute = routeOf('attendance.cards');
  /** 資料範圍看得到本人以外的人才有單位與人員篩選；由 `/me` 一進頁面就知道，只看得到自己的人不會先閃出欄位，也不讀這兩份選單。 */
  readonly seesOthers = computed(() => this.session.seesOthers('attendance.records'));
  /** 單位清單另外只有組織管理權限讀得到；沒有時不提供單位選單（連結帶入的單位照樣查詢）。 */
  readonly showUnits = computed(() => this.seesOthers() && this.session.has('organization'));
  private readonly unitsRead = apiResource({
    feature: 'organization',
    params: () => (this.showUnits() ? true : undefined),
    loader: () => this.organization.units(),
  });
  /** 單位清單讀取中；選單先佔位轉圈。 */
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
    // 讀取在建構完成後才開始，這時 `query` 已經建立；只回傳單位字串，草稿其他欄位（頁籤、區間）變動不重讀。
    params: () => (this.seesOthers() ? this.query.draft().unitId : undefined),
    loader: (unitId, signal) => this.api.people(unitId, signal),
  });
  readonly peopleLoading = this.peopleRead.refreshing;
  /** 使用者以 ID、沒有帳號的資料以卡號（`card:卡號`）為值；說明寫帳號或卡號（搜尋找得到），狀態以標籤列出。 */
  readonly personOptions: Signal<SelectOption[]> = computed(() => [
    { value: '', label: this.query.draft().unitId ? '單位內所有人' : '所有人' },
    ...(this.peopleRead.value() ?? []).map((person) => ({
      value: person.userId ?? cardKey(person.cardNumber!),
      label: person.displayName,
      description: person.userId ? (person.account ?? undefined) : `卡號 ${person.cardNumber}`,
      tags: optionTags(
        !person.userId && '尚未綁帳號',
        !person.holding && '已不持卡',
        person.hidden && '已隱藏',
      ),
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
  readonly tour = computed(() =>
    attendanceRecordsTour({ people: this.seesOthers(), units: this.showUnits() }),
  );
  /** 已套用的條件（換算成起訖）；每次套用都是新參考，表格據此回到第一頁重讀。不寫進網址。 */
  private readonly filters = signal<AttendanceRecordFilters | undefined>(undefined);
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
  /**
   * 依人分頁：後端以人為單位回 `offset`／`limit`（不走 `TableRequest`，沒有欄位排序與篩選），
   * 回應裡的期間、今天與公文系統狀態從 `data()` 取。
   */
  readonly table = endpointTable({
    feature: 'attendance.records',
    params: this.filters,
    load: async (filters, page, signal) => {
      const data = await this.api.records(filters, page.offset, page.limit, signal);
      return { ...data, items: data.people, offset: page.offset, complete: false };
    },
    columns: this.columns,
    pageSize: DEFAULT_PAGE_SIZE,
    pageSizes: PAGE_SIZES,
    unit: '人',
  });
  readonly records = this.table.data;
  /** 使用者感覺得到的讀取（查詢、換頁、重試）：表格淡化、查詢鈕轉圈。 */
  readonly refreshing = this.table.refreshing;
  /** 已有結果時的讀取失敗由頁首提示（附重試）；沒有結果時由表格狀態顯示。 */
  readonly readError = this.table.error;
  readonly people = this.table.rows;
  readonly total = this.table.total;
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
  readonly groups = computed(() => {
    const data = this.records();
    return data ? this.people().map((person) => presentGroup(person, data.today)) : [];
  });

  constructor() {
    // 每次套用條件都換一個新參考（表格回到第一頁重讀）；「本月」以套用當下的台北日期換算。
    effect(() => {
      const query = this.query.applied();
      untracked(() => {
        const filters = recordFilters(query);
        if (typeof filters !== 'string') this.filters.set(filters);
      });
    });
  }

  /** 摘要磚塊與查詢列前方的切換共用：切換即套用，回到第一頁。 */
  showCoverage(coverage: RecordsCoverage) {
    this.query.commit({ coverage });
  }

  reload() {
    this.table.reload();
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
