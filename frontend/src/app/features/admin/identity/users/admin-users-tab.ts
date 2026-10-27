import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
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
import { SortableRows, SortHandle, SortRank, type RowMove } from '@shared/ui/sortable-rows';
import { IdentityApi } from '../identity-api';
import { AdminUsersStore } from '../admin-users-store';
import { AccountReviews } from './account-reviews';
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
    AccountReviews,
    ActionMenu,
    ConfirmDialog,
    TestIdentityDialog,
    AdminUserCreateDialog,
    DirectoryImportDialog,
    DirectorySyncDialog,
    SortableRows,
    SortHandle,
    SortRank,
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
  readonly ids = computed(() => (this.store.users()?.users ?? []).map((user) => user.id));
  /** 拖曳與方向鍵只在看得到鄰居時（未搜尋）可用；寫入中不能再移動。位置編號在搜尋時也能用。 */
  readonly sortable = computed(() => this.store.reorderable() && !this.store.ordering());
  readonly lockedHint = computed(() =>
    this.store.ordering() ? '正在儲存順序…' : '清除搜尋後可拖曳；或按編號輸入位置',
  );
  readonly nameOf = (id: string) =>
    this.store.users()?.users.find((user) => user.id === id)?.displayName ?? '';
  /** 已排序與未排序的分界畫在第一個未排序的人上方；全部未排序或搜尋中不畫。 */
  firstUnsorted(user: AdminUserDto) {
    const sorted = this.store.users()?.sorted ?? 0;
    return this.store.reorderable() && sorted > 0 && user.position === sorted + 1;
  }
  moved(move: RowMove) {
    this.store.move(move.id, (this.store.users()?.offset ?? 0) + move.index + 1);
  }
  async resetOrder() {
    const sorted = this.store.users()?.sorted ?? 0;
    if (
      !(await this.confirmation().ask({
        title: '清除使用者順序',
        message: `${sorted} 位已排序的使用者會改回依姓名排列，出勤紀錄等列表跟著改變。清除前的順序會記在稽核紀錄。`,
        confirm: '清除順序',
        danger: true,
      }))
    )
      return;
    await this.store.resetOrder();
  }
  constructor() {
    // 換頁或換條件才回到頂端；同一頁重讀（拖曳排序、編輯帳號後）保留捲動位置，才能接著往下調整。
    effect(() => {
      if (this.store.request()) this.userTable()?.resetScroll();
    });
  }
  /** 只有檢視器沒有持有的部分：從 AD 更新、切換身分與移除。 */
  actions(user: AdminUserDto): MenuAction[] {
    const sync: MenuAction[] = this.session.has('identity.users.sync')
      ? [
          {
            id: 'sync',
            label: '從 AD 更新',
            icon: 'repeat',
            disabled: !user.authentication?.adEnabled,
          },
        ]
      : [];
    return [
      ...sync,
      {
        id: 'test',
        label: '以此身分測試',
        icon: 'shield',
        // 伺服器不允許巢狀切換（test_identity_nested），測試身分期間直接停用。
        disabled:
          this.session.testing() || user.id === this.session.me()?.id || user.enabled === false,
      },
      {
        id: 'delete',
        label: '刪除使用者',
        icon: 'trash',
        danger: true,
        disabled: user.id === this.session.me()?.id,
      },
    ];
  }
  /** 從選單發起、正在送出的使用者（從 AD 更新、刪除）；只有那一列的選單鈕轉圈。 */
  readonly acting = signal<string | null>(null);
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
      this.acting.set(user.id);
      try {
        await this.api.deleteUser(user.id);
        this.store.changed(0);
        this.store.toast.success('已刪除登入身分', '歷史資料已保留。');
      } catch (error) {
        this.store.actionError.set(safeMessage(error));
      } finally {
        this.acting.set(null);
      }
    }
  }
  /** 單人「從 AD 更新」直接執行，結果以 toast 告知；AD 找不到或已停用時用警告語氣並說明不會自動停用。 */
  private async sync(user: AdminUserDto) {
    this.acting.set(user.id);
    try {
      const [item] = (await this.api.syncDirectoryUsers([user.id])).items;
      this.store.changed();
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
    } finally {
      this.acting.set(null);
    }
  }
}
