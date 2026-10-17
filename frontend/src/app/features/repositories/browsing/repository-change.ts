import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  viewChild,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { apiResource } from '@core/api/api-resource';
import { CopyFeedback } from '@shared/browser/copy-feedback';
import { formatDate } from '@shared/browser/format';
import { Icon } from '@shared/ui/icon';
import { Notice } from '@shared/ui/notice';
import { RepositoriesApi } from '../repositories-api';
import { RepositoryDiff } from './repository-diff';
import { RepositoryReviewDialog } from '../reviews/repository-review-dialog';
import { RepositoryStore, shortSha } from '../repository-store';

/** 單一提交與其父提交的比較，或從 `base` 到 `head` 之間的全部變更。 */
@Component({
  selector: 'nx-repository-change',
  imports: [RouterLink, Icon, Notice, RepositoryDiff, RepositoryReviewDialog],
  providers: [CopyFeedback],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './repository-change.scss',
  template: `<a class="back" [routerLink]="store.link('commits')" queryParamsHandling="preserve"
      ><nx-icon name="left" />提交紀錄</a
    >
    @if (error()) {
      <nx-notice [message]="error()"
        ><button notice-actions type="button" (click)="reload()">重試</button></nx-notice
      >
    }
    @if (change(); as change) {
      <header class="head">
        @if (change.base; as from) {
          <p class="eyebrow"><nx-icon name="compare" />區間變更</p>
          <h2>
            {{ from.title || short(from.sha) }} 之後 →
            {{ change.head.title || short(change.head.sha) }}
          </h2>
        } @else {
          <p class="eyebrow"><nx-icon name="commit" />單一提交</p>
          <h2>{{ change.head.title || '（沒有說明）' }}</h2>
          @if (body(); as body) {
            <p class="message">{{ body }}</p>
          }
        }
        <div class="facts">
          <span class="who"
            ><span class="avatar" aria-hidden="true">{{ initial() }}</span
            >{{ change.head.author }}</span
          >
          @if (change.head.at) {
            <span>{{ date(change.head.at) }}</span>
          }
          @if (change.base; as from) {
            <code class="sha">{{ short(from.sha) }}</code
            ><nx-icon name="chevron" class="to" />
          }
          <button
            type="button"
            class="sha"
            [title]="'複製 ' + change.head.sha"
            (click)="copy.copy(change.head.sha)"
          >
            <nx-icon [name]="copy.copied() ? 'check' : 'copy'" />{{ short(change.head.sha) }}
          </button>
          @if (!change.base) {
            @for (parent of change.head.parents; track parent) {
              <a class="parent" [routerLink]="store.link('commits', parent)"
                >父提交 {{ short(parent) }}</a
              >
            }
          }
        </div>
        <div class="actions">
          <button
            type="button"
            class="primary-button"
            [disabled]="change.tooLarge"
            (click)="ask('summary')"
          >
            <nx-icon name="review" />AI 變更摘要
          </button>
          <button
            type="button"
            class="secondary-button"
            [disabled]="change.tooLarge"
            (click)="ask('review')"
          >
            AI 檢閱
          </button>
          <a class="quiet-button" [routerLink]="store.link()" [queryParams]="{ ref: head() }"
            ><nx-icon name="file-code" />這個版本的程式碼</a
          >
          <a class="quiet-button" [href]="giteaUrl()" target="_blank" rel="noopener noreferrer"
            ><nx-icon name="external" />Gitea</a
          >
        </div>
      </header>
      @if (change.tooLarge) {
        <nx-notice
          tone="warning"
          message="差異超過 250 KB，無法在這裡顯示，也無法交給 AI。請縮小區間，或到 Gitea 查看。"
        />
      } @else {
        <nx-repository-diff [diff]="change.diff" />
      }
    } @else if (!error()) {
      <div class="loading" role="status" aria-label="正在讀取變更">
        <span class="skeleton head-skeleton"></span><span class="skeleton diff-skeleton"></span>
      </div>
    }
    <nx-repository-review-dialog />`,
})
export class RepositoryChange {
  readonly head = input.required<string>();
  readonly base = input('');
  readonly store = inject(RepositoryStore);
  readonly copy = inject(CopyFeedback);
  private readonly api = inject(RepositoriesApi);
  private readonly dialog = viewChild.required(RepositoryReviewDialog);
  private readonly read = apiResource({
    feature: 'repositories',
    params: () => {
      const key = this.store.key();
      return key ? { key, head: this.head(), base: this.base() } : undefined;
    },
    loader: async ({ key, head, base }) => ({
      id: `${base}...${head}`,
      value: await this.api.diff(key, head, base || null),
    }),
  });
  readonly change = computed(() => {
    const read = this.read.value();
    return read?.id === `${this.base()}...${this.head()}` ? read.value : null;
  });
  readonly error = this.read.error;
  readonly body = computed(() => {
    const message = this.change()?.head.message ?? '';
    const newline = message.indexOf('\n');
    return newline < 0 ? '' : message.slice(newline + 1).trim();
  });
  readonly initial = computed(() => (this.change()?.head.author || '?').slice(0, 1).toUpperCase());
  readonly giteaUrl = computed(() => {
    const url = this.store.detail()?.repository.url ?? '';
    return this.base()
      ? `${url}/compare/${this.base()}...${this.head()}`
      : `${url}/commit/${this.head()}`;
  });
  readonly short = shortSha;
  readonly date = formatDate;

  reload() {
    this.read.reload();
  }
  ask(purpose: 'review' | 'summary') {
    const change = this.change();
    if (!change) return;
    this.dialog().open({
      label: change.base
        ? `${shortSha(change.base.sha)} 之後 → ${shortSha(change.head.sha)}`
        : `${shortSha(change.head.sha)} ${change.head.title}`,
      commit: change.head.sha,
      baseCommit: change.base?.sha ?? null,
      pullRequest: null,
      purpose,
    });
  }
}
