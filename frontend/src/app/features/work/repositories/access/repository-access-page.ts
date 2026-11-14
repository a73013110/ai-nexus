import {
  ChangeDetectionStrategy,
  Component,
  Injector,
  afterNextRender,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { apiResource } from '@core/api/api-resource';
import type { ManagedRepositoryDto } from '@core/api/schema';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { FeaturePage } from '@core/layout/feature-page';
import { action } from '@shared/browser/action';
import { ViewScope } from '@shared/browser/view-scope';
import { DataTable, DataTableColumn, TableState } from '@shared/ui/data-table';
import { Icon } from '@shared/ui/icon';
import { Notice } from '@shared/ui/notice';
import { PendingButton } from '@shared/ui/pending-button';
import { SearchField } from '@shared/ui/search-field';
import { StatusBadge } from '@shared/ui/status-badge';
import { TableHeader } from '@shared/ui/table-header';
import { localTable } from '@shared/ui/table-model';
import { ToastService } from '@shared/ui/toast-service';
import { ViewSwitch } from '@shared/ui/view-switch';
import { RepositoriesApi } from '../repositories-api';
import { RepositoryAccessDrawer } from './repository-access-drawer';
import { myAccess, repositoryTags, syncSummary } from './repository-access-presentation';

type AccessView = 'all' | 'mine' | 'stewarded';

/**
 * 儲存庫權限（`repositories.access`）：Gitea 各組織的儲存庫，申請讀取或寫入，核准後由同步工作加為協作者。
 * 負責人看得到自己負責的儲存庫的名單並可直接授予；系統管理員看全部，另可設定負責人與立即同步。
 * 清單一次讀完（儲存庫數量有限），搜尋與檢視在本機篩；`?repository=` 是開著的儲存庫（通知連結帶入）。
 */
@Component({
  selector: 'nx-repository-access-page',
  imports: [
    DataTable,
    DataTableColumn,
    FeaturePage,
    Icon,
    Notice,
    PendingButton,
    RepositoryAccessDrawer,
    SearchField,
    StatusBadge,
    TableHeader,
    TableState,
    ViewSwitch,
  ],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: block;
      min-width: 0;
    }
    .access-name {
      display: grid;
      min-width: 0;
    }
    .access-name small,
    .access-cell small {
      color: var(--text-muted);
    }
    .access-cell {
      display: grid;
      justify-items: start;
      gap: var(--p-space-1);
      min-width: 0;
    }
    .access-tags {
      display: flex;
      flex-wrap: wrap;
      gap: var(--p-space-1);
    }
    .access-mine-column {
      --column-width: 10rem;
    }
    .access-stewards-column {
      --column-width: 10rem;
    }
    .access-state-column {
      --column-width: 9rem;
    }
    .access-sync {
      color: var(--text-muted);
      font-size: var(--text-small);
    }
  `,
  templateUrl: './repository-access-page.html',
})
export class RepositoryAccessPage {
  readonly session = inject(WorkspaceSession);
  private readonly api = inject(RepositoriesApi);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly injector = inject(Injector);
  readonly drawer = viewChild.required(RepositoryAccessDrawer);

  /** 同步進行中或已排入時每 3 秒重讀，看得到結果；其餘時間不輪詢。 */
  readonly read = apiResource({
    feature: 'repositories.access',
    loader: (_, signal) => this.api.managed(signal),
    poll: (value) => (value?.sync?.running || value?.sync?.requested ? 3000 : null),
  });
  readonly data = computed(() => this.read.value());
  readonly administrator = computed(() => !!this.data()?.administrator);
  readonly sync = computed(() => syncSummary(this.data()?.sync ?? null));
  readonly syncing = action();

  readonly views = computed(() => [
    { value: 'all', label: '全部' },
    { value: 'mine', label: '我的權限' },
    ...(this.data()?.items.some((item) => item.manager === 'Steward')
      ? [{ value: 'stewarded', label: '我負責的' }]
      : []),
  ]);
  readonly view = signal<AccessView>('all');
  readonly search = signal('');
  readonly table = localTable({
    rows: () => {
      const items = this.data()?.items;
      if (!items) return undefined;
      const view = this.view();
      const needle = this.search().trim().toLowerCase();
      return items.filter(
        (item) =>
          (view === 'all' ||
            (view === 'mine'
              ? !!item.myGrant || !!item.pendingInstanceId
              : item.manager === 'Steward')) &&
          (!needle ||
            `${item.owner}/${item.name} ${item.description ?? ''}`.toLowerCase().includes(needle)),
      );
    },
    read: {
      loading: this.read.loading,
      refreshing: this.read.refreshing,
      error: this.read.error,
      reload: () => this.read.reload(),
    },
    columns: [
      {
        id: 'name',
        label: '儲存庫',
        hideable: false,
        sort: 'asc',
        value: (item: ManagedRepositoryDto) => `${item.owner}/${item.name}`,
      },
      { id: 'mine', label: '我的權限' },
      {
        id: 'stewards',
        label: '負責人',
        value: (item: ManagedRepositoryDto) =>
          item.stewards.map((person) => person.displayName).join('、'),
      },
      { id: 'state', label: '狀態' },
    ],
    sort: { column: 'name', direction: 'asc' },
    unit: '個',
  });
  readonly rows = computed(() =>
    this.table.rows().map((item) => ({
      item,
      fullName: `${item.owner}/${item.name}`,
      mine: myAccess(item),
      stewards: item.stewards.map((person) => person.displayName).join('、'),
      tags: repositoryTags(item),
    })),
  );
  readonly empty = computed(() =>
    this.data()?.items.length
      ? { title: '沒有符合條件的儲存庫', text: '換個檢視或搜尋條件再看看。' }
      : {
          title: '還沒有儲存庫',
          text: this.administrator()
            ? '按「立即同步」從 Gitea 讀取各組織的儲存庫。'
            : '系統管理員同步 Gitea 後，可申請的儲存庫會列在這裡。',
        },
  );

  readonly selectedId = signal<string | null>(null);
  readonly selectedIndex = computed(() =>
    this.table.rows().findIndex((item) => item.id === this.selectedId()),
  );

  constructor() {
    // 通知連結或重新整理帶入的儲存庫：等畫面建好再開抽屜。自己改寫網址時 ID 相同，不重複開。
    this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      const id = params.get('repository');
      if (!id || id === this.selectedId()) return;
      this.selectedId.set(id);
      afterNextRender(() => this.drawer().open(), { injector: this.injector });
    });
  }

  chooseView(value: string) {
    this.view.set(value as AccessView);
  }
  openRepository(id: string) {
    this.selectedId.set(id);
    this.drawer().open();
    this.syncUrl(id);
  }
  adjacent(direction: -1 | 1) {
    const row = this.table.rows()[this.selectedIndex() + direction];
    if (row) this.openRepository(row.id);
  }
  closeRepository() {
    this.selectedId.set(null);
    this.syncUrl(null);
  }
  async startSync() {
    const outcome = await this.syncing.run(() => this.api.syncAccess());
    if (!outcome.ok) return;
    this.toast.info('已排入同步', '完成後清單會自動更新。');
    this.read.reload();
  }
  private syncUrl(repository: string | null) {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { repository },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }
}
