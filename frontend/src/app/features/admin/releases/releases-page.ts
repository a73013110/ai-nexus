import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { apiResource } from '@core/api/api-resource';
import type { SchemaChangeDto } from '@core/api/schema';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { safeMessage } from '@core/errors/safe-errors';
import { FeaturePage } from '@core/layout/feature-page';
import { attachmentName, downloadBlob } from '@shared/browser/download';
import { formatDate } from '@shared/browser/format';
import { ViewScope } from '@shared/browser/view-scope';
import { DataTable, DataTableColumn, DataTableRow, TableState } from '@shared/ui/data-table';
import { DetailDrawer } from '@shared/ui/detail-drawer';
import { Icon } from '@shared/ui/icon';
import { Notice } from '@shared/ui/notice';
import { PendingButton } from '@shared/ui/pending-button';
import { RefreshButton } from '@shared/ui/refresh-button';
import { StatGrid, StatTile } from '@shared/ui/stat-tile';
import { StatusBadge } from '@shared/ui/status-badge';
import { TableHeader } from '@shared/ui/table-header';
import { localTable } from '@shared/ui/table-model';
import { ReleasesApi } from './releases-api';
import {
  CHANGE_NAMES,
  OBJECT_TYPE_NAMES,
  changeTone,
  compatibility,
  objectName,
  shortRevision,
} from './releases-format';
import { SchemaDiff } from './schema-diff';

const options = (names: Record<string, string>) =>
  Object.entries(names).map(([value, label]) => ({ value, label }));

/**
 * 版本頁（ADR 0028）：程式與資料庫的版本對照、正式機上不在這一版裡的結構異動，以及部署歷史。
 * 異動清單由伺服器比對雜湊、只傳回有差異的物件；定義本文在打開差異時才讀。
 */
@Component({
  selector: 'nx-releases-page',
  imports: [
    DataTable,
    DataTableColumn,
    DataTableRow,
    DetailDrawer,
    FeaturePage,
    Icon,
    Notice,
    PendingButton,
    RefreshButton,
    SchemaDiff,
    StatGrid,
    StatTile,
    StatusBadge,
    TableHeader,
    TableState,
  ],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './releases-page.html',
  styleUrl: './releases-page.scss',
})
export class ReleasesPage {
  readonly session = inject(WorkspaceSession);
  private readonly api = inject(ReleasesApi);
  private readonly view = inject(ViewScope);
  private readonly drawer = viewChild(DetailDrawer);
  readonly status = apiResource({
    feature: 'releases',
    loader: (_, signal) => this.api.status(signal),
  });
  readonly changes = apiResource({
    feature: 'releases',
    loader: (_, signal) => this.api.changes(signal),
  });
  readonly deployments = apiResource({
    feature: 'releases',
    loader: (_, signal) => this.api.deployments(signal),
  });
  readonly verdict = computed(() => {
    const status = this.status.value();
    return status ? compatibility(status) : null;
  });
  readonly changeTable = localTable({
    rows: () => this.changes.value(),
    read: this.changes,
    columns: [
      {
        id: 'object',
        label: '物件',
        hideable: false,
        sort: true,
        filter: 'text',
        value: objectName,
      },
      { id: 'type', label: '種類', filter: { options: options(OBJECT_TYPE_NAMES) } },
      { id: 'change', label: '異動', filter: { options: options(CHANGE_NAMES) } },
      { id: 'changedAt', label: '時間', sort: 'desc', time: true },
    ],
    sort: { column: 'changedAt', direction: 'desc' },
    pageSize: 25,
    navigated: () => this.drawer()?.close(),
  });
  readonly deploymentTable = localTable({
    rows: () => this.deployments.value(),
    read: this.deployments,
    columns: [
      { id: 'deployedAt', label: '時間', hideable: false, sort: 'desc', time: true },
      {
        id: 'component',
        label: '對象',
        filter: {
          options: [
            { value: 'app', label: '程式' },
            { value: 'database', label: '資料庫' },
          ],
        },
      },
      { id: 'version', label: '版本', sort: true },
      { id: 'revision', label: '修訂' },
      { id: 'host', label: '主機' },
    ],
    sort: { column: 'deployedAt', direction: 'desc' },
    pageSize: 25,
  });
  readonly selected = signal<SchemaChangeDto | null>(null);
  readonly detail = apiResource({
    feature: 'releases',
    params: () => this.selected() ?? undefined,
    loader: (change, signal) => this.api.change(change.schemaName, change.name, signal),
  });
  readonly exporting = signal(false);
  readonly exportError = signal('');
  readonly changeNames = CHANGE_NAMES;
  readonly typeNames = OBJECT_TYPE_NAMES;
  readonly changeTone = changeTone;
  readonly objectName = objectName;
  readonly shortRevision = shortRevision;
  readonly date = formatDate;

  refresh() {
    this.status.reload();
    this.changes.reload();
    this.deployments.reload();
  }
  inspect(change: SchemaChangeDto) {
    this.selected.set(change);
    this.drawer()?.open();
  }
  closeDetail() {
    this.selected.set(null);
  }
  async export() {
    if (this.exporting() || !this.changes.value()?.length) return;
    const guard = this.view.guard();
    this.exporting.set(true);
    this.exportError.set('');
    try {
      const response = await this.api.export();
      const name = attachmentName(response, { title: 'ai-nexus-schema-changes', extension: 'sql' });
      const blob = await response.blob();
      if (guard()) downloadBlob(blob, name.title, name.extension);
    } catch (error) {
      if (guard()) this.exportError.set(safeMessage(error));
    } finally {
      if (guard()) this.exporting.set(false);
    }
  }
}
