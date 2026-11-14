import {
  ChangeDetectionStrategy,
  Component,
  Injector,
  afterNextRender,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { FeaturePage } from '@core/layout/feature-page';
import { formatDate } from '@shared/browser/format';
import { ViewScope } from '@shared/browser/view-scope';
import { DataTable, DataTableColumn, TableState } from '@shared/ui/data-table';
import { Select } from '@shared/ui/select';
import { StatusBadge } from '@shared/ui/status-badge';
import { TableHeader } from '@shared/ui/table-header';
import { cursorTable } from '@shared/ui/table-model';
import { ViewSwitch } from '@shared/ui/view-switch';
import { FlowApi, type FlowView } from '../flow-api';
import { FLOW_STATUS_OPTIONS, flowStatus } from '../flow-presentation';
import { FlowInstanceDrawer } from './flow-instance-drawer';

const EMPTY: Record<FlowView, { title: string; text: string }> = {
  tasks: { title: '沒有等你審核的申請', text: '有申請輪到你時，通知中心會提醒你。' },
  mine: { title: '你還沒有送出申請', text: '在儲存庫權限等頁面送出申請後，進度會列在這裡。' },
  decided: { title: '你還沒有審過申請', text: '核准或退回過的申請會列在這裡。' },
  all: { title: '還沒有任何申請', text: '' },
};

/**
 * 簽核（`flow.approvals`）：待我審核、我送出的、我審過的，持有 `flow.approvals.manage` 的人另可看全部。
 * 網址的 `?instance=` 是開著的申請（通知連結帶入），開關抽屜時同步改寫，重新整理或分享後仍開在同一份。
 * 待我審核一次讀完（數量有限）；其他檢視依送出時間由新到舊，以游標往後讀。
 */
@Component({
  selector: 'nx-flow-approvals-page',
  imports: [
    DataTable,
    DataTableColumn,
    FeaturePage,
    FlowInstanceDrawer,
    Select,
    StatusBadge,
    TableHeader,
    TableState,
    ViewSwitch,
  ],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: block;
      min-width: 0;
    }
    .flow-kind-column {
      --column-width: 8rem;
    }
    .flow-person-column {
      --column-width: 7rem;
    }
    .flow-status-column {
      --column-width: 6.5rem;
    }
    .flow-step-column {
      --column-width: 9rem;
    }
    .flow-time-column {
      --column-width: 10.5rem;
    }
  `,
  templateUrl: './flow-approvals-page.html',
})
export class FlowApprovalsPage {
  readonly session = inject(WorkspaceSession);
  private readonly api = inject(FlowApi);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly injector = inject(Injector);
  readonly drawer = viewChild.required(FlowInstanceDrawer);

  readonly views = computed(() => [
    { value: 'tasks', label: '待我審核' },
    { value: 'mine', label: '我的申請' },
    { value: 'decided', label: '我審過的' },
    ...(this.session.has('flow.approvals.manage') ? [{ value: 'all', label: '全部' }] : []),
  ]);
  readonly view = signal<FlowView>('tasks');
  readonly status = signal('');
  readonly statusOptions = FLOW_STATUS_OPTIONS;
  /** 待我審核都是審核中的申請，不套用狀態篩選。 */
  private readonly query = computed(() => ({
    view: this.view(),
    status: this.view() === 'tasks' ? '' : this.status(),
  }));
  readonly table = cursorTable({
    feature: 'flow.approvals',
    params: this.query,
    load: async ({ view, status }, table, signal) => {
      if (view === 'tasks') return { items: await this.api.tasks(signal), next: null };
      const cursor = table.cursor === undefined ? undefined : String(table.cursor);
      const page = await this.api.instances(view, status, cursor, signal);
      return { items: page.items, next: page.hasMore ? (page.items.at(-1)?.id ?? null) : null };
    },
    columns: [
      { id: 'title', label: '申請', hideable: false },
      { id: 'kind', label: '種類' },
      { id: 'applicant', label: '申請人' },
      { id: 'status', label: '狀態' },
      { id: 'step', label: '目前關卡' },
      { id: 'submittedAt', label: '送出時間' },
    ],
    pageSize: 50,
    pageSizes: [50],
    unit: '份',
  });
  readonly rows = computed(() =>
    this.table.rows().map((row) => ({
      row,
      status: flowStatus(row.status),
      submitted: formatDate(row.submittedAt),
    })),
  );
  readonly empty = computed(() =>
    this.query().status
      ? { title: '沒有符合狀態的申請', text: '換個狀態再看看。' }
      : EMPTY[this.view()],
  );

  readonly selectedId = signal<string | null>(null);
  readonly selectedIndex = computed(() =>
    this.table.rows().findIndex((row) => row.id === this.selectedId()),
  );

  constructor() {
    // 通知連結或重新整理帶入的申請：等畫面建好再開抽屜。自己改寫網址時 ID 相同，不重複開。
    this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      const id = params.get('instance');
      if (!id || id === this.selectedId()) return;
      this.selectedId.set(id);
      afterNextRender(() => this.drawer().open(), { injector: this.injector });
    });
  }

  chooseView(value: string) {
    this.view.set(value as FlowView);
  }
  openInstance(id: string) {
    this.selectedId.set(id);
    this.drawer().open();
    this.syncUrl(id);
  }
  adjacent(direction: -1 | 1) {
    const row = this.table.rows()[this.selectedIndex() + direction];
    if (row) this.openInstance(row.id);
  }
  closeInstance() {
    this.selectedId.set(null);
    this.syncUrl(null);
  }
  private syncUrl(instance: string | null) {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { instance },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }
}
