import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import type { FeatureId } from '@core/api/catalog';
import type { PunchDto, TablePageOfPunchDto } from '@core/api/schema';
import { formatDate } from '@shared/browser/format';
import { DataTable, DataTableColumn, TableState } from '@shared/ui/data-table';
import { StatusBadge } from '@shared/ui/status-badge';
import { TableHeader } from '@shared/ui/table-header';
import {
  type ReadTableModel,
  type TableColumnDef,
  type TableRequestQuery,
  endpointTable,
} from '@shared/ui/table-model';
import { punchCodes } from '../attendance-presentation';

export type PunchTableModel = ReadTableModel<PunchDto, TablePageOfPunchDto>;

const COLUMNS: readonly TableColumnDef<PunchDto>[] = [
  { id: 'punchedAt', label: '刷卡時間', hideable: false, sort: true, filter: 'date' },
  { id: 'cardNumber', label: '卡號', hideable: false, sort: true, filter: 'text' },
  { id: 'holder', label: '持有人', sort: true, filter: 'text' },
  { id: 'codes', label: '機器與事件代號', header: '代號' },
];

/**
 * 刷卡表格的模型，排序、篩選與分頁交給 `endpointTable`（後端共用同一套欄位，資料在門檻內時在本機處理）。
 * `card`：一張卡的刷卡（卡片詳情）不列卡號、新到舊；否則（匯入明細）列卡號、依時間由早到晚。
 * `params` 換成新值就從第一頁重讀，`undefined` 不讀。
 */
export function punchTable<P>(options: {
  feature: FeatureId;
  params: () => P | undefined;
  load: (params: P, table: TableRequestQuery, signal: AbortSignal) => Promise<TablePageOfPunchDto>;
  card?: boolean;
}): PunchTableModel {
  return endpointTable({
    feature: options.feature,
    params: options.params,
    load: options.load,
    columns: options.card ? COLUMNS.filter((column) => column.id !== 'cardNumber') : COLUMNS,
    sort: { column: 'punchedAt', direction: options.card ? 'desc' : 'asc' },
  });
}

/**
 * 原始刷卡與歸戶結果的表格，卡片詳情與匯入詳情共用；模型沒有卡號欄時不畫卡號。
 * 持有人是讀取當下依持有期間歸戶的結果，機器與事件代號原樣列出、不解讀。
 * 放在抽屜最後、吃剩下的高度並在框內捲動：host 自帶 `ui-table-fill`，外層區塊堆疊（`.ui-drawer-sections`）也要加。
 */
@Component({
  selector: 'nx-punch-table',
  imports: [DataTable, DataTableColumn, StatusBadge, TableHeader, TableState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'ui-table-fill' },
  styles: `
    .punch-table table {
      table-layout: fixed;
      min-width: 24rem;
    }
    .punch-table.with-card table {
      min-width: 28rem;
    }
    .punch-table-time-column {
      width: 10.5rem;
    }
    .punch-table-codes-column {
      width: 5rem;
    }
    .punch-table-card {
      font-family: var(--font-code);
    }
  `,
  template: `
    <nx-data-table
      class="punch-table"
      [class.with-card]="withCard()"
      [label]="title()"
      [table]="table()"
      [stacked]="true"
      [preferenceKey]="preferenceKey()"
    >
      <div table-toolbar class="ui-table-toolbar">
        <h3>{{ title() }}</h3>
      </div>
      <table>
        <caption class="sr-only">
          {{
            title()
          }}；欄位標題可排序與篩選。
        </caption>
        <colgroup>
          <col nxTableColumn="punchedAt" class="punch-table-time-column" />
          @if (withCard()) {
            <col nxTableColumn="cardNumber" />
          }
          <col nxTableColumn="holder" />
          <col nxTableColumn="codes" class="punch-table-codes-column" />
        </colgroup>
        <thead>
          <tr>
            <th nxHeader="punchedAt"></th>
            @if (withCard()) {
              <th nxHeader="cardNumber"></th>
            }
            <th nxHeader="holder"></th>
            <th nxHeader="codes"></th>
          </tr>
        </thead>
        <tbody>
          @for (item of rows(); track item.punch.id) {
            <tr>
              <th scope="row" nxTableColumn="punchedAt">
                <time [attr.datetime]="item.punch.punchedAt">{{ item.time }}</time>
              </th>
              @if (withCard()) {
                <td
                  nxTableColumn="cardNumber"
                  class="punch-table-card ui-truncate"
                  data-label="卡號"
                >
                  {{ item.punch.cardNumber }}
                </td>
              }
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
        [emptyText]="emptyText()"
      />
    </nx-data-table>
  `,
})
export class PunchTable {
  readonly table = input.required<PunchTableModel>();
  /** 工具列標題，也是表格的無障礙名稱。 */
  readonly title = input.required<string>();
  /** 欄位顯示偏好的鍵；兩處的欄位不同，各自保存。 */
  readonly preferenceKey = input.required<string>();
  readonly emptyText = input('');
  protected readonly withCard = computed(() => !!this.table().column('cardNumber'));
  protected readonly rows = computed(() =>
    this.table()
      .rows()
      .map((punch) => ({ punch, time: formatDate(punch.punchedAt), ...punchCodes(punch) })),
  );
}
