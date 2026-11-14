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
import { Field } from '@shared/ui/field';
import { QueryBar, QueryField } from '@shared/ui/query-bar';
import { queryState } from '@shared/ui/query-state';
import { Select } from '@shared/ui/select';
import { StatusBadge } from '@shared/ui/status-badge';
import { TableHeader } from '@shared/ui/table-header';
import { localTable } from '@shared/ui/table-model';
import { ToastService } from '@shared/ui/toast-service';
import { ViewSwitch } from '@shared/ui/view-switch';
import { RepositoriesApi } from '../repositories-api';
import { RepositoryAccessDrawer } from './repository-access-drawer';
import {
  REPOSITORY_STATES,
  myAccess,
  repositoryTags,
  syncSummary,
} from './repository-access-presentation';
import {
  REPOSITORY_SEARCH_MAX,
  REPOSITORY_VIEWS,
  matchesRepository,
  repositoryAccessQueryOptions,
} from './repository-access-query';

/**
 * 儲存庫權限（`repositories.access`）：Gitea 各組織的儲存庫，申請讀取或寫入，核准後由同步工作加為協作者。
 * 負責人看得到自己負責的儲存庫的名單並可直接授予；系統管理員看全部，另可設定負責人與立即同步。
 * 清單一次讀完，條件用共用的 `QueryBar` 在本機篩（`repository-access-query.ts`）；`?repository=` 是開著的儲存庫（通知連結帶入）。
 * 套用條件會以條件重寫網址、拿掉 `repository`，抽屜照樣開著，只是重新整理後不再自動打開。
 */
@Component({
  selector: 'nx-repository-access-page',
  imports: [
    DataTable,
    DataTableColumn,
    FeaturePage,
    Field,
    Icon,
    Notice,
    PendingButton,
    QueryBar,
    QueryField,
    RepositoryAccessDrawer,
    Select,
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
    /* 標籤排成一行，列高才不因標籤數量不同而參差；放不下的由 title 與詳情補上。 */
    .access-tags {
      display: flex;
      gap: var(--p-space-1);
      overflow: hidden;
    }
    .access-mine-column {
      --column-width: 10rem;
    }
    .access-stewards-column {
      --column-width: 10rem;
    }
    .access-state-column {
      --column-width: 12rem;
    }
    .access-sync {
      color: var(--text-muted);
      font-size: var(--text-small);
    }
    .access-sync[data-tone='danger'] {
      color: var(--danger);
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

  readonly query = queryState(repositoryAccessQueryOptions);
  readonly draft = this.query.draft;
  readonly searchMax = REPOSITORY_SEARCH_MAX;
  /** 「我負責的」只給有負責儲存庫的人。 */
  readonly views = computed(() =>
    REPOSITORY_VIEWS.filter(
      (view) =>
        view.value !== 'stewarded' || this.data()?.items.some((item) => item.manager === 'Steward'),
    ),
  );
  /** 組織從清單讀出；只有一個組織時這個條件沒有意義，但保留讓欄位位置固定。 */
  readonly owners = computed(() => [
    { value: '', label: '所有組織' },
    ...[...new Set((this.data()?.items ?? []).map((item) => item.owner))]
      .sort((a, b) => a.localeCompare(b))
      .map((owner) => ({ value: owner, label: owner })),
  ]);
  /** 「有未納管協作者」「未指定負責人」是管理者要處理的事，一般使用者不列。 */
  readonly states = computed(() =>
    REPOSITORY_STATES.filter(
      (state) =>
        !['unregistered', 'unassigned'].includes(state.value) ||
        this.data()?.items.some((item) => item.manager !== 'None'),
    ),
  );
  readonly table = localTable({
    rows: () => {
      const query = this.query.applied();
      return this.data()?.items.filter((item) => matchesRepository(item, query));
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
    this.table.rows().map((item) => {
      const tags = repositoryTags(item);
      return {
        item,
        fullName: `${item.owner}/${item.name}`,
        mine: myAccess(item),
        stewards: item.stewards.map((person) => person.displayName).join('、'),
        tags,
        tagText: tags.map((tag) => tag.label).join('、'),
      };
    }),
  );
  readonly empty = computed(() =>
    this.data()?.items.length
      ? { title: '沒有符合條件的儲存庫', text: '換個檢視或查詢條件再看看。' }
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
