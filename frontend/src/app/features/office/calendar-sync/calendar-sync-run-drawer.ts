import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  output,
  viewChild,
} from '@angular/core';
import type { CalendarSyncRunDto } from '@core/api/schema';
import { formatDate, formatDuration, formatNumber } from '@shared/browser/format';
import { DataTable, DataTableColumn, TableState } from '@shared/ui/data-table';
import { DetailDrawer } from '@shared/ui/detail-drawer';
import { DrawerPager } from '@shared/ui/drawer-pager';
import { DrawerSummary } from '@shared/ui/drawer-summary';
import { Notice } from '@shared/ui/notice';
import { StatusBadge } from '@shared/ui/status-badge';
import { TableHeader } from '@shared/ui/table-header';
import { endpointTable } from '@shared/ui/table-model';
import { CalendarSyncApi } from './calendar-sync-api';
import {
  EVENT_KINDS,
  RUN_ITEM_OUTCOME_OPTIONS,
  RUN_TRIGGERS,
  failureText,
  runDuration,
  runItemOutcome,
  runItemReasons,
  runStatus,
} from './calendar-sync-presentation';

/**
 * 一輪同步的詳情：這一輪的摘要，以及逐筆處理了哪些事件、為什麼、失敗原因。
 * 明細是當下的快照且不會再變，所以只依所選的一輪讀一次，頁面輪詢重讀紀錄時不必跟著重讀。
 */
@Component({
  selector: 'nx-calendar-sync-run-drawer',
  imports: [
    DataTable,
    DataTableColumn,
    DetailDrawer,
    DrawerPager,
    DrawerSummary,
    Notice,
    StatusBadge,
    TableHeader,
    TableState,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: contents;
    }
    .run-items table {
      table-layout: fixed;
      min-width: 44rem;
    }
    .run-items-outcome-column {
      --column-width: 6.5rem;
    }
    .run-items-name-column {
      --column-width: 7.5rem;
    }
    .run-items-time-column {
      --column-width: 11rem;
    }
    .run-items-code-column {
      --column-width: 8.5rem;
    }
    .run-items-code {
      font-family: var(--font-code);
    }
    .run-items-reason {
      display: grid;
      gap: var(--p-space-1);
      overflow-wrap: anywhere;
    }
  `,
  templateUrl: './calendar-sync-run-drawer.html',
})
export class CalendarSyncRunDrawer {
  private readonly api = inject(CalendarSyncApi);
  readonly run = input<CalendarSyncRunDto | null>(null);
  readonly index = input(-1);
  readonly count = input(0);
  readonly move = output<-1 | 1>();
  readonly closed = output<void>();
  private readonly drawer = viewChild.required(DetailDrawer);
  readonly drawerId = computed(() => this.drawer().id);

  readonly summary = computed(() => {
    const run = this.run();
    if (!run) return null;
    return {
      run,
      time: formatDate(run.startedAt),
      status: runStatus(run.status),
      trigger:
        (RUN_TRIGGERS[run.triggerKind] ?? run.triggerKind) +
        (run.requestedByName ? `・${run.requestedByName}` : ''),
      problem: failureText(run.errorCode),
      tone: run.status === 'held' ? ('warning' as const) : ('danger' as const),
      duration: formatDuration(runDuration(run)),
      source: run.sourceCount === null ? '讀不到公文系統' : formatNumber(run.sourceCount),
      window: run.lookbackDays === null ? '未記錄' : `回看 ${run.lookbackDays} 天`,
    };
  });

  readonly items = endpointTable({
    feature: 'calendar-sync',
    params: () => this.run()?.id,
    load: (runId, table, signal) => this.api.runItems(runId, table, signal),
    columns: [
      {
        id: 'outcome',
        label: '結果',
        hideable: false,
        sort: true,
        filter: { options: RUN_ITEM_OUTCOME_OPTIONS },
      },
      { id: 'applicantName', label: '申請人', hideable: false, sort: true, filter: 'text' },
      { id: 'startsAt', label: '期間', sort: 'desc', filter: 'date' },
      { id: 'reason', label: '原因', hideable: false },
      { id: 'desNo', label: '文號', sort: true, filter: 'text' },
    ],
    sort: { column: 'desNo', direction: 'asc' },
  });
  readonly itemRows = computed(() =>
    this.items.rows().map((row) => ({
      row,
      outcome: runItemOutcome(row.outcome),
      kind: EVENT_KINDS[row.kind] ?? row.kind,
      period: `${formatDate(row.startsAt)} – ${formatDate(row.endsAt)}`,
      reasons: runItemReasons(row),
      problem: failureText(row.errorCode),
    })),
  );

  open() {
    this.drawer().open();
  }
  close() {
    this.drawer().close();
  }
}
