import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import type { PunchReimportResult } from '@core/api/schema';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { FeaturePage } from '@core/layout/feature-page';
import { formatBytes, formatDate, formatNumber } from '@shared/browser/format';
import { ViewScope } from '@shared/browser/view-scope';
import { CopyValue } from '@shared/ui/copy-value';
import { DataTable, DataTableColumn, DataTableRow, TableState } from '@shared/ui/data-table';
import { DetailDrawer } from '@shared/ui/detail-drawer';
import { DrawerPager } from '@shared/ui/drawer-pager';
import { Field } from '@shared/ui/field';
import { Icon } from '@shared/ui/icon';
import { Notice } from '@shared/ui/notice';
import { PendingButton } from '@shared/ui/pending-button';
import { QueryBar } from '@shared/ui/query-bar';
import { queryState } from '@shared/ui/query-state';
import { StatusBadge } from '@shared/ui/status-badge';
import { TableHeader } from '@shared/ui/table-header';
import { endpointTable } from '@shared/ui/table-model';
import { TimeRangePicker } from '@shared/ui/time-range-picker';
import { ToastService } from '@shared/ui/toast-service';
import { ViewSwitch } from '@shared/ui/view-switch';
import { AttendanceApi } from '../attendance-api';
import { IMPORT_TRIGGER, importStatus } from '../attendance-presentation';
import { attendanceImportsTour } from '../attendance-tour';
import {
  IMPORT_RANGES,
  IMPORT_STATUSES,
  emptyImportsQuery,
  importFilters,
  importsQueryOptions,
} from './imports-query';
import { ImportPunches } from './import-punches';
import { ReimportDialog, reimportOutcome } from './reimport-dialog';

/**
 * 打卡匯入紀錄（`attendance.imports`）：每次匯入（排程或手動）一筆，預設新到舊；排序、欄位篩選與分頁交給 `endpointTable`。
 * 點一筆開詳情：這個檔目前的內容（`isCurrent`）列出匯入的刷卡，失敗或已被之後的匯入取代的只列結果與來源資訊。
 * 有 `attendance.imports.reimport` 時可指定檔名日期（或日期區間）手動重匯；結果就是第一頁新出現的紀錄。
 */
@Component({
  selector: 'nx-attendance-imports-page',
  imports: [
    CopyValue,
    DataTable,
    DataTableColumn,
    DataTableRow,
    DetailDrawer,
    DrawerPager,
    FeaturePage,
    Field,
    Icon,
    ImportPunches,
    Notice,
    PendingButton,
    QueryBar,
    ReimportDialog,
    StatusBadge,
    TableHeader,
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
    .imports-replaced {
      display: grid;
      justify-items: start;
      gap: var(--p-space-2);
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
    }),
  );
  /** 套用條件時把相對時間換算成起訖；條件不合法時不讀。 */
  private readonly filters = computed(() => {
    const filters = importFilters(this.query.applied());
    return typeof filters === 'string' ? undefined : filters;
  });
  readonly table = endpointTable({
    feature: 'attendance.imports',
    params: this.filters,
    load: (filters, table, signal) => this.api.imports(filters, table, signal),
    columns: [
      { id: 'startedAt', label: '時間', hideable: false, sort: 'desc', filter: 'date' },
      { id: 'fileName', label: '檔名', hideable: false, sort: true, filter: 'text' },
      { id: 'status', label: '結果', hideable: false },
      { id: 'punchCount', label: '筆數', sort: 'desc', filter: 'number', numeric: true },
      {
        id: 'triggerKind',
        label: '觸發',
        filter: {
          options: Object.entries(IMPORT_TRIGGER).map(([value, label]) => ({ value, label })),
        },
      },
    ],
    sort: { column: 'startedAt', direction: 'desc' },
    navigated: () => {
      this.drawer()?.close();
      this.selectedId.set(null);
    },
  });
  readonly presentedRows = computed(() =>
    this.table.rows().map((row) => ({
      row,
      time: formatDate(row.startedAt),
      status: importStatus(row.status),
      count: formatNumber(row.punchCount),
      trigger: IMPORT_TRIGGER[row.triggerKind] ?? row.triggerKind,
      finished: formatDate(row.finishedAt),
      sourceModified: formatDate(row.sourceModifiedAt),
      size: formatBytes(row.sourceSize),
    })),
  );
  /** 開啟詳情的匯入；詳情只用列上的資料，刷卡由 `ImportPunches` 另外讀。 */
  readonly selectedId = signal<string | null>(null);
  readonly selectedIndex = computed(() =>
    this.presentedRows().findIndex((item) => item.row.id === this.selectedId()),
  );
  readonly selected = computed(() => this.presentedRows()[this.selectedIndex()] ?? null);
  readonly drawer = viewChild(DetailDrawer);
  /** 最近一次手動重匯中失敗的檔（檔案格式錯誤等）；請求本身成功，所以放在頁面提示。 */
  readonly failure = signal('');
  private readonly dialog = viewChild.required(ReimportDialog);

  reload() {
    this.table.reload();
  }
  openDetails(id: string) {
    this.selectedId.set(id);
    this.drawer()?.open();
  }
  adjacent(direction: -1 | 1) {
    const item = this.presentedRows()[this.selectedIndex() + direction];
    if (item) this.openDetails(item.row.id);
  }
  /** 已被取代的匯入：只看這個檔的紀錄，最新一次成功的就在最前面。 */
  showFile(fileName: string) {
    this.query.commit({ fileName, status: '', range: emptyImportsQuery().range });
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
    this.table.goTo(0);
    this.table.reload();
  }
}
