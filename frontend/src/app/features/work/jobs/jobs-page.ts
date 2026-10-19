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
import { ResourceTarget } from '@shared/ui/resource-target';
import { JobsApi } from './jobs-api';
import { jobKindIcon, jobKindName } from './job-kind';
import { JobToasts } from './job-toasts';
import { apiResource } from '@core/api/api-resource';
import { jobsTour } from './jobs-tour';
import { routeOf } from '@core/api/catalog';

@Component({
  selector: 'nx-jobs-page',
  imports: [
    Notice,
    EmptyState,
    ResourceGallery,
    FeaturePage,
    Icon,
    JobProgress,
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
        ><button notice-actions type="button" (click)="reload()">重試</button></nx-notice
      >
    }
    <nx-resource-gallery
      label="任務"
      placeholder="尋找任務"
      [search]="term()"
      [filters]="filterOptions"
      [filter]="filter()"
      [loading]="jobs.loading()"
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
                class="quiet-button job-control"
                [disabled]="busy() === job.id || job.cancelRequested"
                (click)="act(job, false)"
              >
                <nx-icon name="stop" />停止處理
              </button>
            } @else if (canRetry(job)) {
              <button
                class="quiet-button job-control"
                [disabled]="busy() === job.id"
                (click)="act(job, true)"
              >
                <nx-icon name="repeat" />重試
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
    loader: async (target) => {
      const jobs = await this.api.list();
      if (target && !jobs.some((item2) => item2.id === target))
        jobs.push(await this.api.get(target));
      return jobs;
    },
    poll: () => 3000,
  });
  readonly filter = signal(this.target() ? 'all' : 'active');
  readonly actionError = signal('');
  readonly error = computed(() => this.actionError() || this.jobs.error());
  readonly busy = signal<string | null>(null);
  readonly filterOptions = [
    { value: 'active', label: '處理中' },
    { value: 'all', label: '全部' },
    { value: 'attention', label: '需要處理' },
    { value: 'completed', label: '已完成' },
  ];
  readonly term = signal('');
  readonly visible = computed(() =>
    (this.jobs.value() ?? []).filter(
      (item3) =>
        item3.label.toLowerCase().includes(this.term().toLowerCase()) &&
        (this.filter() === 'all' ||
          (this.filter() === 'active'
            ? ['queued', 'running'].includes(item3.status)
            : this.filter() === 'attention'
              ? ['failed', 'cancelled'].includes(item3.status)
              : item3.status === 'completed')),
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
    return ['queued', 'running'].includes(job.status);
  }
  canRetry(job: JobDto) {
    return ['failed', 'cancelled'].includes(job.status) && job.attempt < 6;
  }
  reload() {
    this.actionError.set('');
    this.jobs.reload();
  }
  async act(job: JobDto, retry: boolean) {
    if (this.busy()) return;
    const valid = this.scope.guard();
    this.busy.set(job.id);
    this.actionError.set('');
    try {
      const value = await (retry ? this.api.retry(job.id) : this.api.cancel(job.id));
      if (retry) this.jobToasts.track(value);
      if (valid())
        this.jobs.value.update((items) =>
          items?.map((item) => (item.id === value.id ? value : item)),
        );
    } catch (error) {
      if (valid()) this.actionError.set(this.scope.message(error));
    } finally {
      if (valid()) this.busy.set(null);
    }
  }
  readonly kind = jobKindName;
  readonly icon = jobKindIcon;
  readonly date = formatDate;
  readonly when = relativeTime;
}
