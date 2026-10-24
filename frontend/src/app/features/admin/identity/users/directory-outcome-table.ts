import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import type { DirectoryOutcomeDto, DirectoryPersonDto } from '@core/api/schema';
import { DataTable, DataTableColumn, TableState, type TableColumn } from '@shared/ui/data-table';
import { ChangeList } from '@shared/ui/change-list';
import { StatusBadge } from '@shared/ui/status-badge';
import {
  fieldChanges,
  outcomeLabel,
  outcomeName,
  outcomeNote,
  outcomeTone,
} from './directory-outcomes';

/** 匯入預覽、匯入結果與「從 AD 更新」結果共用的逐人清單：結果、會改（或已改）的欄位與需要管理員注意的事。 */
@Component({
  selector: 'nx-directory-outcome-table',
  imports: [ChangeList, DataTable, DataTableColumn, StatusBadge, TableState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<nx-data-table
    class="directory-outcomes"
    [label]="label()"
    [columns]="columns"
    [busy]="busy()"
  >
    <table>
      <colgroup>
        <col nxTableColumn="name" />
        <col nxTableColumn="outcome" />
        <col nxTableColumn="detail" />
      </colgroup>
      <thead>
        <tr>
          <th scope="col" nxTableColumn="name">人員</th>
          <th scope="col" nxTableColumn="outcome">結果</th>
          <th scope="col" nxTableColumn="detail">內容</th>
        </tr>
      </thead>
      <tbody>
        @for (item of items(); track item.sid) {
          @let name = outcomeName(item, known());
          <tr>
            <td nxTableColumn="name">
              <strong>{{ name.displayName }}</strong
              ><small class="ui-truncate" [title]="name.account">{{ name.account }}</small>
            </td>
            <td nxTableColumn="outcome">
              <div class="resource-badges">
                <nx-status-badge [tone]="outcomeTone(item.outcome)">{{
                  outcomeLabel(item.outcome)
                }}</nx-status-badge>
                @if (item.directoryDisabled && item.outcome !== 'disabled') {
                  <nx-status-badge tone="warning">AD 已停用</nx-status-badge>
                }
              </div>
            </td>
            <td nxTableColumn="detail">
              @if (item.changes.length) {
                <nx-change-list [changes]="fieldChanges(item.changes)" />
              }
              @if (outcomeNote(item); as note) {
                <small>{{ note }}</small>
              } @else if (!item.changes.length && item.path) {
                <small>{{ item.path }}</small>
              }
            </td>
          </tr>
        }
      </tbody>
    </table>
    <nx-table-state [count]="items().length" emptyTitle="沒有選取的人員" />
  </nx-data-table>`,
  // host 要是 flex 欄才把對話框 body 剩下的高度交給表格：表格內捲動、表頭固定，不必設 --table-max-height。
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      min-width: 0;
      min-height: 0;
    }
    nx-change-list + small {
      display: block;
      margin-top: var(--p-space-1);
    }
  `,
})
export class DirectoryOutcomeTable {
  readonly items = input.required<readonly DirectoryOutcomeDto[]>();
  readonly label = input('匯入結果');
  /** 選取時的資料：目錄已找不到的人用它顯示姓名。 */
  readonly known = input<ReadonlyMap<string, DirectoryPersonDto>>(new Map());
  /** 重新預覽中：保留舊結果並淡化。 */
  readonly busy = input(false);
  readonly columns: TableColumn[] = [
    { id: 'name', label: '人員', hideable: false },
    { id: 'outcome', label: '結果', hideable: false },
    { id: 'detail', label: '內容' },
  ];
  readonly outcomeName = outcomeName;
  readonly outcomeLabel = outcomeLabel;
  readonly outcomeTone = outcomeTone;
  readonly outcomeNote = outcomeNote;
  readonly fieldChanges = fieldChanges;
}
