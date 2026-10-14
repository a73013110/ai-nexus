import { ChangeDetectionStrategy, Component, computed, inject, viewChild } from '@angular/core';
import { RouterLink } from '@angular/router';
import { apiResource } from '@core/api/api-resource';
import type { RepositoryReviewDto } from '@core/api/schema';
import { formatModelDisplayName, relativeTime } from '@shared/browser/format';
import { Icon } from '@shared/ui/icon';
import { Notice } from '@shared/ui/notice';
import { StatusBadge } from '@shared/ui/status-badge';
import { jobStatus } from '@features/jobs/job-kind';
import { RepositoriesApi } from '../repositories-api';
import { RepositoryReviewDialog } from './repository-review-dialog';
import { RepositoryStore, shortSha } from '../repository-store';

export const purposeName = (purpose: string) => (purpose === 'summary' ? '變更摘要' : '快速檢閱');

/** What a review read, in a few words. */
export function reviewScope(review: RepositoryReviewDto) {
  if (review.pullRequest) return `Pull Request #${review.pullRequest}`;
  return review.baseCommit
    ? `${shortSha(review.baseCommit)} 之後 → ${shortSha(review.commit)}`
    : `提交 ${shortSha(review.commit)}`;
}

/** The repository's AI reviews, newest first. */
@Component({
  selector: 'nx-repository-reviews',
  imports: [RouterLink, Icon, Notice, StatusBadge, RepositoryReviewDialog],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './repository-reviews.scss',
  template: `<div class="intro">
      <p>從提交、區間或 Pull Request 交給 AI，在背景產生檢閱或摘要；完成時會通知你。</p>
      <button
        type="button"
        class="primary-button"
        [disabled]="!store.commit()"
        (click)="startCurrent()"
      >
        <nx-icon name="review" />檢閱目前版本
      </button>
    </div>
    @if (error()) {
      <nx-notice [message]="error()"
        ><button notice-actions type="button" (click)="reload()">重試</button></nx-notice
      >
    }
    @if (reviews(); as reviews) {
      @if (reviews.length) {
        <ol class="list">
          @for (review of reviews; track review.id; let i = $index) {
            <li [style.--i]="i < 12 ? i : 12">
              <a class="row" [routerLink]="store.link('reviews', review.id)">
                <span class="mark" [attr.data-purpose]="review.purpose"
                  ><nx-icon [name]="review.purpose === 'summary' ? 'document' : 'review'"
                /></span>
                <span class="main">
                  <strong>{{ purpose(review.purpose) }} · {{ scope(review) }}</strong>
                  <span class="meta"
                    >{{ model(review) }} · {{ when(review.createdAt) }}
                    @if (review.commentUrl) {
                      · 已貼到 PR
                    }
                    @if (review.note) {
                      · 「{{ review.note }}」
                    }
                  </span>
                </span>
                <nx-status-badge [tone]="status(review).tone">{{
                  status(review).label
                }}</nx-status-badge>
              </a>
            </li>
          }
        </ol>
      } @else {
        <div class="blank">
          <span class="halo" aria-hidden="true"><nx-icon name="review" /></span>
          <h3>還沒有 AI 檢閱</h3>
          <p>在「提交紀錄」選一個提交或區間，或打開一個 Pull Request，就能交給 AI。</p>
        </div>
      }
    } @else if (!error()) {
      @for (row of [1, 2, 3]; track row) {
        <span class="skeleton row-skeleton"></span>
      }
    }
    <nx-repository-review-dialog />`,
})
export class RepositoryReviews {
  readonly store = inject(RepositoryStore);
  private readonly api = inject(RepositoriesApi);
  private readonly dialog = viewChild.required(RepositoryReviewDialog);
  private readonly read = apiResource({
    feature: 'repositories',
    params: () => this.store.key() ?? undefined,
    loader: async (key) => ({
      name: `${key.owner}/${key.name}`,
      rows: await this.api.reviews(key),
    }),
  });
  readonly reviews = computed(() => {
    const read = this.read.value();
    return read && read.name === this.store.fullName() ? read.rows : null;
  });
  readonly error = this.read.error;
  readonly purpose = purposeName;
  readonly scope = reviewScope;
  readonly model = formatModelDisplayName;
  readonly when = relativeTime;
  status = (review: RepositoryReviewDto) => jobStatus(review.job.status);

  reload() {
    this.read.reload();
  }
  startCurrent() {
    const commit = this.store.commit();
    if (commit)
      this.dialog().open({
        label: `${this.store.refName()} 的最新提交 ${shortSha(commit)}`,
        commit,
        baseCommit: null,
        pullRequest: null,
      });
  }
}
