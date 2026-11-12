import { apiResource } from '@core/api/api-resource';
import { Notice } from '@shared/ui/notice';
import { Field } from '@shared/ui/field';
import { ClientValidationError, safeMessage } from '@core/errors/safe-errors';
import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
  linkedSignal,
  computed,
} from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormField, form, maxLength, readonly, required } from '@angular/forms/signals';
import type {
  CollectionDto,
  RetrievalEvaluationCase,
  RetrievalEvaluationDto,
  RetrievalReportDto,
} from '@core/api/schema';
import { isActive } from '@core/api/generation-status';
import { QualityApi } from './quality-api';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { action } from '@shared/browser/action';
import { ViewScope } from '@shared/browser/view-scope';
import { formatDate } from '@shared/browser/format';
import { downloadFile } from '@shared/browser/download';
import { Checkbox } from '@shared/ui/checkbox';
import { PendingButton } from '@shared/ui/pending-button';
import { Select } from '@shared/ui/select';
import { JobProgress } from '@features/work/jobs/job-progress';
import { FeaturePage } from '@core/layout/feature-page';
import { Icon } from '@shared/ui/icon';
import { Panel } from '@shared/ui/panel';
import { KnowledgeApi } from '@features/work/knowledge/knowledge-api';
import { JobToasts } from '@features/work/jobs/job-toasts';
import { RETRIEVAL_TOUR } from './quality-tour';

@Component({
  selector: 'nx-retrieval-evaluation',
  imports: [
    Notice,
    Field,
    FormField,
    PendingButton,
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
  private readonly jobToasts = inject(JobToasts);
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
  /** 寫入與下載：`key` 是觸發的控制項（`start`、`cancel`、`retry`、`download`、`import`），只有它轉圈。 */
  readonly working = action();
  readonly busy = this.working.pending;
  readonly error = computed(
    () =>
      this.working.error() ||
      this.collectionsRead.error() ||
      this.runsRead.error() ||
      this.reportRead.error(),
  );
  readonly loading = computed(() => this.collectionsRead.loading() || this.runsRead.loading());
  readonly runsLoading = this.runsRead.loading;
  /** 讀取失敗後按重試：重試鈕轉圈。 */
  readonly refreshing = computed(
    () => this.collectionsRead.refreshing() || this.runsRead.refreshing(),
  );
  readonly date = formatDate;
  readonly active = isActive;
  readonly tour = RETRIEVAL_TOUR;
  private readonly draft = signal({ title: '', corpus: '' });
  readonly fields = form(this.draft, (schema) => {
    required(schema.title);
    maxLength(schema.title, 120);
    required(schema.corpus);
    maxLength(schema.corpus, 500000);
    // 排入中唯讀（不停用，焦點留在原處）。
    readonly(schema.title, () => this.working.pendingFor('start'));
    readonly(schema.corpus, () => this.working.pendingFor('start'));
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
    this.working.error.set('');
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
    this.working.error.set('');
    if (id === this.reportId()) this.reportRead.reload();
    else this.reportId.set(id);
  }
  async create(event: Event) {
    event.preventDefault();
    if (this.busy() || this.fields().invalid() || !this.selected().length) return;
    const { title, corpus } = this.draft();
    let cases: RetrievalEvaluationCase[];
    try {
      cases = JSON.parse(corpus) as RetrievalEvaluationCase[];
      if (!Array.isArray(cases) || cases.length < 1 || cases.length > 20)
        throw new ClientValidationError('retrievalEvaluationFormat');
    } catch (error) {
      this.working.fail(
        error instanceof SyntaxError
          ? 'JSON 格式不正確，請依範例修正後再執行。'
          : safeMessage(error),
      );
      return;
    }
    const outcome = await this.working.run(
      async () => {
        const run = await this.api.startRetrievalEvaluation({
          title: title.trim(),
          collectionIds: this.selected(),
          cases,
        });
        // 評測在背景進行：已離開頁面也照樣追蹤。
        this.jobToasts.track(run.job);
        return run;
      },
      { key: 'start' },
    );
    if (!outcome.ok) return;
    this.runsRead.value.update((runs) => [outcome.value, ...(runs ?? [])]);
    this.select(outcome.value.id);
  }
  async importCorpus(event: Event) {
    const input = event.target as HTMLInputElement,
      file = input.files?.[0];
    input.value = '';
    if (!file) return;
    if (file.size > 1000000) {
      this.working.fail('驗收集檔案最多 1 MB。');
      return;
    }
    const outcome = await this.working.run(() => file.text(), { key: 'import' });
    if (outcome.ok) this.draft.update((value) => ({ ...value, corpus: outcome.value }));
  }
  async control(kind: 'cancel' | 'retry') {
    const report = this.report();
    if (!report) return;
    const outcome = await this.working.run(
      () => this.jobToasts.control(report.run.job.id, kind === 'retry'),
      { key: kind },
    );
    if (outcome.ok) this.select(report.run.id);
  }
  async download() {
    const report = this.report();
    if (!report) return;
    const outcome = await this.working.run(() => this.api.retrievalReport(report.run.id), {
      key: 'download',
    });
    if (outcome.ok) downloadFile(JSON.stringify(outcome.value, null, 2), report.run.title, 'json');
  }
}
