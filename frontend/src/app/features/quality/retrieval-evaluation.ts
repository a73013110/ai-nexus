import { Notice } from '../../shared/ui/notice';
import { Card } from '../../shared/ui/card';
import { Field } from '../../shared/ui/field';
import { ClientValidationError } from '../../core/errors/safe-errors';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import type {
  CollectionDto,
  RetrievalEvaluationCase,
  RetrievalEvaluationDto,
  RetrievalReportDto,
} from '../../core/api/schema';
import { isActive } from '../../core/api/generation-status';
import { QualityApi } from './quality-api';
import { WorkspaceSession } from '../../core/auth/workspace-session';
import { ViewScope } from '../../shared/browser/view-scope';
import { formatDate } from '../../shared/browser/format';
import { downloadFile } from '../../shared/browser/download';
import { Checkbox } from '../../shared/ui/checkbox';
import { Select } from '../../shared/ui/select';
import { JobProgress } from '../tasks/job-progress';
import { KnowledgeApi } from '../knowledge/knowledge-api';
import { JobsApi } from '../tasks/jobs-api';

@Component({
  selector: 'nx-retrieval-evaluation',
  host: { class: 'platform-form' },
  imports: [Notice, Card, Field, ReactiveFormsModule, DecimalPipe, Checkbox, Select, JobProgress],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './retrieval-evaluation.html',
})
export class RetrievalEvaluation {
  private readonly api = inject(QualityApi);
  private readonly knowledge = inject(KnowledgeApi);
  private readonly jobs = inject(JobsApi);
  private readonly scope = inject(ViewScope);
  readonly session = inject(WorkspaceSession);
  readonly collections = signal<CollectionDto[]>([]);
  readonly selected = signal<string[]>([]);
  readonly runs = signal<RetrievalEvaluationDto[]>([]);
  readonly report = signal<RetrievalReportDto | null>(null);
  readonly error = signal('');
  readonly busy = signal(false);
  readonly loading = signal(true);
  readonly date = formatDate;
  private sequence = 0;
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
  constructor() {
    void this.load();
  }
  async load() {
    const valid = this.scope.guard();
    try {
      if (!this.session.has('knowledge')) throw new ClientValidationError('featureAccess');
      const [collections, runs] = await Promise.all([
        this.knowledge.collections(),
        this.api.retrievalEvaluations(),
      ]);
      if (!valid()) return;
      this.collections.set(collections);
      this.runs.set(runs);
      if (!this.report() && runs[0]) await this.select(runs[0].id);
    } catch (error) {
      if (valid()) this.error.set(this.scope.message(error));
    } finally {
      if (valid()) this.loading.set(false);
    }
  }
  options() {
    return this.runs().map((x) => ({
      value: x.id,
      label: `${x.title} · ${this.date(x.createdAt)}`,
      description: x.job.stage,
    }));
  }
  choose(id: string, checked: boolean) {
    this.selected.update((ids) => (checked ? [...ids, id] : ids.filter((x) => x !== id)));
  }
  async select(id: string) {
    const generation = ++this.sequence,
      alive = this.scope.guard();
    const valid = () => alive() && generation === this.sequence;
    this.scope.cancel('retrieval-eval');
    this.error.set('');
    try {
      const report = await this.api.retrievalEvaluation(id);
      if (!valid()) return;
      this.report.set(report);
      if (isActive(report.run.job.status))
        this.scope.later(() => void this.select(id), 2000, 'retrieval-eval');
    } catch (error) {
      if (valid()) {
        this.report.set(null);
        this.error.set(this.scope.message(error));
      }
    }
  }
  async create(event: Event) {
    event.preventDefault();
    if (this.busy() || this.form.invalid || !this.selected().length) return;
    const valid = this.scope.guard();
    this.error.set('');
    let cases: RetrievalEvaluationCase[];
    try {
      cases = JSON.parse(this.form.controls.corpus.value) as RetrievalEvaluationCase[];
      if (!Array.isArray(cases) || cases.length < 1 || cases.length > 20)
        throw new ClientValidationError('retrievalEvaluationFormat');
    } catch (error) {
      this.error.set(
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
      if (!valid()) return;
      this.runs.update((runs) => [run, ...runs]);
      await this.select(run.id);
    } catch (error) {
      if (valid()) this.error.set(this.scope.message(error));
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
      this.error.set('驗收集檔案最多 1 MB。');
      return;
    }
    try {
      const text = await file.text();
      if (valid()) {
        this.form.controls.corpus.setValue(text);
        this.error.set('');
      }
    } catch (error) {
      if (valid()) this.error.set(this.scope.message(error));
    }
  }
  async control(action: 'cancel' | 'retry') {
    const report = this.report(),
      valid = this.scope.guard();
    if (!report || this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    try {
      if (action === 'cancel') await this.jobs.cancel(report.run.job.id);
      else await this.jobs.retry(report.run.job.id);
      if (valid()) await this.select(report.run.id);
    } catch (error) {
      if (valid()) this.error.set(this.scope.message(error));
    } finally {
      if (valid()) this.busy.set(false);
    }
  }
  async download() {
    const report = this.report(),
      valid = this.scope.guard();
    if (!report || this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    try {
      const latest = await this.api.retrievalReport(report.run.id);
      if (valid()) downloadFile(JSON.stringify(latest, null, 2), report.run.title, 'json');
    } catch (error) {
      if (valid()) this.error.set(this.scope.message(error));
    } finally {
      if (valid()) this.busy.set(false);
    }
  }
}
