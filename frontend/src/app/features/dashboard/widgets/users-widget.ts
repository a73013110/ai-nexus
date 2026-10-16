import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { DataTable, DataTableRow, TableState } from '@shared/ui/data-table';
import { money } from '@features/billing/billing-api';
import { DashboardStore } from '../dashboard-store';

/** Spend per user in the chosen currency; picking one narrows the whole overview to them. */
@Component({
  selector: 'nx-users-widget',
  imports: [DataTable, DataTableRow, TableState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: grid;
      gap: var(--p-space-3);
    }
    header {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: var(--p-space-2);
    }
    p {
      margin: 0;
      color: var(--secondary);
      font-size: var(--p-font-xs);
    }
    th small {
      display: block;
      color: var(--muted);
    }
    meter {
      width: 100%;
      height: 0.375rem;
    }
  `,
  template: `<header>
      <p>依目前選擇的幣別與費用類型；匯出涵蓋已套用的範圍與期間。</p>
      @if (store.appliedOwner()) {
        <button type="button" class="secondary-button" (click)="store.viewOwner('')">
          查看所有人
        </button>
      }
    </header>
    <nx-data-table label="使用者區間費用" [busy]="store.refreshing()">
      <table>
        <thead>
          <tr>
            <th scope="col">使用者</th>
            <th scope="col" class="ui-table-numeric">呼叫</th>
            <th scope="col" class="ui-table-numeric">已知費用</th>
            <th scope="col">占比</th>
            <th scope="col" class="ui-table-numeric">費用未知次數</th>
            <th scope="col"><span class="sr-only">操作</span></th>
          </tr>
        </thead>
        <tbody>
          @for (user of users(); track user.ownerId) {
            <tr
              nxTableRow
              [class.is-selected]="store.appliedOwner() === user.ownerId"
              (rowActivate)="store.viewOwner(user.ownerId)"
            >
              <th scope="row">
                <strong>{{ user.displayName }}</strong
                ><small>{{ user.account }}</small>
              </th>
              <td class="ui-table-numeric">{{ user.requests.toLocaleString() }}</td>
              <td class="ui-table-numeric">{{ money(user.amount, user.currency) }}</td>
              <td>
                <meter
                  min="0"
                  [max]="max()"
                  [value]="user.amount"
                  [attr.aria-label]="user.displayName + ' 費用占比'"
                ></meter>
              </td>
              <td class="ui-table-numeric">{{ user.unknownCalls }}</td>
              <td>
                <button
                  type="button"
                  class="quiet-button"
                  data-row-action
                  (click)="store.viewOwner(user.ownerId)"
                >
                  檢視
                </button>
              </td>
            </tr>
          }
        </tbody>
      </table>
      <nx-table-state
        [count]="users().length"
        [loading]="store.loading()"
        emptyTitle="此期間尚無符合條件的費用紀錄"
      />
    </nx-data-table>`,
})
export class UsersWidget {
  readonly store = inject(DashboardStore);
  readonly money = money;
  readonly users = computed(() => [...this.store.users()].sort((a, b) => b.amount - a.amount));
  readonly max = computed(() => Math.max(1e-8, ...this.users().map((x) => x.amount)));
}
