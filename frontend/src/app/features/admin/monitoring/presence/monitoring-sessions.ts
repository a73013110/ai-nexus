import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  input,
  model,
  output,
  signal,
} from '@angular/core';
import { DataTable, DataTableRow, TableState } from '@shared/ui/data-table';
import { StatusBadge } from '@shared/ui/status-badge';
import { SearchField } from '@shared/ui/search-field';
import { Select } from '@shared/ui/select';
import { TableHeader } from '@shared/ui/table-header';
import { localTable } from '@shared/ui/table-model';
import type { OnlineSession } from '@core/api/schema';
import { presenceState, monitoringTime, monitoringFeatureNames } from '../monitoring-format';

@Component({
  selector: 'nx-monitoring-sessions',
  imports: [DataTable, DataTableRow, StatusBadge, SearchField, Select, TableHeader, TableState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './monitoring-sessions.html',
  styles: ':host { display: grid; gap: var(--p-space-3); min-width: 0; }',
})
export class MonitoringSessions {
  readonly sessions = input.required<OnlineSession[]>();
  /** 在檢視器中開啟的工作階段，與任何開啟的列一樣標示。 */
  readonly selectedId = input<string | null>(null);
  /** 連線中斷、顯示的是舊快照：表格淡化。 */
  readonly busy = input(false);
  readonly selected = output<OnlineSession>();
  /** 目前功能；與上方「依目前功能篩選」的清單同步，所以留在表格外。 */
  readonly feature = model('all');
  readonly query = signal('');
  readonly featureName = monitoringFeatureNames();
  readonly stateName = presenceState;
  readonly time = monitoringTime;
  readonly featureOptions = computed(() => [
    { value: 'all', label: '所有功能' },
    ...[...new Set(this.sessions().map((session) => session.feature))]
      .sort()
      .map((id) => ({ value: id, label: this.featureName(id) })),
  ]);
  /** 搜尋與功能是表格外的條件：跨多個欄位，或由上方清單控制；在線狀態、排序與分頁交給表格。 */
  private readonly matched = computed(() => {
    const query = this.query().trim().toLocaleLowerCase();
    const feature = this.feature();
    return this.sessions().filter(
      (session) =>
        (feature === 'all' || session.feature === feature) &&
        (!query ||
          [session.displayName, session.account, session.address, session.browser, session.device]
            .join(' ')
            .toLocaleLowerCase()
            .includes(query)),
    );
  });
  readonly table = localTable({
    rows: this.matched,
    columns: [
      { id: 'user', label: '使用者', sort: true, value: (session) => session.displayName },
      { id: 'device', label: '裝置／連線位址' },
      { id: 'feature', label: '目前功能' },
      {
        id: 'state',
        label: '狀態',
        filter: {
          options: [
            { value: 'active', label: '使用中' },
            { value: 'idle', label: '閒置' },
            { value: 'background', label: '背景分頁' },
          ],
        },
      },
      { id: 'requests', label: '請求數', sort: 'desc', numeric: true },
      { id: 'lastAction', label: '最近操作' },
    ],
    sort: { column: 'user', direction: 'asc' },
    pageSize: 10,
    pageSizes: [10],
  });
  constructor() {
    // 搜尋或換功能是新的一批資料：回到第一頁。
    effect(() => {
      this.matched();
      this.table.goTo(0);
    });
  }
}
