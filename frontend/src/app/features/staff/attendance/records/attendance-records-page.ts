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
import type { AttendancePersonDto } from '@core/api/schema';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { FeaturePage } from '@core/layout/feature-page';
import { Checkbox } from '@shared/ui/checkbox';
import { DataTable, DataTableColumn, TableState, type TableColumn } from '@shared/ui/data-table';
import { Notice } from '@shared/ui/notice';
import { QueryBar } from '@shared/ui/query-bar';
import { queryState } from '@shared/ui/query-state';
import { Select, type SelectOption } from '@shared/ui/select';
import { StatusBadge } from '@shared/ui/status-badge';
import { TimeRangePicker } from '@shared/ui/time-range-picker';
import { OrganizationApi } from '@features/admin/organization/organization-api';
import { unitRows } from '@features/admin/organization/unit-tree';
import { AttendanceApi, type AttendanceRecordFilters } from '../attendance-api';
import { presentRecord } from '../attendance-presentation';
import { RECORD_RANGES, recordFilters, recordsQueryOptions } from './records-query';

/**
 * 出勤紀錄（`attendance.records`）：資料範圍內每人每天的判定、上下班刷卡、應下班、遲到早退與當天的假單。
 * 名單依人分組、不分頁（後端一次回整段，最長 31 天）；公文系統連不上時頁首標示，判定改為無法判定而不是異常。
 * 單位清單只有組織管理權限讀得到，沒有時不提供單位選單（連結帶入的單位照樣查詢）。
 */
@Component({
  selector: 'nx-attendance-records-page',
  imports: [
    Checkbox,
    DataTable,
    DataTableColumn,
    FeaturePage,
    Notice,
    QueryBar,
    Select,
    StatusBadge,
    TableState,
    TimeRangePicker,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: ':host { display: block; min-width: 0; }',
  styleUrl: './attendance-records-page.scss',
  templateUrl: './attendance-records-page.html',
})
export class AttendanceRecordsPage {
  readonly session = inject(WorkspaceSession);
  private readonly api = inject(AttendanceApi);
  private readonly organization = inject(OrganizationApi);
  readonly ranges = RECORD_RANGES;
  /** 讀過的人（ID → 人）；指定某人之後名單只剩他，選單仍保留先前讀到的其他人。 */
  private readonly known = signal<ReadonlyMap<string, AttendancePersonDto>>(new Map());
  private readonly unitsRead = apiResource({
    feature: 'organization',
    params: () => (this.session.has('organization') ? true : undefined),
    loader: () => this.organization.units(),
  });
  readonly unitOptions = computed<SelectOption[] | null>(() => {
    const units = this.unitsRead.value();
    if (!units) return null;
    return [
      { value: '', label: '所有單位' },
      ...unitRows(units).map((row) => ({ value: row.unit.id, label: row.path.join(' › ') })),
    ];
  });
  readonly query = queryState(
    recordsQueryOptions({
      person: (id) => this.known().get(id)?.displayName,
      unit: (id) => this.unitsRead.value()?.find((unit) => unit.id === id)?.name,
    }),
  );
  readonly draft = this.query.draft;
  private readonly request = signal<AttendanceRecordFilters | undefined>(undefined);
  private readonly recordsRead = apiResource({
    feature: 'attendance.records',
    params: this.request,
    loader: (query, signal) => this.api.records(query, signal),
  });
  readonly records = this.recordsRead.value;
  readonly loading = this.recordsRead.loading;
  readonly readError = this.recordsRead.error;
  readonly people = computed(() => this.records()?.people ?? []);
  readonly personOptions = computed<SelectOption[]>(() => [
    { value: '', label: '所有人' },
    ...[...this.known().values()]
      .sort((a, b) => a.displayName.localeCompare(b.displayName, 'zh-Hant'))
      .map((person) => ({
        value: person.userId,
        label: person.displayName,
        description: person.account,
      })),
  ]);
  /**
   * 「含不列入出勤」只在全公司範圍（`all`）有作用，但回應與 `/me` 都沒有帶範圍；
   * 只看得到自己（一個人）時不顯示，已勾選或名單裡出現不列入的人時一定顯示。
   */
  readonly showExcluded = computed(
    () =>
      this.query.applied().includeExcluded ||
      this.people().length > 1 ||
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
    // 每次套用條件都重讀；「本月」以套用當下的台北日期換算。
    effect(() => {
      const query = this.query.applied();
      untracked(() => {
        const filters = recordFilters(query);
        if (typeof filters !== 'string') this.request.set(filters);
      });
    });
    effect(() => {
      const people = this.records()?.people;
      if (!people?.length) return;
      untracked(() =>
        this.known.update((current) => {
          const next = new Map(current);
          for (const person of people) next.set(person.userId, person);
          return next;
        }),
      );
    });
  }

  reload() {
    this.recordsRead.reload();
  }
}
