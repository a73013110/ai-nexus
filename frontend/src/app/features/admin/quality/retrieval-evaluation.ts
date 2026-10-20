import { apiResource } from '@core/api/api-resource';
import { Notice } from '@shared/ui/notice';
import { Field } from '@shared/ui/field';
import { ClientValidationError } from '@core/errors/safe-errors';
import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
  linkedSignal,
  computed,
} from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import type {
  CollectionDto,
  RetrievalEvaluationCase,
  RetrievalEvaluationDto,
  RetrievalReportDto,
} from '@core/api/schema';
import { isActive } from '@core/api/generation-status';
import { QualityApi } from './quality-api';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { ViewScope } from '@shared/browser/view-scope';
import { formatDate } from '@shared/browser/format';
import { downloadFile } from '@shared/browser/download';
import { Checkbox } from '@shared/ui/checkbox';
import { Select } from '@shared/ui/select';
import { JobProgress } from '@features/work/jobs/job-progress';
import { FeaturePage } from '@core/layout/feature-page';
import { Icon } from '@shared/ui/icon';
import { Panel } from '@shared/ui/panel';
import { KnowledgeApi } from '@features/work/knowledge/knowledge-api';
import { JobsApi } from '@features/work/jobs/jobs-api';
import { JobToasts } from '@features/work/jobs/job-toasts';
import { RETRIEVAL_TOUR } from './quality-tour';

@Component({
  selector: 'nx-retrieval-evaluation',
  imports: [
    Notice,
    Field,
    ReactiveFormsModule,
    DecimalPipe,
    Checkbox,
    Select,
    JobProgress,
    FeaturePage,
    Icon,
    Panel,
  ],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './retrieval-evaluation.scss',
  templateUrl: './retrieval-evaluation.html',
})
export class RetrievalEvaluation {
  private readonly api = inject(QualityApi);
  private readonly knowledge = inject(KnowledgeApi);
  private readonly jobs = inject(JobsApi);
  private readonly jobToasts = inject(JobToasts);
  private readonly scope = inject(ViewScope);
  readonly session = inject(WorkspaceSession);
  private readonly collectionsRead = apiResource({
    feature: 'knowledge',
    loader: () => this.knowledge.collections(),
  });
  private readonly runsRead = apiResource({
    feature: 'quality.retrieval',
    loader: () => this.api.retrievalEvaluations(),
  });
  readonly collections = computed<CollectionDto[]>(() => this.collectionsRead.value() ?? []);
  readonly selected = signal<string[]>([]);
  readonly runs = computed<RetrievalEvaluationDto[]>(() => this.runsRead.value() ?? []);
  /** 畫面上的報告：最新的執行，直到使用者選了別的。 */
  private readonly reportId = linkedSignal<RetrievalEvaluationDto[], string>({
    source: this.runs,
    computation: (runs, previous) => previous?.value || runs[0]?.id || '',
  });
  /** 執行中的評估每 2 秒重新讀取一次。 */
  private readonly reportRead = apiResource({
    feature: 'quality.retrieval',
    params: () => this.reportId() || undefined,
    loader: (id) => this.api.retrievalEvaluation(id),
    poll: (report) => (report && isActive(report.run.job.status) ? 2000 : null),
  });
  readonly report = computed<RetrievalReportDto | null>(() => {
    const value = this.reportRead.value();
    return value?.run.id === this.reportId() ? value : null;
  });
  readonly actionError = signal('');
  readonly error = computed(
    () =>
      this.actionError() ||
      this.collectionsRead.error() ||
      this.runsRead.error() ||
      this.reportRead.error(),
  );
  readonly busy = signal(false);
  readonly loading = computed(() => this.collectionsRead.loading() || this.runsRead.loading());
  readonly date = formatDate;
  readonly active = isActive;
  readonly tour = RETRIEVAL_TOUR;
  readonly form = new FormGroup({
    title: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(120)],
    }),
    corpus: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(500000)],
    }),
  });
  readonly example = JSON.stringify(
    [
      {
        id: '題一',
        query: '採購如何核准？',
        relevant: [{ documentId: '文件 UUID', pages: [1], grade: 3 }],
      },
      { id: '無答案', query: '知識庫沒有記載的問題', relevant: [], noAnswer: true },
    ],
    null,
    2,
  );
  load() {
    this.actionError.set('');
    this.collectionsRead.reload();
    this.runsRead.reload();
  }
  readonly options = computed(() =>
    this.runs().map((item) => ({
      value: item.id,
      label: `${item.title} · ${this.date(item.createdAt)}`,
      description: item.job.stage,
    })),
  );
  choose(id: string, checked: boolean) {
    this.selected.update((ids) => (checked ? [...ids, id] : ids.filter((item) => item !== id)));
  }
  select(id: string) {
    this.actionError.set('');
    if (id === this.reportId()) this.reportRead.reload();
    else this.reportId.set(id);
  }
  async create(event: Event) {
    event.preventDefault();
    if (this.busy() || this.form.invalid || !this.selected().length) return;
    const valid = this.scope.guard();
    this.actionError.set('');
    let cases: RetrievalEvaluationCase[];
    try {
      cases = JSON.parse(this.form.controls.corpus.value) as RetrievalEvaluationCase[];
      if (!Array.isArray(cases) || cases.length < 1 || cases.length > 20)
        throw new ClientValidationError('retrievalEvaluationFormat');
    } catch (error) {
      this.actionError.set(
        error instanceof SyntaxError
          ? 'JSON 格式不正確，請依範例修正後再執行。'
          : this.scope.message(error),
      );
      return;
    }
    this.busy.set(true);
    try {
      const run = await this.api.startRetrievalEvaluation({
        title: this.form.controls.title.value.trim(),
        collectionIds: this.selected(),
        cases,
      });
      this.jobToasts.track(run.job);
      if (!valid()) return;
      this.runsRead.value.update((runs) => [run, ...(runs ?? [])]);
      this.select(run.id);
    } catch (error) {
      if (valid()) this.actionError.set(this.scope.message(error));
    } finally {
      if (valid()) this.busy.set(false);
    }
  }
  async importCorpus(event: Event) {
    const input = event.target as HTMLInputElement,
      file = input.files?.[0],
      valid = this.scope.guard();
    input.value = '';
    if (!file) return;
    if (file.size > 1000000) {
      this.actionError.set('驗收集檔案最多 1 MB。');
      return;
    }
    try {
      const text = await file.text();
      if (valid()) {
        this.form.controls.corpus.setValue(text);
        this.actionError.set('');
      }
    } catch (error) {
      if (valid()) this.actionError.set(this.scope.message(error));
    }
  }
  async control(action: 'cancel' | 'retry') {
    const report = this.report(),
      valid = this.scope.guard();
    if (!report || this.busy()) return;
    this.busy.set(true);
    this.actionError.set('');
    try {
      if (action === 'cancel') await this.jobs.cancel(report.run.job.id);
      else this.jobToasts.track(await this.jobs.retry(report.run.job.id));
      if (valid()) this.select(report.run.id);
    } catch (error) {
      if (valid()) this.actionError.set(this.scope.message(error));
    } finally {
      if (valid()) this.busy.set(false);
    }
  }
  async download() {
    const report = this.report(),
      valid = this.scope.guard();
    if (!report || this.busy()) return;
    this.busy.set(true);
    this.actionError.set('');
    try {
      const latest = await this.api.retrievalReport(report.run.id);
      if (valid()) downloadFile(JSON.stringify(latest, null, 2), report.run.title, 'json');
    } catch (error) {
      if (valid()) this.actionError.set(this.scope.message(error));
    } finally {
      if (valid()) this.busy.set(false);
    }
  }
}
