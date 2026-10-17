import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { apiResource } from '@core/api/api-resource';
import type { RepositoryStatusDto } from '@core/api/schema';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { FeaturePage } from '@core/layout/feature-page';
import { ViewScope } from '@shared/browser/view-scope';
import { ActionMenu, type MenuAction } from '@shared/ui/action-menu';
import { ConfirmDialog } from '@shared/ui/confirm-dialog';
import { EmptyState } from '@shared/ui/empty-state';
import { Icon } from '@shared/ui/icon';
import { Notice } from '@shared/ui/notice';
import { ToastService } from '@shared/ui/toast-service';
import { RepositoriesApi } from './repositories-api';
import { repositoriesTour, repositoryTour } from './repositories-tour';
import { RepositoryConnect } from './connections/repository-connect';
import { RepositoryDetail, repositorySection } from './repository-detail';
import { repositoryIcons } from './repository-icons';
import { RepositoryList } from './repository-list';
import { RepositoryStore } from './repository-store';

/**
 * 在單一路由上承載儲存庫列表與一個儲存庫的各檢視。負責 Gitea 連線、已選取的儲存庫與 ref，並把舊的
 * `?review=` 連結轉成審查路由。
 */
@Component({
  selector: 'nx-repositories-page',
  imports: [
    FeaturePage,
    ActionMenu,
    ConfirmDialog,
    EmptyState,
    Icon,
    Notice,
    RepositoryConnect,
    RepositoryDetail,
    RepositoryList,
  ],
  providers: [ViewScope, RepositoryStore, repositoryIcons],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './repositories-page.scss',
  template: `<nx-feature-page
      [title]="title()"
      [description]="description()"
      [back]="store.key() ? { path: '/repositories', label: '全部程式庫' } : null"
      [tour]="tour()"
    >
      <div page-actions class="page-actions">
        @if (status()?.connected && !reconnecting()) {
          @if (store.detail(); as detail) {
            <button
              type="button"
              class="icon-button pin-toggle"
              [class.pinned]="detail.repository.pinned"
              [attr.aria-pressed]="detail.repository.pinned"
              [attr.aria-label]="detail.repository.pinned ? '取消釘選' : '釘選到列表上方'"
              [title]="detail.repository.pinned ? '取消釘選' : '釘選到列表上方'"
              [disabled]="busy()"
              (click)="togglePin()"
            >
              <nx-icon name="pin" />
            </button>
            <a
              class="secondary-button"
              [href]="detail.repository.url"
              target="_blank"
              rel="noopener noreferrer"
              ><nx-icon name="external" />在 Gitea 開啟</a
            >
          } @else if (!store.key()) {
            <span class="account" [title]="status()!.baseUrl"
              ><span class="account-mark" aria-hidden="true">{{ initial() }}</span
              >{{ status()!.login }}</span
            >
          }
          <nx-action-menu
            class="repository-account"
            label="Gitea 連線"
            [heading]="{ title: status()!.login || 'Gitea', detail: status()!.baseUrl }"
            [items]="accountActions"
            (action)="account($event)"
          />
        }
      </div>
      @if (error()) {
        <nx-notice [message]="error()"
          ><button notice-actions type="button" (click)="reload()">重試</button></nx-notice
        >
      }
      @if (status(); as connection) {
        @if (!connection.available) {
          <nx-empty-state>
            <nx-icon name="git" />
            <h2>程式庫尚未啟用</h2>
            <p>請洽管理員在設定中開啟 Gitea 連線。</p>
          </nx-empty-state>
        } @else if (!connection.connected || reconnecting()) {
          <nx-repository-connect
            [status]="connection"
            [reconnecting]="reconnecting()"
            (connected)="connected($event)"
            (cancelled)="reconnecting.set(false)"
          />
        } @else if (store.key()) {
          <nx-repository-detail [view]="view()" [item]="item()" />
        } @else {
          <nx-repository-list />
        }
      } @else if (statusLoading()) {
        <div class="page-skeleton" role="status" aria-label="正在載入程式庫">
          <span class="skeleton"></span><span class="skeleton"></span><span class="skeleton"></span>
        </div>
      }
    </nx-feature-page>
    <nx-confirm-dialog />`,
})
export class RepositoriesPage {
  readonly store = inject(RepositoryStore);
  private readonly session = inject(WorkspaceSession);
  private readonly api = inject(RepositoriesApi);
  private readonly scope = inject(ViewScope);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);
  private readonly confirm = viewChild.required(ConfirmDialog);
  private readonly route = inject(ActivatedRoute);
  private readonly params = toSignal(this.route.paramMap, { requireSync: true });
  private readonly query = toSignal(this.route.queryParamMap, { requireSync: true });
  readonly view = computed(() => this.params().get('view') ?? '');
  readonly item = computed(() => this.params().get('item') ?? '');
  private readonly statusRead = apiResource({
    feature: 'repositories',
    loader: () => this.api.status(),
  });
  readonly status = computed<RepositoryStatusDto | null>(() => this.statusRead.value() ?? null);
  readonly statusLoading = this.statusRead.loading;
  readonly reconnecting = signal(false);
  readonly busy = signal(false);
  private readonly actionError = signal('');
  readonly error = computed(
    () =>
      this.actionError() || this.statusRead.error() || (this.store.key() ? this.store.error() : ''),
  );
  readonly initial = computed(() => (this.status()?.login ?? 'G').slice(0, 1).toUpperCase());
  readonly title = computed(() => {
    const key = this.store.key();
    return key ? key.name : this.session.featureName('repositories');
  });
  readonly description = computed(() => {
    const key = this.store.key();
    if (!key)
      return '用你的 Gitea 權限閱讀程式、提交紀錄、議題與 Pull Request，再帶入對話或交給 AI 初檢。';
    return this.store.detail()?.repository.description || key.owner;
  });
  /** 連線後，顯示列表或已開啟儲存庫的導覽；連線畫面自己會說明。 */
  readonly tour = computed(() => {
    if (!this.status()?.connected || this.reconnecting()) return null;
    const features = { chat: this.session.has('chat'), knowledge: this.session.has('knowledge') };
    if (!this.store.key()) return repositoriesTour(features);
    const section = repositorySection(this.view());
    return repositoryTour({
      ...features,
      section,
      // 檔案透過查詢參數開啟；其他區段的項目都在路徑中。
      item: section === 'code' ? !!this.query().get('file') : !!this.item(),
      empty: !!this.store.detail()?.repository.empty,
    });
  });
  readonly accountActions: MenuAction[] = [
    { id: 'reconnect', label: '更新授權', icon: 'key' },
    { id: 'disconnect', label: '中斷連線', icon: 'logout', danger: true },
  ];

  constructor() {
    effect(() => {
      const params = this.params(),
        owner = params.get('owner'),
        name = params.get('name');
      untracked(() => {
        const current = this.store.key();
        if (owner && name) {
          if (current?.owner !== owner || current.name !== name)
            this.store.key.set({ owner, name });
        } else if (current) this.store.key.set(null);
      });
    });
    effect(() => this.store.ref.set(this.query().get('ref') ?? ''));
    // 通知與工作只知道審查的 id；其儲存庫由審查本身決定。
    effect(() => {
      const review = this.query().get('review');
      if (review && this.status()?.connected) untracked(() => void this.openReview(review));
    });
  }

  reload() {
    this.actionError.set('');
    this.statusRead.reload();
    this.store.reload();
  }

  connected(status: RepositoryStatusDto) {
    this.statusRead.value.set(status);
    this.reconnecting.set(false);
    this.store.reload();
  }

  async account(action: string) {
    if (action === 'reconnect') {
      this.reconnecting.set(true);
      return;
    }
    const confirmed = await this.confirm().ask({
      title: '中斷 Gitea 連線？',
      message:
        '會刪除保存的權杖。已匯入知識庫的文件與 review 紀錄會保留，之後重新連線即可繼續閱讀。',
      confirm: '中斷連線',
      danger: true,
    });
    if (!confirmed || this.busy()) return;
    const valid = this.scope.guard();
    this.busy.set(true);
    try {
      await this.api.disconnect();
      if (!valid()) return;
      this.statusRead.value.update((item2) =>
        item2 ? { ...item2, connected: false, login: null } : item2,
      );
      void this.router.navigate(['/repositories']);
    } catch (error2) {
      if (valid()) this.actionError.set(this.scope.message(error2));
    } finally {
      if (valid()) this.busy.set(false);
    }
  }

  async togglePin() {
    const key = this.store.key(),
      pinned = this.store.detail()?.repository.pinned;
    if (!key || this.busy()) return;
    const valid = this.scope.guard();
    this.busy.set(true);
    this.store.setPinned(!pinned);
    try {
      if (pinned) await this.api.unpin(key);
      else await this.api.pin(key);
      if (valid())
        this.toast.show({ title: pinned ? '已取消釘選' : '已釘選到列表上方', duration: 2400 });
    } catch (error3) {
      if (!valid()) return;
      this.store.setPinned(!!pinned);
      this.actionError.set(this.scope.message(error3));
    } finally {
      if (valid()) this.busy.set(false);
    }
  }

  private async openReview(id: string) {
    const valid = this.scope.guard();
    try {
      const detail = await this.api.review(id);
      if (!valid()) return;
      const [owner, name] = detail.review.repository.split('/');
      await this.router.navigate(['/repositories', owner, name, 'reviews', id], {
        replaceUrl: true,
      });
    } catch (error4) {
      if (valid()) this.actionError.set(this.scope.message(error4));
    }
  }
}
