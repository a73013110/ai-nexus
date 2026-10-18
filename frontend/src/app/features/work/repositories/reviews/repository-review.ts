import {
  ChangeDetectionStrategy,
  Component,
  computed,
  ElementRef,
  inject,
  input,
  signal,
  viewChild,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { apiResource } from '@core/api/api-resource';
import type { RepositoryReviewDetailDto, RepositoryReviewDto } from '@core/api/schema';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { ClientValidationError } from '@core/errors/safe-errors';
import { formatDate, formatModelDisplayName, formatNumber } from '@shared/browser/format';
import { ViewScope } from '@shared/browser/view-scope';
import { MarkdownView } from '@shared/markdown/markdown-view';
import { DialogHeader } from '@shared/ui/dialog-header';
import { CompactDialog } from '@shared/ui/compact-dialog';
import { Field } from '@shared/ui/field';
import { Icon } from '@shared/ui/icon';
import { Notice } from '@shared/ui/notice';
import { ToastService } from '@shared/ui/toast-service';
import { JobProgress } from '@features/work/jobs/job-progress';
import { JobToasts } from '@features/work/jobs/job-toasts';
import { RepositoriesApi } from '../repositories-api';
import { purposeName, reviewScope } from './repository-reviews';
import { RepositoryStore } from '../repository-store';

const commentLimit = 20_000;

/** 一次審查：執行中顯示進度，之後是報告與它讀取的證據。 */
@Component({
  selector: 'nx-repository-review',
  imports: [
    RouterLink,
    CompactDialog,
    DialogHeader,
    Field,
    Icon,
    JobProgress,
    MarkdownView,
    Notice,
  ],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './repository-review.scss',
  template: `<a class="back" [routerLink]="store.link('reviews')"><nx-icon name="left" />AI 檢閱</a>
    @if (error()) {
      <nx-notice [message]="error()"
        ><button notice-actions type="button" (click)="reload()">重試</button></nx-notice
      >
    }
    @if (detail(); as d) {
      <header class="head">
        <p class="eyebrow">
          <nx-icon [name]="d.review.purpose === 'summary' ? 'document' : 'review'" />{{
            purpose(d.review.purpose)
          }}
        </p>
        <h2>{{ scope(d.review) }}</h2>
        <p class="meta">
          {{ model(d.review) }} · {{ date(d.review.createdAt) }}
          @if (d.review.note) {
            · 關注「{{ d.review.note }}」
          }
        </p>
        <nx-job-progress [job]="d.review.job">
          @if (active()) {
            <button
              job-actions
              type="button"
              class="quiet-button"
              [disabled]="busy() || d.review.job.cancelRequested"
              (click)="changeJob(false)"
            >
              停止
            </button>
          } @else if (d.review.job.status === 'failed' || d.review.job.status === 'cancelled') {
            <button
              job-actions
              type="button"
              class="quiet-button"
              [disabled]="busy() || d.review.job.attempt >= 6"
              (click)="changeJob(true)"
            >
              沿用已完成的分析重試
            </button>
          }
        </nx-job-progress>
        <div class="actions">
          <a class="quiet-button" [routerLink]="changeLink(d.review)"
            ><nx-icon [name]="d.review.pullRequest ? 'pull' : 'compare'" />{{
              d.review.pullRequest ? '打開 Pull Request' : '查看變更'
            }}</a
          >
          @if (d.review.commentUrl; as url) {
            <a class="quiet-button" [href]="url" target="_blank" rel="noopener noreferrer"
              ><nx-icon name="check" />已貼到 PR · 查看留言</a
            >
          } @else if (d.review.pullRequest && d.report && d.review.job.status === 'completed') {
            <button type="button" class="secondary-button" (click)="openComment()">
              <nx-icon name="lines" />貼到 PR 留言
            </button>
          }
          @if (session.has('tasks')) {
            <a class="quiet-button" routerLink="/tasks" [queryParams]="{ job: d.review.job.id }"
              >背景任務</a
            >
          }
        </div>
      </header>
      <article class="report" aria-label="整體報告">
        @if (d.report; as report) {
          <nx-markdown-view [content]="report.output" />
          @if (report.truncated) {
            <nx-notice
              tone="warning"
              message="部分分析達到模型輸出上限，結果不完整。請縮小範圍或改用其他模型。"
            />
          }
          <p class="usage">
            輸入 {{ tokens(report.inputTokens) }} · 輸出 {{ tokens(report.outputTokens) }} tokens ·
            {{ seconds(report.elapsedMs) }} 秒
          </p>
        } @else {
          <p class="pending" role="status">
            {{
              active()
                ? '正在逐段閱讀變更並彙整報告，完成後會顯示結論與重點。'
                : '報告尚未完成，可以沿用已完成的分析重試。'
            }}
          </p>
        }
        @if (binary()) {
          <nx-notice
            tone="warning"
            [message]="'包含 ' + binary() + ' 個二進位區段，AI 無法閱讀，請人工確認原檔。'"
          />
        }
      </article>
      @if (d.sections.length) {
        <section class="evidence" aria-label="分析明細">
          <h3>
            分段明細 <span>{{ d.sections.length }}</span>
          </h3>
          @for (section of d.sections; track section.ordinal) {
            <details (toggle)="toggle(section.ordinal, $any($event.target).open)">
              <summary>
                <nx-icon name="chevron" class="caret" /><span class="label">{{
                  section.label
                }}</span>
                @if (section.binary) {
                  <span class="tag">二進位</span>
                } @else if (section.output) {
                  <nx-icon name="check" class="done" />
                }
              </summary>
              @if (expanded().has(section.ordinal)) {
                <div class="section">
                  @if (section.output) {
                    <nx-markdown-view [content]="section.output" />
                  }
                  @if (section.truncated) {
                    <nx-notice tone="warning" message="這段的分析達到模型輸出上限，結果不完整。" />
                  }
                  <pre class="diff"><code>{{ section.diff }}</code></pre>
                </div>
              }
            </details>
          }
        </section>
      }
      <p class="disclaimer">結果來自變更的靜態分析，仍需核對上下文並執行測試。</p>
    } @else if (!error()) {
      <div class="loading" role="status" aria-label="正在讀取 review">
        <span class="skeleton head-skeleton"></span><span class="skeleton body-skeleton"></span>
      </div>
    }
    <dialog
      nxCompactDialog
      size="lg"
      #comment
      class="platform-dialog"
      (cancel)="posting() ? $event.preventDefault() : null"
    >
      <form class="dialog-form" (submit)="post($event)">
        <nx-dialog-header [heading]="'貼到 Pull Request #' + detail()?.review?.pullRequest" />
        <div class="dialog-body">
          <p class="form-note">
            會以你的 Gitea 帳號留言，所有能看到這個 PR 的人都看得到；每份 review 只能貼一次。
          </p>
          <label
            >留言內容<textarea
              nxField
              rows="14"
              [maxLength]="limit"
              [value]="draft()"
              (input)="draft.set($any($event.target).value)"
            ></textarea>
          </label>
          <p class="form-note count">{{ number(draft().length) }} / {{ number(limit) }}</p>
          @if (commentError()) {
            <nx-notice tone="danger" [message]="commentError()" />
          }
        </div>
        <footer class="dialog-foot">
          <button
            type="button"
            class="secondary-button"
            [disabled]="posting()"
            (click)="comment.close()"
          >
            取消
          </button>
          <button class="primary-button" [disabled]="posting() || !draft().trim()">
            {{ posting() ? '正在留言…' : '留言' }}
          </button>
        </footer>
      </form>
    </dialog>`,
})
export class RepositoryReview {
  readonly id = input.required<string>();
  readonly store = inject(RepositoryStore);
  readonly session = inject(WorkspaceSession);
  private readonly api = inject(RepositoriesApi);
  private readonly scopeGuard = inject(ViewScope);
  private readonly jobs = inject(JobToasts);
  private readonly toast = inject(ToastService);
  private readonly commentDialog = viewChild.required<ElementRef<HTMLDialogElement>>('comment');
  /** 排隊中或執行中的審查每 3 秒重新讀取一次。 */
  private readonly read = apiResource({
    feature: 'repositories',
    params: () => {
      const repository = this.store.fullName();
      return repository ? { id: this.id(), repository } : undefined;
    },
    loader: async ({ id, repository }) => {
      const detail = await this.api.review(id);
      if (detail.review.repository !== repository)
        throw new ClientValidationError('reviewRepository');
      return detail;
    },
    poll: (detail) =>
      ['queued', 'running'].includes(detail?.review.job.status ?? '') ? 3000 : null,
  });
  readonly detail = computed<RepositoryReviewDetailDto | null>(() => {
    const detail = this.read.value();
    return detail?.review.id === this.id() ? detail : null;
  });
  readonly active = computed(() =>
    ['queued', 'running'].includes(this.detail()?.review.job.status ?? ''),
  );
  readonly binary = computed(
    () => this.detail()?.sections.filter((item) => item.binary).length ?? 0,
  );
  readonly expanded = signal<ReadonlySet<number>>(new Set());
  readonly busy = signal(false);
  private readonly actionError = signal('');
  readonly error = computed(() => this.actionError() || this.read.error());
  readonly draft = signal('');
  readonly posting = signal(false);
  readonly commentError = signal('');
  readonly limit = commentLimit;
  readonly purpose = purposeName;
  readonly scope = reviewScope;
  readonly model = formatModelDisplayName;
  readonly date = formatDate;
  readonly number = formatNumber;

  tokens = (value: number | null) => (value == null ? '—' : formatNumber(value));
  seconds = (ms: number) => (ms / 1000).toFixed(1);
  changeLink(review: RepositoryReviewDto) {
    if (review.pullRequest) return this.store.link('pulls', review.pullRequest);
    return review.baseCommit
      ? this.store.link('compare', `${review.baseCommit}...${review.commit}`)
      : this.store.link('commits', review.commit);
  }
  reload() {
    this.actionError.set('');
    this.read.reload();
  }
  toggle(ordinal: number, open: boolean) {
    this.expanded.update((current) => {
      const next = new Set(current);
      if (open) next.add(ordinal);
      else next.delete(ordinal);
      return next;
    });
  }
  async changeJob(retry: boolean) {
    const id = this.detail()?.review.id;
    if (!id || this.busy()) return;
    const valid = this.scopeGuard.guard();
    this.busy.set(true);
    this.actionError.set('');
    try {
      const job = await this.api.reviewJob(id, retry);
      if (retry) this.jobs.track(job);
      if (valid()) this.read.reload();
    } catch (error2) {
      if (valid()) this.actionError.set(this.scopeGuard.message(error2));
    } finally {
      if (valid()) this.busy.set(false);
    }
  }
  openComment() {
    const detail = this.detail();
    if (!detail?.report) return;
    const header = `> **AI ${purposeName(detail.review.purpose)}**（${formatModelDisplayName(detail.review)}）— 僅供參考，仍需人工確認。\n\n`;
    this.draft.set((header + detail.report.output).slice(0, commentLimit));
    this.commentError.set('');
    this.commentDialog().nativeElement.showModal();
  }
  async post(event: Event) {
    event.preventDefault();
    const id = this.detail()?.review.id,
      body = this.draft().trim();
    if (!id || !body || this.posting()) return;
    const valid = this.scopeGuard.guard();
    this.posting.set(true);
    this.commentError.set('');
    try {
      const review = await this.api.comment(id, body);
      if (!valid()) return;
      this.read.value.update((item2) => (item2 ? { ...item2, review } : item2));
      this.commentDialog().nativeElement.close();
      this.toast.success('已貼到 Pull Request');
    } catch (error3) {
      if (valid()) this.commentError.set(this.scopeGuard.message(error3));
    } finally {
      if (valid()) this.posting.set(false);
    }
  }
}
