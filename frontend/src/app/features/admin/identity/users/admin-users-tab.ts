import {
  ChangeDetectionStrategy,
  Component,
  effect,
  inject,
  input,
  output,
  viewChild,
} from '@angular/core';
import type { AdminUserDto } from '@core/api/schema';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { safeMessage } from '@core/errors/safe-errors';
import { formatBytes, formatNumber } from '@shared/browser/format';
import { ActionMenu, type MenuAction } from '@shared/ui/action-menu';
import { ConfirmDialog } from '@shared/ui/confirm-dialog';
import {
  DataTable,
  DataTableColumn,
  DataTableRow,
  TablePagination,
  TableState,
  type TableColumn,
} from '@shared/ui/data-table';
import { Icon } from '@shared/ui/icon';
import { SearchField } from '@shared/ui/search-field';
import { IdentityApi } from '../identity-api';
import { AdminUsersStore } from '../admin-users-store';
import { AdminUserCreateDialog } from './admin-user-create-dialog';
import { emailState, signInMethods } from './user-account-fields';
import { AccessStatus } from '@features/admin/access-control/roles/access-status';
import { TestIdentityDialog } from './test-identity-dialog';
import { DirectoryImportDialog } from './directory-import-dialog';
import { DirectorySyncDialog } from './directory-sync-dialog';
import { describeChange, outcomeLabel, outcomeNote, writesUser } from './directory-outcomes';

@Component({
  selector: 'nx-admin-users-tab',
  imports: [
    DataTable,
    DataTableColumn,
    DataTableRow,
    TablePagination,
    TableState,
    Icon,
    SearchField,
    AccessStatus,
    ActionMenu,
    ConfirmDialog,
    TestIdentityDialog,
    AdminUserCreateDialog,
    DirectoryImportDialog,
    DirectorySyncDialog,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: '../../../../shared/ui/management-table-page.scss',
  templateUrl: './admin-users-tab.html',
})
export class AdminUsersTab {
  readonly session = inject(WorkspaceSession);
  readonly store = inject(AdminUsersStore);
  private readonly api = inject(IdentityApi);
  /** 在檢視器中開啟的使用者，帳號、角色與政策都在那裡編輯。 */
  readonly selectedId = input<string | null>(null);
  readonly inspect = output<AdminUserDto>();
  private readonly userTable = viewChild<DataTable>('userTable');
  private readonly confirmation = viewChild.required(ConfirmDialog);
  private readonly testDialog = viewChild.required(TestIdentityDialog);
  readonly createDialog = viewChild.required(AdminUserCreateDialog);
  /** 只有具備對應動作授權時才建立，所以是選用的。 */
  readonly importDialog = viewChild(DirectoryImportDialog);
  readonly syncDialog = viewChild(DirectorySyncDialog);
  readonly userColumns: TableColumn[] = [
    { id: 'name', label: '使用者', hideable: false },
    { id: 'email', label: '通知信箱' },
    { id: 'authentication', label: '登入與狀態' },
    { id: 'roles', label: '角色' },
    { id: 'usage', label: '30 天用量' },
    { id: 'storage', label: '原檔容量' },
  ];
  readonly bytes = formatBytes;
  readonly format = formatNumber;
  readonly methods = signInMethods;
  readonly emailState = emailState;
  constructor() {
    effect(() => {
      if (this.store.users()) this.userTable()?.resetScroll();
    });
  }
  /** 只有檢視器沒有持有的部分：從 AD 更新、切換身分與移除。 */
  actions(user: AdminUserDto): MenuAction[] {
    const testing = this.session.testing();
    const sync: MenuAction[] = this.session.has('identity.users.sync')
      ? [
          {
            id: 'sync',
            label: '從 AD 更新',
            icon: 'repeat',
            disabled: testing || !user.authentication?.adEnabled,
          },
        ]
      : [];
    return [
      ...sync,
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
    if (action === 'sync') await this.sync(user);
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
        this.store.toast.success('已刪除登入身分', '歷史資料已保留。');
      } catch (error) {
        this.store.actionError.set(safeMessage(error));
      }
    }
  }
  /** 單人「從 AD 更新」直接執行，結果以 toast 告知；AD 找不到或已停用時用警告語氣並說明不會自動停用。 */
  private async sync(user: AdminUserDto) {
    try {
      const [item] = (await this.api.syncDirectoryUsers([user.id])).items;
      this.store.readUsers();
      if (!item) return;
      if (writesUser(item))
        this.store.toast.success(
          '已從 AD 更新：' + user.displayName,
          item.changes.map(describeChange).join('；'),
        );
      else if (item.outcome === 'unchanged' && !outcomeNote(item))
        this.store.toast.info(user.displayName + ' 已與 AD 一致');
      else
        this.store.toast.show({
          tone: 'warning',
          title: user.displayName + '：' + outcomeLabel(item.outcome),
          description: outcomeNote(item) || undefined,
        });
    } catch (error) {
      this.store.actionError.set(safeMessage(error));
    }
  }
}
