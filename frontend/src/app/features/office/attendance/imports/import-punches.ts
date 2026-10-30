import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { formatDate } from '@shared/browser/format';
import { DataTable, DataTableColumn, TableState } from '@shared/ui/data-table';
import { StatusBadge } from '@shared/ui/status-badge';
import { TableHeader } from '@shared/ui/table-header';
import { endpointTable } from '@shared/ui/table-model';
import { AttendanceApi } from '../attendance-api';
import { punchCodes } from '../attendance-presentation';

/**
 * 一次匯入寫入的刷卡（匯入詳情的主要內容），依刷卡時間由早到晚；排序、篩選與分頁交給 `endpointTable`，一個每日檔通常一次讀完、在本機處理。
 * 只給這個檔最近一次成功的匯入（`isCurrent`）用：刷卡依檔案保存，較舊的匯入內容已被取代，後端回 409。
 * 持有人是讀取當下依持有期間歸戶的結果，機器與事件代號原樣列出、不解讀。
 * 表格吃抽屜剩下的高度、在框內捲動：使用處要在 host 與外層區塊堆疊加 `ui-table-fill`。
 */
@Component({
  selector: 'nx-import-punches',
  imports: [DataTable, DataTableColumn, StatusBadge, TableHeader, TableState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    .import-punches-table table {
      table-layout: fixed;
      min-width: 28rem;
    }
    .import-punches-time-column {
      width: 10.5rem;
    }
    .import-punches-codes-column {
      width: 5rem;
    }
    .import-punches-card {
      font-family: var(--font-code);
    }
  `,
  template: `
    <nx-data-table
      class="import-punches-table"
      label="匯入的刷卡"
      [table]="table"
      [stacked]="true"
      preferenceKey="office-attendance-import-punches"
    >
      <div table-toolbar class="ui-table-toolbar">
        <h3>匯入的刷卡</h3>
      </div>
      <table>
        <caption class="sr-only">
          這次匯入寫入的刷卡，依刷卡時間由早到晚；欄位標題可排序與篩選。
        </caption>
        <colgroup>
          <col nxTableColumn="punchedAt" class="import-punches-time-column" />
          <col nxTableColumn="cardNumber" />
          <col nxTableColumn="holder" />
          <col nxTableColumn="codes" class="import-punches-codes-column" />
        </colgroup>
        <thead>
          <tr>
            <th nxHeader="punchedAt"></th>
            <th nxHeader="cardNumber"></th>
            <th nxHeader="holder"></th>
            <th nxHeader="codes"></th>
          </tr>
        </thead>
        <tbody>
          @for (item of rows(); track $index) {
            <tr>
              <td nxTableColumn="punchedAt">
                <time [attr.datetime]="item.punch.punchedAt">{{ item.time }}</time>
              </td>
              <th scope="row" nxTableColumn="cardNumber" class="import-punches-card ui-truncate">
                {{ item.punch.cardNumber }}
              </th>
              <td nxTableColumn="holder" data-label="持有人">
                @if (item.punch.unattributed) {
                  <nx-status-badge tone="warning">無持有人</nx-status-badge>
                } @else {
                  <span class="ui-truncate" [title]="item.punch.holder">{{
                    item.punch.holder
                  }}</span>
                }
              </td>
              <td nxTableColumn="codes" data-label="代號" [title]="item.codesTitle">
                {{ item.codes }}
              </td>
            </tr>
          }
        </tbody>
      </table>
      <nx-table-state
        loadingText="正在載入刷卡…"
        errorTitle="無法取得刷卡"
        emptyTitle="沒有符合條件的刷卡"
        emptyText="這個檔沒有刷卡，或試著移除欄位篩選。"
      />
    </nx-data-table>
  `,
})
export class ImportPunches {
  /** 匯入紀錄的 ID；換一筆時從第一頁重讀。 */
  readonly importId = input.required<string>();
  private readonly api = inject(AttendanceApi);
  readonly table = endpointTable({
    feature: 'attendance.imports',
    params: this.importId,
    load: (id, table, signal) => this.api.importPunches(id, table, signal),
    columns: [
      { id: 'punchedAt', label: '刷卡時間', hideable: false, sort: true, filter: 'date' },
      { id: 'cardNumber', label: '卡號', hideable: false, sort: true, filter: 'text' },
      { id: 'holder', label: '持有人', sort: true, filter: 'text' },
      { id: 'codes', label: '機器與事件代號', header: '代號' },
    ],
    sort: { column: 'punchedAt', direction: 'asc' },
  });
  readonly rows = computed(() =>
    this.table
      .rows()
      .map((punch) => ({ punch, time: formatDate(punch.punchedAt), ...punchCodes(punch) })),
  );
}
