import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  linkedSignal,
  signal,
  untracked,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { apiResource } from '@core/api/api-resource';
import type { RepositoryIssueDto, RepositoryLabelDto, RepositoryPullDto } from '@core/api/schema';
import { relativeTime } from '@shared/browser/format';
import { ViewScope } from '@shared/browser/view-scope';
import { Icon } from '@shared/ui/icon';
import { Notice } from '@shared/ui/notice';
import { PendingButton } from '@shared/ui/pending-button';
import { SearchField } from '@shared/ui/search-field';
import { ViewSwitch } from '@shared/ui/view-switch';
import { RepositoriesApi } from '../repositories-api';
import { RepositoryStore } from '../repository-store';

export type ThreadKind = 'pulls' | 'issues';

interface ThreadRead {
  id: string;
  page: number;
  data: { items: (RepositoryIssueDto | RepositoryPullDto)[]; hasMore: boolean };
}

interface ThreadRow {
  number: number;
  title: string;
  icon: string;
  tone: string;
  author: string;
  labels: RepositoryLabelDto[];
  comments: number;
  updatedAt: string | null;
  draft: boolean;
  refs: string;
}

/** 儲存庫的議題或拉取請求，依狀態篩選；議題也可搜尋。 */
@Component({
  selector: 'nx-repository-threads',
  imports: [RouterLink, Icon, Notice, PendingButton, SearchField, ViewSwitch],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './repository-threads.scss',
  template: `<div class="filters">
      <nx-view-switch
        label="狀態"
        appearance="segment"
        [options]="states()"
        [value]="state()"
        (valueChange)="filter({ state: $event === 'open' ? null : $event })"
      />
      @if (kind() === 'issues') {
        <nx-search-field
          class="search"
          label="搜尋議題"
          placeholder="搜尋標題或內容"
          [value]="search()"
          [busy]="stale()"
          (valueChange)="type($event)"
        />
      }
    </div>
    @if (error()) {
      <nx-notice [message]="error()"
        ><button notice-actions nxPending type="button" [pending]="refreshing()" (click)="reload()">
          {{ refreshing() ? '重試中…' : '重試' }}
        </button></nx-notice
      >
    }
    <ol class="list" [class.is-refreshing]="stale()" [attr.aria-busy]="stale() || null">
      @for (row of rows(); track row.number; let i = $index) {
        <li [style.--i]="i < 12 ? i : 12">
          <a class="row" [routerLink]="store.link(kind(), row.number)">
            <nx-icon [name]="row.icon" class="state" [attr.data-tone]="row.tone" />
            <span class="main">
              <span class="title"
                ><strong>{{ row.title }}</strong>
                @if (row.draft) {
                  <span class="draft">草稿</span>
                }
                @for (label of row.labels; track label.name) {
                  <span class="label" [style.--label]="label.color || null">{{ label.name }}</span>
                }
              </span>
              <span class="meta"
                >#{{ row.number }} · {{ row.author }}
                @if (row.updatedAt) {
                  · {{ when(row.updatedAt) }}更新
                }
                @if (row.refs) {
                  · <code>{{ row.refs }}</code>
                }
              </span>
            </span>
            @if (row.comments) {
              <span class="comments" [attr.aria-label]="row.comments + ' 則留言'"
                ><nx-icon name="lines" />{{ row.comments }}</span
              >
            }
          </a>
        </li>
      } @empty {
        @if (loading()) {
          <li role="status">
            <span class="sr-only">正在載入{{ kind() === 'pulls' ? ' Pull Request' : '議題' }}…</span
            ><span class="skeleton row-skeleton"></span>
          </li>
          @for (row of [2, 3]; track row) {
            <li aria-hidden="true"><span class="skeleton row-skeleton"></span></li>
          }
        } @else if (!error()) {
          <li class="blank">
            <nx-icon [name]="kind() === 'pulls' ? 'pull' : 'issue'" />
            <p>{{ emptyText() }}</p>
          </li>
        }
      }
    </ol>
    @if (hasMore()) {
      <div class="more">
        <button
          nxPending
          type="button"
          class="secondary-button"
          [pending]="loading()"
          (click)="more()"
        >
          {{ loading() ? '載入中…' : '載入更多' }}
        </button>
      </div>
    }`,
})
export class RepositoryThreads {
  readonly kind = input.required<ThreadKind>();
  readonly store = inject(RepositoryStore);
  private readonly api = inject(RepositoriesApi);
  private readonly router = inject(Router);
  private readonly scope = inject(ViewScope);
  private readonly query = toSignal(inject(ActivatedRoute).queryParamMap, { requireSync: true });
  readonly state = computed(() => {
    const state = this.query().get('state') ?? 'open';
    return ['open', 'closed', 'all'].includes(state) ? state : 'open';
  });
  private readonly q = computed(() =>
    this.kind() === 'issues' ? (this.query().get('q') ?? '') : '',
  );
  readonly search = linkedSignal(() => this.q());
  readonly states = computed(() => [
    { value: 'open', label: '開啟中' },
    { value: 'closed', label: this.kind() === 'pulls' ? '已關閉／合併' : '已關閉' },
    { value: 'all', label: '全部' },
  ]);
  private readonly page = signal(1);
  private readonly read = apiResource({
    feature: 'repositories',
    params: () => {
      const key = this.store.key();
      return key
        ? { key, kind: this.kind(), state: this.state(), q: this.q(), page: this.page() }
        : undefined;
    },
    loader: async ({ key, kind, state, q, page }): Promise<ThreadRead> => {
      const data =
        kind === 'pulls'
          ? await this.api.pulls(key, state, page)
          : await this.api.issues(key, state, page, q);
      return { id: `${key.owner}/${key.name}:${kind}:${state}:${q}`, page, data };
    },
  });
  private readonly id = computed(
    () => `${this.store.fullName()}:${this.kind()}:${this.state()}:${this.q()}`,
  );
  private readonly latest = computed(() => {
    const read = this.read.value();
    return read && read.id === this.id() ? read : null;
  });
  /** 切換到其他狀態或搜尋載入時，上一份列表會保留並變暗。 */
  readonly stale = computed(() => !!this.read.value() && !this.latest());
  readonly rows = linkedSignal<ThreadRead | undefined, ThreadRow[]>({
    source: this.read.value,
    computation: (read, previous) => {
      if (!read) return previous?.value ?? [];
      const rows = read.data.items.map((item) => this.row(item));
      if (read.page === 1) return rows;
      const seen = new Set(previous?.value.map((item) => item.number));
      return [...(previous?.value ?? []), ...rows.filter((item) => !seen.has(item.number))];
    },
  });
  readonly hasMore = computed(() => !!this.latest()?.data.hasMore);
  readonly loading = this.read.loading;
  /** 使用者看得到的重讀（重試）。 */
  readonly refreshing = this.read.refreshing;
  readonly error = this.read.error;
  readonly emptyText = computed(() => {
    const thing = this.kind() === 'pulls' ? 'Pull Request' : '議題';
    if (this.q()) return `找不到符合「${this.q()}」的${thing}。`;
    return this.state() === 'open' ? `目前沒有開啟中的${thing}。` : `沒有${thing}。`;
  });
  readonly when = relativeTime;

  constructor() {
    effect(() => {
      this.id();
      untracked(() => this.page.set(1));
    });
  }

  filter(queryParams: Record<string, string | null>) {
    void this.router.navigate([], { queryParams, queryParamsHandling: 'merge', replaceUrl: true });
  }
  type(value: string) {
    this.search.set(value);
    this.scope.later(() => this.filter({ q: value.trim() || null }), 300, 'search');
  }
  reload() {
    this.read.reload();
  }
  more() {
    if (!this.loading()) this.page.update((page) => page + 1);
  }
  private row(item: RepositoryIssueDto | RepositoryPullDto): ThreadRow {
    const open = item.state === 'open';
    const pull = 'headRef' in item ? item : null;
    return {
      number: item.number,
      title: item.title,
      author: item.author,
      labels: item.labels,
      comments: item.comments,
      updatedAt: item.updatedAt,
      draft: !!pull?.draft,
      refs: pull ? `${pull.headRef} → ${pull.baseRef}` : '',
      icon: pull
        ? pull.merged
          ? 'merged'
          : open
            ? 'pull'
            : 'pull-closed'
        : open
          ? 'issue'
          : 'issue-closed',
      tone: pull?.merged ? 'merged' : open ? 'open' : 'closed',
    };
  }
}
