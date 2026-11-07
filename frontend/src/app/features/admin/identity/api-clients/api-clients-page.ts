import { ChangeDetectionStrategy, Component, computed, inject, viewChild } from '@angular/core';
import type { ApiClientDto, IssuedApiClientKeyDto } from '@core/api/schema';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { FeaturePage } from '@core/layout/feature-page';
import { formatDate, relativeTime } from '@shared/browser/format';
import type { ActionOutcome } from '@shared/browser/action';
import { ViewScope } from '@shared/browser/view-scope';
import { ActionMenu, type MenuAction } from '@shared/ui/action-menu';
import { ConfirmDialog } from '@shared/ui/confirm-dialog';
import { DataTable, DataTableColumn, TableState } from '@shared/ui/data-table';
import { Icon } from '@shared/ui/icon';
import { NameDialog } from '@shared/ui/name-dialog';
import { Notice } from '@shared/ui/notice';
import { PendingButton } from '@shared/ui/pending-button';
import { TableHeader } from '@shared/ui/table-header';
import { localTable } from '@shared/ui/table-model';
import { ApiClientKeyDialog } from './api-client-key-dialog';
import { ApiClientsStore } from './api-clients-store';

/** 與後端 `ApiClient.NameMaxLength` 相同；伺服器仍會再檢查。 */
const NAME_MAX = 100;

/**
 * 外部系統（`identity.api-clients`）：發給其他系統呼叫 AD 帳密驗證的金鑰，一個系統一把。
 * 金鑰只在新增與重新產生後顯示一次；最近使用取自帳密驗證的稽核。
 */
@Component({
  selector: 'nx-api-clients-page',
  imports: [
    ActionMenu,
    ApiClientKeyDialog,
    ConfirmDialog,
    DataTable,
    DataTableColumn,
    FeaturePage,
    Icon,
    NameDialog,
    Notice,
    PendingButton,
    TableHeader,
    TableState,
  ],
  providers: [ApiClientsStore, ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: ':host { display: block; min-width: 0; }',
  styleUrl: './api-clients-page.scss',
  templateUrl: './api-clients-page.html',
})
export class ApiClientsPage {
  readonly session = inject(WorkspaceSession);
  readonly store = inject(ApiClientsStore);
  private readonly names = viewChild.required(NameDialog);
  private readonly keys = viewChild.required(ApiClientKeyDialog);
  private readonly confirmation = viewChild.required(ConfirmDialog);
  readonly table = localTable({
    rows: this.store.clients,
    read: {
      loading: this.store.loading,
      refreshing: this.store.refreshing,
      error: this.store.readError,
      reload: () => this.store.reload(),
    },
    columns: [
      {
        id: 'name',
        label: '名稱',
        hideable: false,
        sort: true,
        filter: 'text',
        value: (client: ApiClientDto) => client.name,
      },
      { id: 'key', label: '金鑰' },
      {
        id: 'issued',
        label: '金鑰產生於',
        sort: 'desc',
        filter: 'date',
        value: (client: ApiClientDto) => client.keyIssuedAt,
      },
      {
        id: 'used',
        label: '最近使用',
        sort: 'desc',
        filter: 'date',
        value: (client: ApiClientDto) => client.lastUsedAt ?? null,
      },
    ],
    unit: '個',
  });
  readonly rows = computed(() => {
    const busy = this.store.busy();
    return this.table.rows().map((client) => ({
      client,
      issued: formatDate(client.keyIssuedAt),
      used: client.lastUsedAt ? relativeTime(client.lastUsedAt) : '',
      usedTitle: client.lastUsedAt ? formatDate(client.lastUsedAt) : '',
      actions: [
        { id: 'rename', label: '改名', icon: 'edit', disabled: busy },
        { id: 'reissue', label: '重新產生金鑰', icon: 'repeat', disabled: busy },
        { id: 'delete', label: '刪除', icon: 'trash', danger: true, disabled: busy },
      ] satisfies MenuAction[],
    }));
  });

  create() {
    this.names().open({
      title: '新增外部系統',
      value: '',
      maxLength: NAME_MAX,
      description: '名稱用來辨識呼叫的系統，例如「公文系統」；建立後會產生金鑰。',
      save: async (name) => this.showKey(await this.saved(this.store.create(name))),
    });
  }
  action(client: ApiClientDto, id: string) {
    if (id === 'rename') this.rename(client);
    else if (id === 'reissue') void this.reissue(client);
    else if (id === 'delete') void this.remove(client);
  }
  private rename(client: ApiClientDto) {
    this.names().open({
      title: '外部系統改名',
      value: client.name,
      maxLength: NAME_MAX,
      description: '金鑰不變，對方不必更新設定。',
      save: async (name) => void (await this.saved(this.store.rename(client, name))),
    });
  }
  private async reissue(client: ApiClientDto) {
    if (
      !(await this.confirmation().ask({
        title: `重新產生「${client.name}」的金鑰？`,
        message: '舊金鑰會立即失效，對方要換上新金鑰才能繼續驗證。',
        confirm: '重新產生',
        danger: true,
      }))
    )
      return;
    const outcome = await this.store.reissue(client);
    if (outcome.ok) this.showKey(outcome.value);
    else if (outcome.error) this.store.notice.set(outcome.error);
  }
  private async remove(client: ApiClientDto) {
    if (
      !(await this.confirmation().ask({
        title: `刪除外部系統「${client.name}」？`,
        message: '金鑰會立即失效，對方無法再驗證帳密。刪除後無法復原。',
        confirm: '刪除',
        danger: true,
      }))
    )
      return;
    const outcome = await this.store.remove(client);
    if (!outcome.ok && outcome.error) this.store.notice.set(outcome.error);
  }
  /**
   * 名稱對話框的寫入：版本衝突時關閉對話框、改由頁內說明（再送也一樣被拒）；其他失敗拋出原本的錯誤，
   * 讓對話框顯示審閱過的訊息並保留輸入。略過（重讀中）時什麼都不做。
   */
  private async saved<T>(write: Promise<ActionOutcome<T>>): Promise<T | null> {
    const outcome = await write;
    if (outcome.ok) return outcome.value;
    if (outcome.stale) this.store.stale();
    else if (outcome.error) throw outcome.cause;
    return null;
  }
  /** 等名稱對話框關閉後再開：兩個 modal 同時開著時，前一個關閉會把焦點還給背後的頁面。 */
  private showKey(issued: IssuedApiClientKeyDto | null) {
    if (issued) setTimeout(() => this.keys().open(issued));
  }
}
