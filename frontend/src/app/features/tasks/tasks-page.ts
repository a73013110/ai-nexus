import { Notice } from '../../shared/ui/notice';
import { EmptyState } from '../../shared/ui/empty-state';
import { Card } from '../../shared/ui/card';
import { FilterPanel } from '../../shared/ui/filter-panel';
import { ViewSwitch } from '../../shared/ui/view-switch';
import { ClientValidationError } from '../../core/errors/safe-errors';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { WorkspaceSession } from '../../core/auth/workspace-session';
import type { JobDto } from '../../core/api/schema';
import { ViewScope } from '../../shared/browser/view-scope';
import { formatDate } from '../../shared/browser/format';
import { FeaturePage } from '../../core/layout/feature-page';
import { Icon } from '../../shared/ui/icon';
import { JobProgress } from './job-progress';
import { ResourceTarget } from '../../shared/ui/resource-target';
import { JobsApi } from './jobs-api';

@Component({
  selector: 'nx-tasks-page',
  imports: [
    Notice,
    Card,
    EmptyState,
    FilterPanel,
    ViewSwitch,
    FeaturePage,
    Icon,
    JobProgress,
    RouterLink,
    ResourceTarget,
  ],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<nx-feature-page
    [title]="session.featureName('tasks')"
    description="追蹤文件辨識、索引、評測與程式碼 review。離開頁面後任務仍會繼續。"
  >
    <button page-actions class="secondary-button" (click)="refresh()">
      <nx-icon name="repeat" />重新整理
    </button>
    @if (error()) {
      <nx-notice tone="danger" [message]="error()" />
    }
    <nx-filter-panel
      ><nx-view-switch
        label="任務篩選"
        [options]="filterOptions"
        [value]="filter()"
        (valueChange)="filter.set($event)"
    /></nx-filter-panel>
    @if (loading()) {
      <p role="status" class="form-note">正在載入任務…</p>
    } @else if (!visible().length) {
      <nx-empty-state>
        <nx-icon name="check" />
        <h2>{{ filter() === 'active' ? '目前沒有處理中的任務' : '這裡尚無任務' }}</h2>
        <p>加入知識庫文件或執行評測後，即可在這裡追蹤。</p>
        @if (session.has('knowledge')) {
          <a class="secondary-button" routerLink="/knowledge"
            >前往{{ session.featureName('knowledge') }}</a
          >
        }
      </nx-empty-state>
    } @else {
      <div class="job-list">
        @for (job of visible(); track job.id) {
          <article
            nxCard
            class="job-card"
            [id]="'job-' + job.id"
            [class.current]="target() === job.id"
            [nxResourceTarget]="target() === job.id"
            animate.enter="panel-arrive"
          >
            <div class="job-heading">
              <div>
                <span class="panel-eyebrow">{{ kind(job.kind) }}</span>
                <h2>{{ job.label }}</h2>
              </div>
              <time>{{ date(job.createdAt) }}</time>
            </div>
            <nx-job-progress [job]="job" />
            <div class="job-actions">
              <span class="form-note">{{
                job.attempt ? '第 ' + job.attempt + ' 次處理' : '尚未開始'
              }}</span>
              @if (job.kind === 'repository-review' && session.has('repositories')) {
                <a
                  class="quiet-button"
                  routerLink="/repositories"
                  [queryParams]="{ review: job.subjectId }"
                  >檢視 review<nx-icon name="chevron"
                /></a>
              }
              @if (job.kind === 'document-ingest') {
                <a class="quiet-button" [routerLink]="['/reader', job.subjectId]"
                  >檢視文件<nx-icon name="chevron"
                /></a>
              }
              @if (['queued', 'running'].includes(job.status)) {
                <button
                  class="secondary-button"
                  [disabled]="busy() === job.id || job.cancelRequested"
                  (click)="act(job, false)"
                >
                  停止處理
                </button>
              } @else if (['failed', 'cancelled'].includes(job.status) && job.attempt < 6) {
                <button
                  class="secondary-button"
                  [disabled]="busy() === job.id"
                  (click)="act(job, true)"
                >
                  <nx-icon name="repeat" />重試
                </button>
              }
            </div>
          </article>
        }
      </div>
    }
  </nx-feature-page>`,
})
export class TasksPage {
  readonly session = inject(WorkspaceSession);
  private readonly scope = inject(ViewScope);
  private readonly api = inject(JobsApi);
  readonly jobs = signal<JobDto[]>([]);
  readonly target = signal(inject(ActivatedRoute).snapshot.queryParamMap.get('job') || '');
  readonly filter = signal(this.target() ? 'all' : 'active');
  readonly loading = signal(true);
  readonly error = signal('');
  readonly busy = signal<string | null>(null);
  readonly filters = [
    { id: 'active', name: '處理中' },
    { id: 'all', name: '全部' },
    { id: 'attention', name: '需要處理' },
    { id: 'completed', name: '已完成' },
  ];
  readonly filterOptions = this.filters.map((item) => ({ value: item.id, label: item.name }));
  readonly visible = computed(() =>
    this.jobs().filter(
      (x) =>
        this.filter() === 'all' ||
        (this.filter() === 'active'
          ? ['queued', 'running'].includes(x.status)
          : this.filter() === 'attention'
            ? ['failed', 'cancelled'].includes(x.status)
            : x.status === 'completed'),
    ),
  );
  private revision = 0;
  constructor() {
    inject(ActivatedRoute)
      .queryParamMap.pipe(takeUntilDestroyed())
      .subscribe((params) => {
        const id = params.get('job') || '';
        if (id === this.target()) return;
        this.target.set(id);
        if (id) this.filter.set('all');
        if (!this.loading()) void this.refresh();
      });
    void this.initialize();
  }
  private async initialize() {
    const valid = this.scope.guard();
    try {
      await this.session.load();
      if (!valid() || !this.session.me()) return;
      if (!this.session.has('tasks')) throw new ClientValidationError('featureAccess');
      await this.refresh();
    } catch (error) {
      if (valid()) this.error.set(this.scope.message(error));
    } finally {
      if (valid()) this.loading.set(false);
    }
  }
  async refresh() {
    const revision = ++this.revision,
      guard = this.scope.guard(),
      valid = () => guard() && revision === this.revision;
    try {
      const jobs = await this.api.list();
      if (valid()) {
        if (this.target() && !jobs.some((x) => x.id === this.target()))
          jobs.push(await this.api.get(this.target()));
        if (!valid()) return;
        this.jobs.set(jobs);
        this.error.set('');
      }
    } catch (error) {
      if (valid()) this.error.set(this.scope.message(error));
    }
    if (valid())
      this.scope.later(
        () => {
          if (!document.hidden) void this.refresh();
          else this.scope.later(() => void this.refresh(), 4000, 'poll');
        },
        3000,
        'poll',
      );
  }
  async act(job: JobDto, retry: boolean) {
    if (this.busy()) return;
    const valid = this.scope.guard();
    this.busy.set(job.id);
    this.error.set('');
    try {
      const value = await (retry ? this.api.retry(job.id) : this.api.cancel(job.id));
      if (valid()) this.jobs.update((items) => items.map((x) => (x.id === value.id ? value : x)));
    } catch (error) {
      if (valid()) this.error.set(this.scope.message(error));
    } finally {
      if (valid()) this.busy.set(null);
    }
  }
  kind(value: string) {
    return (
      (
        {
          'document-ingest': '文件辨識與索引',
          'document-embedding': '文件批次向量化',
          'embedding-reindex': '檢索索引重建',
          'retrieval-eval': '檢索品質評測',
          evaluation: '品質評測',
          'repository-review': '程式碼 Review',
          'integration-import': '資料來源匯入',
        } as Record<string, string>
      )[value] || '背景處理'
    );
  }
  readonly date = formatDate;
}
