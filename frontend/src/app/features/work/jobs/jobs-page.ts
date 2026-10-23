import { Notice } from '@shared/ui/notice';
import { EmptyState } from '@shared/ui/empty-state';
import { ResourceGallery } from '@shared/ui/resource-gallery';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { WorkspaceSession } from '@core/auth/workspace-session';
import type { JobDto } from '@core/api/schema';
import { ViewScope } from '@shared/browser/view-scope';
import { formatDate, relativeTime } from '@shared/browser/format';
import { FeaturePage } from '@core/layout/feature-page';
import { Icon } from '@shared/ui/icon';
import { JobProgress } from './job-progress';
import { PendingButton } from '@shared/ui/pending-button';
import { ResourceTarget } from '@shared/ui/resource-target';
import { JobsApi } from './jobs-api';
import { jobKindIcon, jobKindName } from './job-kind';
import { JobToasts } from './job-toasts';
import { apiResource } from '@core/api/api-resource';
import { jobsTour } from './jobs-tour';
import { routeOf } from '@core/api/catalog';
import { isActive } from '@core/api/generation-status';

/** 「需要處理」篩選與可重試的狀態。 */
const needsAttention = (status: string) => status === 'failed' || status === 'cancelled';

@Component({
  selector: 'nx-jobs-page',
  imports: [
    Notice,
    EmptyState,
    ResourceGallery,
    FeaturePage,
    Icon,
    JobProgress,
    PendingButton,
    RouterLink,
    ResourceTarget,
  ],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './jobs-page.scss',
  template: `<nx-feature-page
    [title]="session.featureName('jobs')"
    description="追蹤文件辨識、索引、評測與程式碼 review。離開頁面後任務仍會繼續。"
    [tour]="tour()"
  >
    @if (error()) {
      <nx-notice [message]="error()"
        ><button
          notice-actions
          nxPending
          type="button"
          [pending]="jobs.refreshing()"
          (click)="reload()"
        >
          {{ jobs.refreshing() ? '重試中…' : '重試' }}
        </button></nx-notice
      >
    }
    <nx-resource-gallery
      label="任務"
      placeholder="尋找任務"
      [search]="term()"
      [filters]="filterOptions"
      [filter]="filter()"
      [loading]="jobs.loading()"
      [busy]="jobs.refreshing()"
      [empty]="!visible().length"
      (searchChange)="term.set($event)"
      (filterChange)="filter.set($event)"
    >
      @for (job of visible(); track job.id) {
        <article
          class="job-card"
          [id]="'job-' + job.id"
          [class.current]="target() === job.id"
          [nxResourceTarget]="target() === job.id"
          animate.enter="panel-arrive"
        >
          <div class="job-top">
            <span class="job-icon"><nx-icon [name]="icon(job.kind)" /></span>
            <span class="job-kind">{{ kind(job.kind) }}</span>
            <time [attr.datetime]="job.createdAt" [title]="date(job.createdAt)">{{
              when(job.createdAt)
            }}</time>
          </div>
          <h2>{{ job.label }}</h2>
          <nx-job-progress [job]="job" />
          <div class="job-actions">
            <span class="job-attempt">{{
              job.attempt ? '第 ' + job.attempt + ' 次處理' : '尚未開始'
            }}</span>
            @if (opensReview(job)) {
              <a
                class="quiet-button job-result"
                [routerLink]="routeOf('repositories')"
                [queryParams]="{ review: job.subjectId }"
                >檢視 review<nx-icon name="chevron"
              /></a>
            }
            @if (job.kind === 'document-ingest') {
              <a
                class="quiet-button job-result"
                [routerLink]="[routeOf('knowledge.reader'), job.subjectId]"
                >檢視文件<nx-icon name="chevron"
              /></a>
            }
            @if (canStop(job)) {
              <button
                nxPending
                type="button"
                class="quiet-button job-control"
                [pending]="busy().has(job.id)"
                [disabled]="job.cancelRequested && !busy().has(job.id)"
                (click)="act(job, false)"
              >
                <nx-icon name="stop" />{{ busy().has(job.id) ? '停止中…' : '停止處理' }}
              </button>
            } @else if (canRetry(job)) {
              <button
                nxPending
                type="button"
                class="quiet-button job-control"
                [pending]="busy().has(job.id)"
                (click)="act(job, true)"
              >
                <nx-icon name="repeat" />{{ busy().has(job.id) ? '重試中…' : '重試' }}
              </button>
            }
          </div>
        </article>
      }
      <nx-empty-state gallery-empty>
        <nx-icon name="check" />
        @if (term()) {
          <h2>沒有符合的任務</h2>
        } @else {
          <h2>{{ filter() === 'active' ? '目前沒有處理中的任務' : '這裡尚無任務' }}</h2>
          <p>加入知識庫文件或執行評測後，即可在這裡追蹤。</p>
          @if (session.has('knowledge')) {
            <a class="secondary-button" [routerLink]="routeOf('knowledge')"
              >前往{{ session.featureName('knowledge') }}</a
            >
          }
        }
      </nx-empty-state>
    </nx-resource-gallery>
  </nx-feature-page>`,
})
export class JobsPage {
  /** 範本裡的連結由功能目錄取得網址。 */
  readonly routeOf = routeOf;
  readonly session = inject(WorkspaceSession);
  private readonly scope = inject(ViewScope);
  private readonly api = inject(JobsApi);
  private readonly jobToasts = inject(JobToasts);
  readonly target = signal(inject(ActivatedRoute).snapshot.queryParamMap.get('job') || '');
  /** 任務每幾秒重新讀取一次；不在最近列表中的已連結任務會加入其中。 */
  readonly jobs = apiResource({
    feature: 'jobs',
    params: () => this.target(),
    loader: async (target, signal) => {
      const jobs = await this.api.list(signal);
      if (target && !jobs.some((job) => job.id === target))
        jobs.push(await this.api.get(target, signal));
      return jobs;
    },
    poll: () => 3000,
  });
  readonly filter = signal(this.target() ? 'all' : 'active');
  private readonly actionError = signal('');
  readonly error = computed(() => this.actionError() || this.jobs.error());
  /** 送出停止或重試中的任務：只有那張卡片的按鈕轉圈，其他卡片照常可操作。 */
  readonly busy = signal<ReadonlySet<string>>(new Set());
  readonly filterOptions = [
    { value: 'active', label: '處理中' },
    { value: 'all', label: '全部' },
    { value: 'attention', label: '需要處理' },
    { value: 'completed', label: '已完成' },
  ];
  readonly term = signal('');
  readonly visible = computed(() =>
    (this.jobs.value() ?? []).filter(
      (job) =>
        job.label.toLowerCase().includes(this.term().toLowerCase()) &&
        (this.filter() === 'all' ||
          (this.filter() === 'active'
            ? isActive(job.status)
            : this.filter() === 'attention'
              ? needsAttention(job.status)
              : job.status === 'completed')),
    ),
  );
  /** 列表的導覽；卡片步驟依目前篩選顯示的卡片與動作而定。 */
  readonly tour = computed(() => {
    const jobs = this.jobs.loading() ? [] : this.visible();
    return jobsTour({
      cards: jobs.length > 0,
      results: jobs.some((job) => job.kind === 'document-ingest' || this.opensReview(job)),
      controls: jobs.some((job) => this.canStop(job) || this.canRetry(job)),
    });
  });
  constructor() {
    effect(() => this.jobs.value()?.forEach((job) => this.jobToasts.observe(job)));
    inject(ActivatedRoute)
      .queryParamMap.pipe(takeUntilDestroyed())
      .subscribe((params) => {
        const id = params.get('job') || '';
        if (id === this.target()) return;
        this.target.set(id);
        if (id) this.filter.set('all');
      });
  }
  opensReview(job: JobDto) {
    return job.kind === 'repository-review' && this.session.has('repositories');
  }
  canStop(job: JobDto) {
    return isActive(job.status);
  }
  canRetry(job: JobDto) {
    return needsAttention(job.status) && job.attempt < 6;
  }
  reload() {
    this.actionError.set('');
    this.jobs.reload();
  }
  async act(job: JobDto, retry: boolean) {
    if (this.busy().has(job.id)) return;
    const valid = this.scope.guard();
    this.busy.update((ids) => new Set(ids).add(job.id));
    this.actionError.set('');
    try {
      const value = await this.jobToasts.control(job.id, retry);
      if (valid())
        this.jobs.value.update((items) =>
          items?.map((item) => (item.id === value.id ? value : item)),
        );
    } catch (error) {
      if (valid()) this.actionError.set(this.scope.message(error));
    } finally {
      if (valid())
        this.busy.update((ids) => {
          const next = new Set(ids);
          next.delete(job.id);
          return next;
        });
    }
  }
  readonly kind = jobKindName;
  readonly icon = jobKindIcon;
  readonly date = formatDate;
  readonly when = relativeTime;
}
