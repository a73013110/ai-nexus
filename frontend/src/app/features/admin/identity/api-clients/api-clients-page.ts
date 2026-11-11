import { ChangeDetectionStrategy, Component, computed, inject, viewChild } from '@angular/core';
import { Router } from '@angular/router';
import { routeOf } from '@core/api/catalog';
import type { ApiClientDto, IssuedApiClientKeyDto } from '@core/api/schema';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { FeaturePage } from '@core/layout/feature-page';
import { formatDate, relativeTime } from '@shared/browser/format';
import type { ActionOutcome } from '@shared/browser/action';
import { ViewScope } from '@shared/browser/view-scope';
import { ActionMenu, type MenuAction } from '@shared/ui/action-menu';
import { ConfirmDialog } from '@shared/ui/confirm-dialog';
import { DataTable, DataTableColumn, TableState } from '@shared/ui/data-table';
import { Field } from '@shared/ui/field';
import { Icon } from '@shared/ui/icon';
import { Notice } from '@shared/ui/notice';
import { QueryBar, QueryField } from '@shared/ui/query-bar';
import { queryState } from '@shared/ui/query-state';
import { StatusBadge } from '@shared/ui/status-badge';
import { TableHeader } from '@shared/ui/table-header';
import { localTable } from '@shared/ui/table-model';
import { ViewSwitch } from '@shared/ui/view-switch';
import { ApiClientDialog } from './api-client-dialog';
import { ApiClientGuide } from './api-client-guide';
import { ApiClientKeyDialog } from './api-client-key-dialog';
import {
  API_CLIENT_SEARCH_MAX,
  API_CLIENT_STATUSES,
  apiClientCallsQuery,
  apiClientsQueryOptions,
  matchesApiClient,
} from './api-clients-query';
import { ApiClientsStore } from './api-clients-store';

/** 金鑰用超過這麼久就在產生日期上提示更換；不強制，換版時程由雙方約定。 */
const KEY_STALE_DAYS = 365;
const DAY_MS = 86_400_000;
const count = new Intl.NumberFormat('zh-TW');

/**
 * 外部系統（`identity.api-clients`）：發給其他系統呼叫 AD 帳密驗證的金鑰，一個系統一把。
 * 金鑰只在新增與重新產生後顯示一次，連同串接範例；頁首的「串接說明」是給對方開發者的呼叫方式與範例。
 * 最近使用與近 30 天次數取自帳密驗證的稽核，有活動稽核權限時列選單可直接查看。
 * 清單只有個位數、一次讀完：狀態與搜尋在查詢列（寫進網址），排序與欄位篩選在表格，都在本機處理。
 */
@Component({
  selector: 'nx-api-clients-page',
  imports: [
    ActionMenu,
    ApiClientDialog,
    ApiClientGuide,
    ApiClientKeyDialog,
    ConfirmDialog,
    DataTable,
    DataTableColumn,
    FeaturePage,
    Field,
    Icon,
    Notice,
    QueryBar,
    QueryField,
    StatusBadge,
    TableHeader,
    TableState,
    ViewSwitch,
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
  private readonly router = inject(Router);
  private readonly editor = viewChild.required(ApiClientDialog);
  private readonly keys = viewChild.required(ApiClientKeyDialog);
  private readonly confirmation = viewChild.required(ConfirmDialog);
  readonly query = queryState(apiClientsQueryOptions);
  readonly draft = this.query.draft;
  readonly statuses = API_CLIENT_STATUSES;
  readonly searchMax = API_CLIENT_SEARCH_MAX;
  readonly table = localTable({
    rows: () => {
      const query = this.query.applied();
      return this.store.clients()?.filter((client) => matchesApiClient(client, query));
    },
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
      { id: 'status', label: '狀態' },
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
      {
        id: 'calls',
        label: '近 30 天呼叫次數',
        header: '近 30 天',
        sort: 'desc',
        numeric: true,
        value: (client: ApiClientDto) => client.recentCalls,
      },
    ],
    unit: '個',
  });
  readonly rows = computed(() => {
    const busy = this.store.busy();
    const audit = this.session.has('audit');
    const now = Date.now();
    return this.table.rows().map((client) => ({
      client,
      issued: formatDate(client.keyIssuedAt),
      keyStale: now - Date.parse(client.keyIssuedAt) > KEY_STALE_DAYS * DAY_MS,
      used: client.lastUsedAt ? relativeTime(client.lastUsedAt) : '',
      usedTitle: client.lastUsedAt ? formatDate(client.lastUsedAt) : '',
      calls: count.format(client.recentCalls),
      actions: [
        { id: 'edit', label: '編輯', icon: 'edit', disabled: busy },
        ...(audit ? [{ id: 'calls', label: '查看呼叫紀錄', icon: 'audit' }] : []),
        { id: 'reissue', label: '重新產生金鑰', icon: 'repeat', disabled: busy },
        client.enabled
          ? { id: 'disable', label: '停用', icon: 'stop', disabled: busy }
          : { id: 'enable', label: '啟用', icon: 'play', disabled: busy },
        { id: 'delete', label: '刪除', icon: 'trash', danger: true, disabled: busy },
      ] satisfies MenuAction[],
    }));
  });
  /** 一個都還沒有時引導新增；有資料但被條件濾光時說明是條件的關係。 */
  readonly empty = computed(() =>
    this.store.clients()?.length
      ? { title: '沒有符合條件的外部系統', text: '換個狀態或搜尋條件再查詢。' }
      : {
          title: '還沒有外部系統',
          text: '新增後把金鑰交給對方，對方就能以 AD 驗證使用者的帳密。',
        },
  );

  create() {
    this.editor().create();
  }
  action(client: ApiClientDto, id: string) {
    if (id === 'edit') this.editor().edit(client);
    else if (id === 'calls')
      void this.router.navigate([routeOf('audit')], { queryParams: apiClientCallsQuery(client) });
    else if (id === 'reissue') void this.reissue(client);
    else if (id === 'disable') void this.disable(client);
    else if (id === 'enable') void this.enable(client);
    else if (id === 'delete') void this.remove(client);
  }
  /** 等新增對話框關閉後再開：兩個 modal 同時開著時，前一個關閉會把焦點還給背後的頁面。 */
  showKey(issued: IssuedApiClientKeyDto) {
    setTimeout(() => this.keys().open(issued));
  }
  private async disable(client: ApiClientDto) {
    if (
      !(await this.confirmation().ask({
        title: `停用「${client.name}」？`,
        message: '停用後這把金鑰的驗證一律被拒，直到重新啟用；金鑰不變，啟用後對方不必改設定。',
        confirm: '停用',
      }))
    )
      return;
    this.failed(await this.store.setEnabled(client, false));
  }
  private async enable(client: ApiClientDto) {
    this.failed(await this.store.setEnabled(client, true));
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
    else this.failed(outcome);
  }
  private async remove(client: ApiClientDto) {
    if (
      !(await this.confirmation().ask({
        title: `刪除外部系統「${client.name}」？`,
        message: '金鑰會立即失效，對方無法再驗證帳密。刪除後無法復原；只想暫停時改用停用。',
        confirm: '刪除',
        danger: true,
      }))
    )
      return;
    this.failed(await this.store.remove(client));
  }
  /** 列上動作的失敗放頁內 Notice；版本衝突改說明並重讀。略過（重讀中）時什麼都不做。 */
  private failed(outcome: ActionOutcome<unknown>) {
    if (outcome.ok) return;
    if (outcome.stale) this.store.stale();
    else if (outcome.error) this.store.notice.set(outcome.error);
  }
}
