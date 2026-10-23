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
import { APP_TIME_ZONE, nameInitial, relativeTime } from '@shared/browser/format';
import { Icon } from '@shared/ui/icon';
import { Notice } from '@shared/ui/notice';
import { PendingButton } from '@shared/ui/pending-button';
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
 * 釘選提交的歷史，依日期分組。選取兩個提交會開啟它們的範圍：較舊的那個提交的變更也會包含在內，
 * 因此範圍讀作「從這個提交到那個提交」。
 */
@Component({
  selector: 'nx-repository-commits',
  imports: [RouterLink, Icon, Notice, PendingButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './repository-commits.scss',
  template: `<p class="hint">
      <nx-icon name="compare" />點選左側圓點選擇兩個提交，就能看區間內的所有變更並交給 AI 摘要。
    </p>
    @if (error()) {
      <nx-notice [message]="error()"
        ><button notice-actions nxPending type="button" [pending]="refreshing()" (click)="reload()">
          {{ refreshing() ? '重試中…' : '重試' }}
        </button></nx-notice
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
        <div class="commit-skeleton" role="status">
          <span class="sr-only">正在載入提交紀錄…</span>
          @for (row of [1, 2, 3, 4]; track row) {
            <span class="skeleton row-skeleton"></span>
          }
        </div>
      } @else if (!error()) {
        <p class="blank">這個版本還沒有提交紀錄。</p>
      }
    }
    @if (hasMore()) {
      <div class="more">
        <button
          nxPending
          type="button"
          class="secondary-button"
          [pending]="loading()"
          (click)="more()"
        >
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
  private readonly items = linkedSignal<
    { commit: string; page: number; data: RepositoryCommitPageDto } | null,
    RepositoryCommitDto[]
  >({
    source: this.latest,
    computation: (read, previous) => {
      if (!read) return [];
      if (read.page === 1) return read.data.items;
      const seen = new Set(previous?.value.map((item) => item.sha));
      return [...(previous?.value ?? []), ...read.data.items.filter((item) => !seen.has(item.sha))];
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
  /** 使用者看得到的重讀（重試）。 */
  readonly refreshing = this.read.refreshing;
  readonly error = this.read.error;
  /** 最多兩個提交；選第三個會取代較舊的那個選擇。 */
  readonly picked = signal<string[]>([]);
  readonly range = computed(() => {
    const [first, second] = this.picked();
    if (!first || !second) return null;
    const items = this.items(),
      firstIndex = items.findIndex((item) => item.sha === first),
      secondIndex = items.findIndex((item) => item.sha === second);
    if (firstIndex < 0 || secondIndex < 0) return null;
    const head = items[Math.min(firstIndex, secondIndex)],
      from = items[Math.max(firstIndex, secondIndex)];
    return {
      head: head.sha,
      from: from.sha,
      base: from.parents[0] ?? from.sha,
      count: Math.abs(firstIndex - secondIndex) + 1,
    };
  });
  readonly within = computed(() => {
    const range = this.range();
    if (!range) return new Set<string>();
    const items = this.items(),
      start = items.findIndex((item) => item.sha === range.head),
      end = items.findIndex((item) => item.sha === range.from);
    return new Set(items.slice(start, end + 1).map((item) => item.sha));
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

  initial = (name: string) => nameInitial(name, '?').toUpperCase();
  reload() {
    this.read.reload();
  }
  more() {
    if (!this.loading()) this.page.update((page) => page + 1);
  }
  pick(sha: string) {
    this.picked.update((current) =>
      current.includes(sha) ? current.filter((item) => item !== sha) : [...current.slice(-1), sha],
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
