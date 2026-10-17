import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  signal,
  viewChild,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { apiResource } from '@core/api/api-resource';
import type { RepositoryCommentDto, RepositoryIssueDto, RepositoryPullDto } from '@core/api/schema';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { formatDate, relativeTime } from '@shared/browser/format';
import { ViewScope } from '@shared/browser/view-scope';
import { MarkdownView } from '@shared/markdown/markdown-view';
import { Icon } from '@shared/ui/icon';
import { Notice } from '@shared/ui/notice';
import { RepositoriesApi } from '../repositories-api';
import { RepositoryReviewDialog } from '../reviews/repository-review-dialog';
import { RepositoryStore } from '../repository-store';
import type { ThreadKind } from './repository-threads';

interface Thread {
  id: string;
  item: RepositoryIssueDto | RepositoryPullDto;
  pull: RepositoryPullDto | null;
  body: string;
  comments: RepositoryCommentDto[];
  head: string;
  base: string;
}

/** 單一議題或拉取請求與其討論；拉取請求可送交 AI 審查。 */
@Component({
  selector: 'nx-repository-thread',
  imports: [RouterLink, Icon, MarkdownView, Notice, RepositoryReviewDialog],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './repository-thread.scss',
  template: `<a class="back" [routerLink]="store.link(kind())"
      ><nx-icon name="left" />{{ kind() === 'pulls' ? 'Pull Request' : '議題' }}</a
    >
    @if (error()) {
      <nx-notice [message]="error()"
        ><button notice-actions type="button" (click)="reload()">重試</button></nx-notice
      >
    }
    @if (thread(); as thread) {
      <header class="head">
        <div class="status">
          <span class="pill" [attr.data-tone]="state().tone"
            ><nx-icon [name]="state().icon" />{{ state().label }}</span
          >
          @for (label of thread.item.labels; track label.name) {
            <span class="label" [style.--label]="label.color || null">{{ label.name }}</span>
          }
        </div>
        <h2>
          {{ thread.item.title }} <span class="number">#{{ thread.item.number }}</span>
        </h2>
        <p class="meta">
          {{ thread.item.author }}
          @if (thread.item.createdAt) {
            於 {{ date(thread.item.createdAt) }}
          }
          開啟
          @if (thread.pull; as pull) {
            · 將 <code>{{ pull.headRef }}</code> 合併到 <code>{{ pull.baseRef }}</code>
          }
        </p>
        <div class="actions">
          @if (thread.pull) {
            <button type="button" class="primary-button" (click)="ask('review')">
              <nx-icon name="review" />AI 檢閱
            </button>
            <button type="button" class="secondary-button" (click)="ask('summary')">
              AI 變更摘要
            </button>
            @if (thread.head && thread.base) {
              <a
                class="quiet-button"
                [routerLink]="store.link('compare', thread.base + '...' + thread.head)"
                ><nx-icon name="compare" />查看變更</a
              >
            }
          }
          @if (canChat()) {
            <button type="button" class="quiet-button" [disabled]="busy()" (click)="chat()">
              <nx-icon name="chat" />{{ busy() ? '正在整理…' : '帶入對話' }}
            </button>
          }
          <a class="quiet-button" [href]="thread.item.url" target="_blank" rel="noopener noreferrer"
            ><nx-icon name="external" />Gitea</a
          >
        </div>
      </header>
      <ol class="timeline">
        <li>
          <span class="avatar" aria-hidden="true">{{ initial(thread.item.author) }}</span>
          <article class="post">
            <header>
              <strong>{{ thread.item.author }}</strong>
              @if (thread.item.createdAt) {
                <time [attr.datetime]="thread.item.createdAt">{{
                  when(thread.item.createdAt)
                }}</time>
              }
            </header>
            @if (thread.body.trim()) {
              <nx-markdown-view [content]="thread.body" />
            } @else {
              <p class="none">沒有說明。</p>
            }
          </article>
        </li>
        @for (comment of thread.comments; track comment.id; let i = $index) {
          <li [style.--i]="i < 10 ? i + 1 : 10">
            <span class="avatar" aria-hidden="true">{{ initial(comment.author) }}</span>
            <article class="post">
              <header>
                <strong>{{ comment.author }}</strong>
                @if (comment.createdAt) {
                  <a [href]="comment.url" target="_blank" rel="noopener noreferrer"
                    ><time [attr.datetime]="comment.createdAt">{{
                      when(comment.createdAt)
                    }}</time></a
                  >
                }
              </header>
              <nx-markdown-view [content]="comment.body" />
            </article>
          </li>
        }
      </ol>
    } @else if (!error()) {
      <div class="loading" role="status" aria-label="正在讀取">
        <span class="skeleton head-skeleton"></span><span class="skeleton body-skeleton"></span>
      </div>
    }
    <nx-repository-review-dialog />`,
})
export class RepositoryThread {
  readonly kind = input.required<ThreadKind>();
  readonly number = input.required<number>();
  readonly store = inject(RepositoryStore);
  private readonly api = inject(RepositoriesApi);
  private readonly session = inject(WorkspaceSession);
  private readonly scope = inject(ViewScope);
  private readonly dialog = viewChild.required(RepositoryReviewDialog);
  private readonly read = apiResource({
    feature: 'repositories',
    params: () => {
      const key = this.store.key();
      return key ? { key, kind: this.kind(), number: this.number() } : undefined;
    },
    loader: async ({ key, kind, number }): Promise<Thread> => {
      const id = `${key.owner}/${key.name}:${kind}:${number}`;
      if (kind === 'pulls') {
        const detail = await this.api.pull(key, number);
        return {
          id,
          item: detail.pull,
          pull: detail.pull,
          body: detail.body,
          comments: detail.comments,
          head: detail.headCommit,
          base: detail.baseCommit,
        };
      }
      const detail = await this.api.issue(key, number);
      return {
        id,
        item: detail.issue,
        pull: null,
        body: detail.body,
        comments: detail.comments,
        head: '',
        base: '',
      };
    },
  });
  readonly thread = computed(() => {
    const read = this.read.value();
    return read?.id === `${this.store.fullName()}:${this.kind()}:${this.number()}` ? read : null;
  });
  readonly state = computed(() => {
    const thread = this.thread();
    const open = thread?.item.state === 'open';
    if (thread?.pull?.merged) return { icon: 'merged', label: '已合併', tone: 'merged' };
    if (thread?.pull)
      return open
        ? { icon: 'pull', label: thread.pull.draft ? '草稿' : '開啟中', tone: 'open' }
        : { icon: 'pull-closed', label: '已關閉', tone: 'closed' };
    return open
      ? { icon: 'issue', label: '開啟中', tone: 'open' }
      : { icon: 'issue-closed', label: '已關閉', tone: 'closed' };
  });
  readonly canChat = computed(() => this.session.has('chat'));
  readonly busy = signal(false);
  private readonly actionError = signal('');
  readonly error = computed(() => this.actionError() || this.read.error());
  readonly date = formatDate;
  readonly when = relativeTime;

  initial = (name: string) => (name || '?').slice(0, 1).toUpperCase();
  reload() {
    this.actionError.set('');
    this.read.reload();
  }
  ask(purpose: 'review' | 'summary') {
    const thread = this.thread();
    if (!thread?.pull) return;
    this.dialog().open({
      label: `#${thread.item.number} ${thread.item.title}`,
      commit: null,
      baseCommit: null,
      pullRequest: thread.item.number,
      purpose,
    });
  }
  async chat() {
    const thread = this.thread();
    if (!thread || this.busy()) return;
    const valid = this.scope.guard();
    this.busy.set(true);
    this.actionError.set('');
    try {
      await this.store.chat(
        {
          commit: null,
          paths: null,
          issue: thread.pull ? null : thread.item.number,
          pull: thread.pull ? thread.item.number : null,
        },
        valid,
      );
    } catch (error2) {
      if (valid()) this.actionError.set(this.scope.message(error2));
    } finally {
      if (valid()) this.busy.set(false);
    }
  }
}
