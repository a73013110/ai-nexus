import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { apiResource } from '@core/api/api-resource';
import type { RosterExclusionDto } from '@core/api/schema';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { formatDate } from '@shared/browser/format';
import { ViewScope } from '@shared/browser/view-scope';
import { ActionMenu, type MenuAction } from '@shared/ui/action-menu';
import { ConfirmDialog } from '@shared/ui/confirm-dialog';
import { DataTable, DataTableColumn, TableState, type TableColumn } from '@shared/ui/data-table';
import { Icon } from '@shared/ui/icon';
import { Notice } from '@shared/ui/notice';
import { AttendanceApi } from '../attendance-api';
import { attendanceProblem } from '../attendance-presentation';
import { ExclusionDialog } from './exclusion-dialog';

/**
 * 不列入出勤的名單（卡號對應頁的一個檢視）：HR 看全公司出勤時隱藏的人，例如董事、外派或兼任；
 * 沒有帳號的資料（只有姓名的持有人、未歸戶卡號）依卡號列入名單。
 * 它不是權限，本人與主管照常看得到；讀取跟卡號對應同一頁授權，新增、修改備註與恢復列入另需 `.maintain`。
 * 名單是少數人，不分頁、不另設查詢。
 */
@Component({
  selector: 'nx-roster-exclusions',
  imports: [
    ActionMenu,
    ConfirmDialog,
    DataTable,
    DataTableColumn,
    ExclusionDialog,
    Icon,
    Notice,
    TableState,
  ],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      gap: var(--page-gap, var(--p-space-4));
      min-width: 0;
    }
    .exclusions-name small {
      display: block;
      color: var(--secondary);
      font-size: var(--p-font-xs);
    }
    .exclusions-name-column {
      width: 14rem;
    }
    .exclusions-date-column {
      width: 8rem;
    }
    .exclusions-action-column {
      width: 3.5rem;
    }
  `,
  template: `@if (actionError()) {
      <nx-notice
        tone="danger"
        [message]="actionError()"
        [dismissible]="true"
        (dismissed)="actionError.set('')"
      />
    }
    <nx-data-table
      class="exclusions-table"
      label="不列入出勤"
      [busy]="refreshing()"
      [columns]="columns"
      [stacked]="true"
      preferenceKey="office-attendance-exclusions"
    >
      <div table-toolbar class="ui-table-toolbar">
        <h2>
          不列入出勤
          @if (exclusions()) {
            <span class="ui-count-label">{{ rows().length }} 人</span>
          }
        </h2>
      </div>
      @if (canMaintain()) {
        <button
          table-actions
          type="button"
          class="quiet-button exclusions-add"
          [disabled]="session.testing()"
          [title]="session.testing() ? session.testingNote() : ''"
          (click)="dialog().create()"
        >
          <nx-icon name="plus" />加入
        </button>
      }
      <table>
        <caption class="sr-only">
          不列入出勤的人與卡號，依名稱排序，沒有持有人的卡號在最後。
        </caption>
        <colgroup>
          <col nxTableColumn="name" class="exclusions-name-column" />
          <col nxTableColumn="note" />
          <col nxTableColumn="since" class="exclusions-date-column" />
          @if (canMaintain()) {
            <col class="exclusions-action-column" />
          }
        </colgroup>
        <thead>
          <tr>
            <th scope="col" nxTableColumn="name">姓名</th>
            <th scope="col" nxTableColumn="note">原因</th>
            <th scope="col" nxTableColumn="since">設定日期</th>
            @if (canMaintain()) {
              <th scope="col"><span class="sr-only">動作</span></th>
            }
          </tr>
        </thead>
        <tbody>
          @for (row of rows(); track row.key) {
            <tr>
              <th scope="row" nxTableColumn="name" class="exclusions-name">
                {{ row.item.displayName }}<small>{{ row.identity }}</small>
              </th>
              <td nxTableColumn="note" data-label="原因">{{ row.item.note || '—' }}</td>
              <td nxTableColumn="since" data-label="設定日期">{{ row.since }}</td>
              @if (canMaintain()) {
                <td data-stack="end">
                  <nx-action-menu
                    [label]="row.item.displayName + ' 的操作'"
                    [items]="actions()"
                    [busy]="working() === row.key"
                    (action)="act(row.item, $event)"
                  />
                </td>
              }
            </tr>
          }
        </tbody>
      </table>
      <nx-table-state
        [count]="rows().length"
        [loading]="loading()"
        [error]="error()"
        loadingText="正在載入不列入出勤的名單…"
        errorTitle="無法取得不列入出勤的名單"
        emptyTitle="每個持卡人都列入出勤"
        emptyText="HR 看全公司出勤時不想看到的人（例如董事、外派）或卡號，可在這裡加入。"
        (retry)="reload()"
      />
    </nx-data-table>
    <nx-exclusion-dialog (saved)="reload()" />
    <nx-confirm-dialog />`,
})
export class RosterExclusions {
  readonly session = inject(WorkspaceSession);
  private readonly api = inject(AttendanceApi);
  private readonly view = inject(ViewScope);
  readonly dialog = viewChild.required(ExclusionDialog);
  private readonly confirmation = viewChild.required(ConfirmDialog);
  readonly canMaintain = computed(() => this.session.has('attendance.cards.maintain'));
  private readonly read = apiResource({
    feature: 'attendance.cards',
    loader: (_: true, signal) => this.api.exclusions(signal),
  });
  readonly exclusions = this.read.value;
  readonly loading = this.read.loading;
  readonly refreshing = this.read.refreshing;
  readonly error = this.read.error;
  /** 正在恢復列入的列（使用者 ID 或 `card:卡號`）；只讓那一列的選單轉圈。 */
  readonly working = signal<string | null>(null);
  readonly actionError = signal('');
  readonly columns: TableColumn[] = [
    { id: 'name', label: '姓名', hideable: false },
    { id: 'note', label: '原因' },
    { id: 'since', label: '設定日期' },
  ];
  readonly rows = computed(() =>
    (this.exclusions() ?? []).map((item) => ({
      item,
      key: item.userId ?? 'card:' + item.cardNumber,
      // 使用者寫帳號；卡號列（姓名持有人或未歸戶卡號）寫卡號。
      identity: item.userId ? item.account : `卡號 ${item.cardNumber}`,
      since: formatDate(item.createdAt),
    })),
  );
  readonly actions = computed<MenuAction[]>(() => {
    const disabled = this.session.testing() || !!this.working();
    return [
      { id: 'edit', label: '編輯原因', icon: 'edit', disabled },
      { id: 'restore', label: '恢復列入出勤', icon: 'restore', disabled },
    ];
  });

  reload() {
    this.read.reload();
  }
  act(item: RosterExclusionDto, action: string) {
    if (action === 'edit')
      this.dialog().edit(
        item.userId
          ? { userId: item.userId, displayName: item.displayName, account: item.account ?? '' }
          : { cardNumber: item.cardNumber!, displayName: item.displayName },
        item.note,
      );
    else if (action === 'restore') void this.restore(item);
  }
  private async restore(item: RosterExclusionDto) {
    if (
      !(await this.confirmation().ask({
        title: `恢復列入 ${item.displayName}？`,
        message: item.userId
          ? 'HR 看全公司出勤時會再列出這個人。'
          : 'HR 看全公司出勤時會再列出這張卡上沒有帳號的資料。',
        confirm: '恢復列入',
      }))
    )
      return;
    const guard = this.view.guard();
    const key = item.userId ?? 'card:' + item.cardNumber;
    this.working.set(key);
    this.actionError.set('');
    try {
      await this.api.restore(
        item.userId ? { userId: item.userId } : { cardNumber: item.cardNumber! },
      );
      if (guard()) this.reload();
    } catch (error) {
      if (guard()) this.actionError.set(attendanceProblem(error));
    } finally {
      if (guard()) this.working.set(null);
    }
  }
}
