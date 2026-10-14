import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  linkedSignal,
  signal,
  untracked,
} from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { apiResource } from '@core/api/api-resource';
import type { RepositoryCommitDto, RepositoryCommitPageDto } from '@core/api/schema';
import { APP_TIME_ZONE, relativeTime } from '@shared/browser/format';
import { Icon } from '@shared/ui/icon';
import { Notice } from '@shared/ui/notice';
import { RepositoriesApi } from '../repositories-api';
import { RepositoryStore, shortSha } from '../repository-store';

const dayFormat = new Intl.DateTimeFormat('zh-TW', {
  timeZone: APP_TIME_ZONE,
  year: 'numeric',
  month: 'long',
  day: 'numeric',
  weekday: 'short',
});

/**
 * History from the pinned commit, grouped by day. Picking two commits opens their range: the
 * older one's changes are included, so the range reads "from this commit to that one".
 */
@Component({
  selector: 'nx-repository-commits',
  imports: [RouterLink, Icon, Notice],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './repository-commits.scss',
  template: `<p class="hint">
      <nx-icon name="compare" />點選左側圓點選擇兩個提交，就能看區間內的所有變更並交給 AI 摘要。
    </p>
    @if (error()) {
      <nx-notice [message]="error()"
        ><button notice-actions type="button" (click)="reload()">重試</button></nx-notice
      >
    }
    @for (day of days(); track day.label) {
      <section class="day" [attr.aria-label]="day.label">
        <h3>{{ day.label }}</h3>
        <ol>
          @for (commit of day.commits; track commit.sha) {
            <li
              class="commit"
              [class.picked]="picked().includes(commit.sha)"
              [class.within]="within().has(commit.sha)"
            >
              <button
                type="button"
                class="dot"
                [attr.aria-pressed]="picked().includes(commit.sha)"
                [attr.aria-label]="'選為區間端點：' + commit.title"
                (click)="pick(commit.sha)"
              >
                <nx-icon name="check" />
              </button>
              <a class="body" [routerLink]="store.link('commits', commit.sha)">
                <strong class="title">{{ commit.title || '（沒有說明）' }}</strong>
                <span class="meta"
                  ><span class="avatar" aria-hidden="true">{{ initial(commit.author) }}</span
                  >{{ commit.author }}
                  @if (commit.at) {
                    · {{ when(commit.at) }}
                  }
                  @if (commit.parents.length > 1) {
                    <span class="merge"><nx-icon name="merged" />合併</span>
                  }
                </span>
              </a>
              <code class="sha">{{ short(commit.sha) }}</code>
            </li>
          }
        </ol>
      </section>
    } @empty {
      @if (loading()) {
        @for (row of [1, 2, 3, 4]; track row) {
          <span class="skeleton row-skeleton"></span>
        }
      } @else if (!error()) {
        <p class="blank">這個版本還沒有提交紀錄。</p>
      }
    }
    @if (hasMore()) {
      <div class="more">
        <button type="button" class="secondary-button" [disabled]="loading()" (click)="more()">
          {{ loading() ? '載入中…' : '載入更早的提交' }}
        </button>
      </div>
    }
    @if (picked().length) {
      <div class="selection" role="region" aria-label="提交區間">
        @if (range(); as range) {
          <span class="count"
            ><strong>{{ range.count }}</strong> 個提交 · <code>{{ short(range.from) }}</code> →
            <code>{{ short(range.head) }}</code></span
          >
        } @else {
          <span class="count">再選一個提交組成區間</span>
        }
        <button type="button" class="quiet-button" (click)="picked.set([])">清除</button>
        @if (range(); as range) {
          <button type="button" class="primary-button" (click)="compare()">
            <nx-icon name="compare" />查看區間變更
          </button>
        } @else {
          <a class="secondary-button" [routerLink]="store.link('commits', picked()[0])"
            >查看這個提交</a
          >
        }
      </div>
    }`,
})
export class RepositoryCommits {
  readonly store = inject(RepositoryStore);
  private readonly api = inject(RepositoriesApi);
  private readonly router = inject(Router);
  private readonly page = signal(1);
  private readonly read = apiResource({
    feature: 'repositories',
    params: () => {
      const key = this.store.key(),
        commit = this.store.commit();
      return key && commit ? { key, commit, page: this.page() } : undefined;
    },
    loader: async ({ key, commit, page }) => ({
      commit,
      page,
      data: await this.api.commits(key, commit, page),
    }),
  });
  private readonly latest = computed(() => {
    const read = this.read.value();
    return read && read.commit === this.store.commit() ? read : null;
  });
  readonly items = linkedSignal<
    { commit: string; page: number; data: RepositoryCommitPageDto } | null,
    RepositoryCommitDto[]
  >({
    source: this.latest,
    computation: (read, previous) => {
      if (!read) return [];
      if (read.page === 1) return read.data.items;
      const seen = new Set(previous?.value.map((x) => x.sha));
      return [...(previous?.value ?? []), ...read.data.items.filter((x) => !seen.has(x.sha))];
    },
  });
  readonly days = computed(() => {
    const days: { label: string; commits: RepositoryCommitDto[] }[] = [];
    for (const commit of this.items()) {
      const label = commit.at ? dayFormat.format(new Date(commit.at)) : '時間不明';
      const last = days.at(-1);
      if (last?.label === label) last.commits.push(commit);
      else days.push({ label, commits: [commit] });
    }
    return days;
  });
  readonly hasMore = computed(() => !!this.latest()?.data.hasMore);
  readonly loading = this.read.loading;
  readonly error = this.read.error;
  /** Up to two commits; picking a third replaces the older pick. */
  readonly picked = signal<string[]>([]);
  readonly range = computed(() => {
    const [a, b] = this.picked();
    if (!a || !b) return null;
    const items = this.items(),
      ia = items.findIndex((x) => x.sha === a),
      ib = items.findIndex((x) => x.sha === b);
    if (ia < 0 || ib < 0) return null;
    const head = items[Math.min(ia, ib)],
      from = items[Math.max(ia, ib)];
    return {
      head: head.sha,
      from: from.sha,
      base: from.parents[0] ?? from.sha,
      count: Math.abs(ia - ib) + 1,
    };
  });
  readonly within = computed(() => {
    const range = this.range();
    if (!range) return new Set<string>();
    const items = this.items(),
      start = items.findIndex((x) => x.sha === range.head),
      end = items.findIndex((x) => x.sha === range.from);
    return new Set(items.slice(start, end + 1).map((x) => x.sha));
  });
  readonly short = shortSha;
  readonly when = relativeTime;

  constructor() {
    effect(() => {
      this.store.fullName();
      this.store.commit();
      untracked(() => {
        this.page.set(1);
        this.picked.set([]);
      });
    });
  }

  initial = (name: string) => (name || '?').slice(0, 1).toUpperCase();
  reload() {
    this.read.reload();
  }
  more() {
    if (!this.loading()) this.page.update((x) => x + 1);
  }
  pick(sha: string) {
    this.picked.update((current) =>
      current.includes(sha) ? current.filter((x) => x !== sha) : [...current.slice(-1), sha],
    );
  }
  compare() {
    const range = this.range();
    if (range)
      void this.router.navigate(this.store.link('compare', `${range.base}...${range.head}`), {
        queryParamsHandling: 'preserve',
      });
  }
}
