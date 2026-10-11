import {
  ChangeDetectionStrategy,
  Component,
  effect,
  inject,
  input,
  output,
  viewChild,
} from '@angular/core';
import type { AdminUserDto } from '../../../core/api/schema';
import { WorkspaceSession } from '../../../core/auth/workspace-session';
import { safeMessage } from '../../../core/errors/safe-errors';
import { formatBytes, formatNumber } from '../../../shared/browser/format';
import { ActionMenu, type MenuAction } from '../../../shared/ui/action-menu';
import { ConfirmDialog } from '../../../shared/ui/confirm-dialog';
import {
  DataTable,
  DataTableColumn,
  DataTableRow,
  TablePagination,
  type TableColumn,
} from '../../../shared/ui/data-table';
import { EmptyState } from '../../../shared/ui/empty-state';
import { Icon } from '../../../shared/ui/icon';
import { SearchField } from '../../../shared/ui/search-field';
import { StatusBadge } from '../../../shared/ui/status-badge';
import { AdminApi } from '../admin-api';
import { AdminStore } from '../admin-store';
import { TestIdentityDialog } from './test-identity-dialog';

@Component({
  selector: 'nx-admin-users-tab',
  imports: [
    DataTable,
    DataTableColumn,
    DataTableRow,
    TablePagination,
    EmptyState,
    Icon,
    SearchField,
    StatusBadge,
    ActionMenu,
    ConfirmDialog,
    TestIdentityDialog,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './access-tabs.scss',
  templateUrl: './admin-users-tab.html',
})
export class AdminUsersTab {
  readonly session = inject(WorkspaceSession);
  readonly store = inject(AdminStore);
  private readonly api = inject(AdminApi);
  /** The user whose activity is open beside the list. */
  readonly selectedId = input<string | null>(null);
  readonly edit = output<AdminUserDto | undefined>();
  readonly inspect = output<AdminUserDto>();
  private readonly userTable = viewChild<DataTable>('userTable');
  private readonly confirmation = viewChild.required(ConfirmDialog);
  private readonly testDialog = viewChild.required(TestIdentityDialog);
  readonly userColumns: TableColumn[] = [
    { id: 'name', label: '使用者', hideable: false },
    { id: 'authentication', label: '登入與狀態' },
    { id: 'roles', label: '角色' },
    { id: 'usage', label: '30 天用量' },
    { id: 'storage', label: '原檔容量' },
  ];
  readonly bytes = formatBytes;
  readonly format = formatNumber;
  constructor() {
    effect(() => {
      if (this.store.users()) this.userTable()?.resetScroll();
    });
  }
  actions(user: AdminUserDto): MenuAction[] {
    const testing = !!this.session.auth.session()?.testing;
    return [
      { id: 'edit', label: '編輯使用者與登入方式', icon: 'edit', disabled: testing },
      {
        id: 'test',
        label: '以此身分測試',
        icon: 'shield',
        disabled: testing || user.id === this.session.me()?.id || user.enabled === false,
      },
      {
        id: 'delete',
        label: '刪除使用者',
        icon: 'trash',
        danger: true,
        disabled: testing || user.id === this.session.me()?.id,
      },
    ];
  }
  async userAction(action: string, user: AdminUserDto) {
    if (action === 'edit') this.edit.emit(user);
    if (action === 'test') this.testDialog().open(user);
    if (action === 'delete') {
      if (
        !(await this.confirmation().ask({
          title: '刪除使用者：' + user.displayName,
          message:
            '此使用者將無法登入，既有工作階段會失效。對話、附件、用量與稽核仍會保留，登入帳號也會保留，避免他人接管。',
          confirm: '刪除使用者',
          danger: true,
        }))
      )
        return;
      try {
        await this.api.deleteUser(user.id);
        this.store.readUsers(0);
        this.store.notice.set('已刪除登入身分，歷史資料已保留。');
      } catch (error) {
        this.store.actionError.set(safeMessage(error));
      }
    }
  }
}
