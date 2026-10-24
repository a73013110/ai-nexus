import { RouterLink } from '@angular/router';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
  untracked,
} from '@angular/core';
import { apiResource } from '@core/api/api-resource';
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
import { QueryBar } from '@shared/ui/query-bar';
import { queryState } from '@shared/ui/query-state';
import { Select, type SelectOption } from '@shared/ui/select';
import { StatusBadge } from '@shared/ui/status-badge';
import { TimeRangePicker } from '@shared/ui/time-range-picker';
import { OrganizationApi } from '@features/admin/organization/organization-api';
import { unitOptions } from '@features/admin/organization/unit-tree';
import { AttendanceApi, type AttendanceRecordFilters } from '../attendance-api';
import { attendanceProblem, presentRecord, presentSchedule } from '../attendance-presentation';
import { attendanceRecordsTour } from '../attendance-tour';
import { RECORD_RANGES, holderKey, recordFilters, recordsQueryOptions } from './records-query';

/** 每頁人數：一人一年約 250 列，頁面一次只畫這幾個人；上限與後端 `ListAttendanceRecords.MaxPageSize` 相同。 */
const PAGE_SIZES = [10, 20, 50];
const DEFAULT_PAGE_SIZE = 20;

/**
 * 出勤紀錄（`attendance.records`）：資料範圍內每人每天的判定、上下班刷卡、應下班、遲到早退與當天的假單。
 * 名單依人分組、依人分頁（區間最長一年，一次只畫一頁的人）；公文系統連不上時頁首標示，判定改為無法判定而不是異常。
 * 人員選單另外讀（範圍內持過卡的人，含還沒綁帳號的姓名持有人），不靠目前這一頁的名單。
 * 沒有刷卡也沒有假單的放假日預設不列（勾「含放假日」才列），放假日附假日設定的備註。
 * 單位清單只有組織管理權限讀得到，沒有時不提供單位選單（連結帶入的單位照樣查詢）。
 * 判定規則放在頁首的說明面板，文字與時間由後端（`AttendanceRules`，依目前設定）提供，與 Excel 匯出的「說明」工作表同一份。
 * 匯出用已套用的條件，預設區間在按下時重算（與 chip 標籤一致），由伺服器產生整段、全部人的 .xlsx（不分頁）。
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
    Notice,
    PendingButton,
    QueryBar,
    Select,
    RouterLink,
    StatusBadge,
    TablePagination,
    TableState,
    TimeRangePicker,
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
  /** 不列入出勤的名單在卡號對應頁維護；沒有那一頁的權限時不給連結。 */
  readonly exclusionsRoute = routeOf('attendance.cards');
  private readonly unitsRead = apiResource({
    feature: 'organization',
    params: () => (this.session.has('organization') ? true : undefined),
    loader: () => this.organization.units(),
  });
  /** 單位清單讀取中（有組織權限才會讀）；選單先佔位轉圈。 */
  readonly unitsLoading = this.unitsRead.refreshing;
  readonly unitOptions = computed<SelectOption[] | null>(() => {
    const units = this.unitsRead.value();
    return units ? [{ value: '', label: '所有單位' }, ...unitOptions(units)] : null;
  });
  /** 人員選單：範圍看得到、持有過員工卡的人，與名單同一套範圍（與目前這一頁無關）。 */
  private readonly peopleRead = apiResource({
    feature: 'attendance.records',
    loader: (_: true, signal) => this.api.people(signal),
  });
  readonly peopleLoading = this.peopleRead.refreshing;
  readonly personOptions = computed<SelectOption[]>(() => [
    { value: '', label: '所有人' },
    ...(this.peopleRead.value() ?? []).map((person) => ({
      value: person.userId ?? holderKey(person.displayName),
      label: person.displayName,
      description: [
        person.account ?? '尚未綁帳號',
        person.holding ? '' : '已不持卡',
        person.excluded ? '不列入出勤' : '',
      ]
        .filter(Boolean)
        .join('・'),
    })),
  ]);
  readonly query = queryState(
    recordsQueryOptions({
      person: (id) => this.peopleRead.value()?.find((person) => person.userId === id)?.displayName,
      unit: (id) => this.unitsRead.value()?.find((unit) => unit.id === id)?.name,
    }),
  );
  readonly draft = this.query.draft;
  readonly tour = computed(() => attendanceRecordsTour({ units: !!this.unitOptions() }));
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
  /** 只看得到自己（選單只有一個人）時不顯示人員選單；已指定某人或讀取中時佔住欄位。 */
  readonly showPeople = computed(
    () => this.personOptions().length > 2 || !!this.draft().person || this.peopleLoading(),
  );
  /**
   * 「含不列入出勤」只在全公司範圍（`all`）有作用，但回應與 `/me` 都沒有帶範圍；
   * 只看得到自己時不顯示，已勾選或名單裡出現不列入的人時一定顯示。
   */
  readonly showExcluded = computed(
    () =>
      this.query.applied().includeExcluded ||
      this.personOptions().length > 2 ||
      this.people().some((person) => person.excluded),
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
    { id: 'sheets', label: '假單與出差' },
  ];
  readonly groups = computed(() => {
    const data = this.records();
    if (!data) return [];
    return data.people.map((person) => ({
      person,
      key: person.userId ?? holderKey(person.displayName),
      anomalies: person.days.filter((day) => day.status === 'Anomaly').length,
      late: person.days.filter((day) => day.lateSeconds > 0).length,
      early: person.days.filter((day) => day.earlyLeaveSeconds > 0).length,
      days: person.days.map((day) => presentRecord(day, data.today)),
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
      const name = attachmentName(response, { title: '出勤紀錄', extension: 'xlsx' });
      const blob = await response.blob();
      if (guard()) downloadBlob(blob, name.title, name.extension);
    } catch (error) {
      if (guard()) this.exportError.set(attendanceProblem(error));
    } finally {
      if (guard()) this.exporting.set(false);
    }
  }
}
