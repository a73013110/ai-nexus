import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  linkedSignal,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { apiResource } from '@core/api/api-resource';
import type { RepositoryDto, RepositoryPageDto } from '@core/api/schema';
import { relativeTime } from '@shared/browser/format';
import { ViewScope } from '@shared/browser/view-scope';
import { EmptyState } from '@shared/ui/empty-state';
import { Icon } from '@shared/ui/icon';
import { Notice } from '@shared/ui/notice';
import { PendingButton } from '@shared/ui/pending-button';
import { ResourceCard } from '@shared/ui/resource-card';
import { ResourceGallery } from '@shared/ui/resource-gallery';
import { RepositoriesApi } from './repositories-api';
import { routeOf } from '@core/api/catalog';

/** 使用者的 token 能讀取的每個儲存庫，依最近更新排序，釘選的在最上方。 */
@Component({
  selector: 'nx-repository-list',
  imports: [RouterLink, EmptyState, Icon, Notice, PendingButton, ResourceCard, ResourceGallery],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './repository-list.scss',
  template: `@if (error()) {
      <nx-notice [message]="error()"
        ><button notice-actions nxPending type="button" [pending]="loading()" (click)="reload()">
          {{ loading() ? '重試中…' : '重試' }}
        </button></nx-notice
      >
    }
    @if (pinned().length && !query()) {
      <section class="pinned" aria-labelledby="pinned-title">
        <h2 id="pinned-title"><nx-icon name="pin" />釘選</h2>
        <div class="pinned-row">
          @for (pin of pinned(); track pin.fullName) {
            <a class="pin" [routerLink]="link(pin.fullName)">
              <span class="monogram" aria-hidden="true">{{ monogram(pin.fullName) }}</span>
              <span class="pin-text"
                ><strong>{{ name(pin.fullName) }}</strong
                ><small>{{ owner(pin.fullName) }}</small></span
              >
              @if (pin.private) {
                <nx-icon name="lock" class="pin-lock" />
              }
            </a>
          }
        </div>
      </section>
    }
    <nx-resource-gallery
      label="程式庫"
      placeholder="搜尋名稱或說明"
      [search]="search()"
      [loading]="firstLoad()"
      [busy]="refreshing()"
      [empty]="!items().length && !error()"
      (searchChange)="type($event)"
    >
      @for (repo of items(); track repo.fullName) {
        <a
          nxResourceCard
          [icon]="repo.private ? 'lock' : 'git'"
          [routerLink]="link(repo.fullName)"
          [heading]="repo.fullName"
          [description]="repo.description"
        >
          @if (repo.pinned) {
            <span card-badge class="badge"><nx-icon name="pin" />已釘選</span>
          } @else if (repo.archived) {
            <span card-badge class="badge">已封存</span>
          }
          @if (repo.language) {
            <span>{{ repo.language }}</span>
          }
          @if (repo.openPulls) {
            <span class="stat"><nx-icon name="pull" />{{ repo.openPulls }}</span>
          }
          @if (repo.openIssues) {
            <span class="stat"><nx-icon name="issue" />{{ repo.openIssues }}</span>
          }
          @if (repo.updatedAt) {
            <span>{{ when(repo.updatedAt) }}更新</span>
          }
        </a>
      }
      <nx-empty-state gallery-empty>
        <nx-icon name="git" />
        @if (query()) {
          <h2>找不到「{{ query() }}」</h2>
          <p>只會列出你的 Gitea 帳號能閱讀的程式庫。</p>
        } @else {
          <h2>還沒有可閱讀的程式庫</h2>
          <p>你的 Gitea 帳號目前沒有任何程式庫權限。</p>
        }
      </nx-empty-state>
    </nx-resource-gallery>
    @if (hasMore()) {
      <div class="more">
        <button
          nxPending
          type="button"
          class="secondary-button"
          [pending]="loadingMore()"
          [disabled]="refreshing()"
          (click)="more()"
        >
          {{ loadingMore() ? '載入中…' : '載入更多' }}
        </button>
      </div>
    }`,
})
export class RepositoryList {
  private readonly api = inject(RepositoriesApi);
  private readonly scope = inject(ViewScope);
  /** 搜尋欄顯示的文字；查詢在短暫停頓後才跟上。 */
  readonly search = signal('');
  readonly query = signal('');
  private readonly page = signal(1);
  private readonly read = apiResource({
    feature: 'repositories',
    params: () => ({ q: this.query(), page: this.page() }),
    loader: async ({ q, page }) => ({ q, page, data: await this.api.list(q, page) }),
  });
  private readonly latest = computed(() => {
    const read = this.read.value();
    return read && read.q === this.query() ? read : null;
  });
  /** 目前查詢已載入的頁數，不重複。 */
  readonly items = linkedSignal<
    { q: string; page: number; data: RepositoryPageDto } | null,
    RepositoryDto[]
  >({
    source: this.latest,
    computation: (read, previous) => {
      if (!read) return previous?.value ?? [];
      if (read.page === 1) return read.data.items;
      const seen = new Set(previous?.value.map((item) => item.fullName));
      return [
        ...(previous?.value ?? []),
        ...read.data.items.filter((item) => !seen.has(item.fullName)),
      ];
    },
  });
  readonly pinned = computed(() => this.latest()?.data.pinned ?? []);
  readonly hasMore = computed(() => !!this.latest()?.data.hasMore);
  readonly loading = this.read.refreshing;
  readonly error = this.read.error;
  readonly firstLoad = computed(() => this.read.loading() && !this.items().length);
  /** 輸入後還在防抖等待；與第一頁的重讀合起來是卡片格線的重新讀取。 */
  private readonly typing = signal(false);
  /** 搜尋或重讀第一頁：卡片淡化、搜尋欄轉圈。載入更多只轉按鈕，不淡化已有的卡片。 */
  readonly refreshing = computed(
    () => this.typing() || (this.read.refreshing() && this.page() === 1),
  );
  readonly loadingMore = computed(() => this.read.refreshing() && this.page() > 1);
  readonly when = relativeTime;

  type(value: string) {
    this.search.set(value);
    this.typing.set(true);
    this.scope.later(
      () => {
        this.typing.set(false);
        this.page.set(1);
        this.query.set(value.trim());
      },
      300,
      'search',
    );
  }
  reload() {
    this.read.reload();
  }
  more() {
    if (!this.loading()) this.page.update((page) => page + 1);
  }
  link(fullName: string) {
    return [routeOf('repositories'), ...fullName.split('/')];
  }
  owner = (fullName: string) => fullName.split('/')[0];
  name = (fullName: string) => fullName.split('/')[1];
  monogram = (fullName: string) => this.name(fullName).slice(0, 1).toUpperCase();
}
