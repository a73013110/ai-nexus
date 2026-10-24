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
import { apiResource } from '@core/api/api-resource';
import type { PunchImportDto, PunchReimportResult } from '@core/api/schema';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { FeaturePage } from '@core/layout/feature-page';
import { formatDate, formatNumber } from '@shared/browser/format';
import { ViewScope } from '@shared/browser/view-scope';
import {
  CursorTrail,
  DataTable,
  DataTableColumn,
  PAGE_SIZES,
  TablePagination,
  TableState,
  type TableColumn,
} from '@shared/ui/data-table';
import { Field } from '@shared/ui/field';
import { Icon } from '@shared/ui/icon';
import { Notice } from '@shared/ui/notice';
import { PendingButton } from '@shared/ui/pending-button';
import { QueryBar } from '@shared/ui/query-bar';
import { queryState } from '@shared/ui/query-state';
import { StatusBadge } from '@shared/ui/status-badge';
import { TimeRangePicker } from '@shared/ui/time-range-picker';
import { ToastService } from '@shared/ui/toast-service';
import { ViewSwitch } from '@shared/ui/view-switch';
import { AttendanceApi, type PunchImportFilters } from '../attendance-api';
import { IMPORT_TRIGGER, importStatus } from '../attendance-presentation';
import { attendanceImportsTour } from '../attendance-tour';
import {
  IMPORT_RANGES,
  IMPORT_STATUSES,
  importFilters,
  importsQueryOptions,
} from './imports-query';
import { ReimportDialog, reimportOutcome } from './reimport-dialog';

interface ImportsPageRequest {
  filters: PunchImportFilters;
  take: number;
  /** 游標：上一頁最後一筆的 ID；第一頁為 undefined。 */
  before: string | undefined;
  index: number;
}

/**
 * 打卡匯入紀錄（`attendance.imports`）：每次匯入（排程或手動）一筆，新到舊、游標分頁、不計總數。
 * 有 `attendance.imports.reimport` 時可指定檔名日期（或日期區間）手動重匯；結果就是第一頁新出現的紀錄。
 */
@Component({
  selector: 'nx-attendance-imports-page',
  imports: [
    DataTable,
    DataTableColumn,
    FeaturePage,
    Field,
    Icon,
    Notice,
    PendingButton,
    QueryBar,
    ReimportDialog,
    StatusBadge,
    TablePagination,
    TableState,
    TimeRangePicker,
    ViewSwitch,
  ],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: block;
      min-width: 0;
    }
    .imports-table table {
      table-layout: fixed;
      min-width: 48rem;
    }
    .imports-time-column {
      width: 11rem;
    }
    .imports-file-column {
      width: 9rem;
    }
    .imports-status-column {
      width: 14rem;
    }
    .imports-count-column {
      width: 6rem;
    }
    .imports-file {
      font-family: var(--font-code);
    }
  `,
  templateUrl: './attendance-imports-page.html',
})
export class AttendanceImportsPage {
  readonly session = inject(WorkspaceSession);
  private readonly api = inject(AttendanceApi);
  private readonly toast = inject(ToastService);
  readonly statuses = IMPORT_STATUSES;
  readonly ranges = IMPORT_RANGES;
  readonly query = queryState(importsQueryOptions);
  readonly draft = this.query.draft;
  readonly tour = computed(() =>
    attendanceImportsTour({
      maintain: this.session.has('attendance.imports.reimport'),
      testing: this.session.testing(),
    }),
  );
  readonly pageSize = signal(50);
  private readonly request = signal<ImportsPageRequest | undefined>(undefined);
  private readonly pageRead = apiResource({
    feature: 'attendance.imports',
    params: this.request,
    loader: async (request, signal) => ({
      request,
      rows: await this.api.imports(request.filters, request.take, request.before, signal),
    }),
  });
  readonly rows = computed<PunchImportDto[]>(() => this.pageRead.value()?.rows ?? []);
  readonly pageIndex = computed(() => this.pageRead.value()?.request.index ?? 0);
  /** 游標分頁不算總數：本頁筆數等於每頁筆數，才表示可能還有更舊的紀錄。 */
  readonly hasNext = computed(() => {
    const page = this.pageRead.value();
    return !!page && page.rows.length === page.request.take;
  });
  /** 首次載入、換條件或翻頁：還沒有這次請求的列。 */
  readonly loading = this.pageRead.loading;
  /** 使用者感覺得到的讀取（含重試）：表格淡化、查詢與分頁擋住。 */
  readonly refreshing = this.pageRead.refreshing;
  readonly readError = this.pageRead.error;
  readonly columns: TableColumn[] = [
    { id: 'time', label: '時間', hideable: false },
    { id: 'file', label: '檔名', hideable: false },
    { id: 'status', label: '結果', hideable: false },
    { id: 'count', label: '筆數' },
    { id: 'trigger', label: '觸發' },
  ];
  readonly presentedRows = computed(() =>
    this.rows().map((row) => ({
      row,
      time: formatDate(row.startedAt),
      status: importStatus(row.status),
      count: formatNumber(row.punchCount),
      trigger: IMPORT_TRIGGER[row.triggerKind] ?? row.triggerKind,
    })),
  );
  /** 最近一次手動重匯中失敗的檔（檔案格式錯誤等）；請求本身成功，所以放在頁面提示。 */
  readonly failure = signal('');
  private readonly dialog = viewChild.required(ReimportDialog);
  private readonly table = viewChild(DataTable);
  private readonly cursors = new CursorTrail<string | undefined>(undefined);

  constructor() {
    effect(() => {
      const query = this.query.applied();
      untracked(() => {
        const filters = importFilters(query);
        if (typeof filters !== 'string') this.read(filters, undefined, 0);
      });
    });
    effect(() => {
      if (this.request()) this.table()?.resetScroll();
    });
  }

  changePage(direction: -1 | 1) {
    const index = this.pageIndex() + direction;
    if (index < 0 || (direction === 1 && !this.hasNext())) return;
    this.goTo(index, direction === 1 ? this.rows().at(-1)?.id : this.cursors.at(index));
  }
  firstPage() {
    this.goTo(0, undefined);
  }
  changePageSize(size: number) {
    const request = this.request();
    if (this.refreshing() || !request || !PAGE_SIZES.includes(size)) return;
    this.pageSize.set(size);
    this.read(request.filters, undefined, 0);
  }
  private goTo(index: number, before: string | undefined) {
    const request = this.request();
    if (this.refreshing() || !request) return;
    this.read(request.filters, before, index);
  }
  private read(filters: PunchImportFilters, before: string | undefined, index: number) {
    this.cursors.visit(index, before);
    this.request.set({ filters, take: this.pageSize(), before, index });
  }
  reload() {
    this.pageRead.reload();
  }
  openReimport() {
    this.failure.set('');
    this.dialog().open();
  }
  /** 重匯的紀錄在第一頁最前面：回到第一頁重讀。 */
  imported(result: PunchReimportResult) {
    const { toast, failure } = reimportOutcome(result);
    this.failure.set(failure ?? '');
    if (toast) this.toast.success(toast.title, toast.description);
    const request = this.request();
    if (request) this.read(request.filters, undefined, 0);
  }
}
