import { Notice } from '../../shared/ui/notice';
import { EmptyState } from '../../shared/ui/empty-state';
import { Card } from '../../shared/ui/card';
import { ViewSwitch } from '../../shared/ui/view-switch';
import { CompactDialog } from '../../shared/ui/compact-dialog';
import { Field } from '../../shared/ui/field';
import { ApiError, ClientValidationError, issueInMessage } from '../../core/api/safe-errors';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  inject,
  signal,
  viewChild,
  ViewEncapsulation,
} from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DecimalPipe } from '@angular/common';
import { WorkspaceSession } from '../../core/auth/workspace-session';
import { NexusApi } from '../../core/api/nexus-api';
import type {
  EvaluationCase,
  EvaluationSet,
  EvaluationRun,
  EvaluationDetail,
  EvaluationResult,
  Feedback,
  Model,
  ModelPolicy,
} from '../../core/api/types';
import { ViewScope } from '../../shared/browser/view-scope';
import { formatDate, formatModelName, formatModelDisplayName } from '../../shared/browser/format';
import { downloadFile } from '../../shared/browser/download';
import { FeaturePage } from '../../shared/ui/feature-page';
import { Select } from '../../shared/ui/select';
import { Icon } from '../../shared/ui/icon';
import { MarkdownView } from '../../shared/ui/markdown-view';
import { JobProgress } from '../../shared/ui/job-progress';
import { ResourceSharing } from '../../shared/ui/resource-sharing';
import { JobsApi } from '../tasks/jobs-api';
import { QualityApi } from './quality-api';
import { SearchField } from '../../shared/ui/search-field';
import { ConfirmDialog } from '../../shared/ui/confirm-dialog';
import { RetrievalEvaluation } from './retrieval-evaluation';

type VariantForm = { label: string; modelId: string | null; instruction: string };
const blankCase = (): EvaluationCase => ({
  question: '',
  reference: '',
  requiredTerms: [],
  forbiddenTerms: [],
});
@Component({
  selector: 'nx-quality-page',
  // Page-only rules load with this lazy route instead of the initial stylesheet.
  encapsulation: ViewEncapsulation.None,
  styleUrl: '../../../styles/quality.scss',
  imports: [
    Notice,
    EmptyState,
    Card,
    ViewSwitch,
    CompactDialog,
    Field,
    FeaturePage,
    SearchField,
    ConfirmDialog,
    RouterLink,
    Icon,
    Select,
    MarkdownView,
    JobProgress,
    ResourceSharing,
    DecimalPipe,
    RetrievalEvaluation,
  ],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './quality-page.html',
})
export class QualityPage {
  readonly modelName = formatModelDisplayName;
  readonly session = inject(WorkspaceSession);
  private readonly api = inject(QualityApi);
  private readonly nexus = inject(NexusApi);
  private readonly jobs = inject(JobsApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly scope = inject(ViewScope);
  readonly sets = signal<EvaluationSet[]>([]);
  readonly confirm = viewChild.required(ConfirmDialog);
  readonly current = signal<EvaluationSet | null>(null);
  readonly runs = signal<EvaluationRun[]>([]);
  readonly detail = signal<EvaluationDetail | null>(null);
  readonly feedback = signal<Feedback[]>([]);
  readonly tab = signal('sets');
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly notice = signal('');
  readonly filter = signal('');
  readonly visible = computed(() =>
    this.sets().filter((x) => x.resource.name.toLowerCase().includes(this.filter().toLowerCase())),
  );
  readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  readonly sharing = viewChild(ResourceSharing);
  readonly form = signal('set');
  readonly name = signal('');
  readonly description = signal('');
  readonly cases = signal<EvaluationCase[]>([blankCase()]);
  readonly variants = signal<VariantForm[]>([]);
  readonly models = signal<Model[]>([]);
  readonly policy = signal<ModelPolicy | null>(null);
  readonly modelOptions = computed(() =>
    this.models().map((x) => ({ value: x.id, label: formatModelName(x) })),
  );
  readonly score = signal('');
  readonly reviewNote = signal('');
  readonly scores = [
    { value: '', label: '未評分' },
    ...[1, 2, 3, 4, 5].map((x) => ({ value: String(x), label: `${x} 分` })),
  ];
  readonly runOptions = computed(() =>
    this.runs().map((x) => ({
      value: x.id,
      label: `${this.date(x.createdAt)} · 題庫 v${x.setVersion}`,
      description: x.job.stage,
    })),
  );
  private editingId: string | null = null;
  editing() {
    return this.editingId !== null;
  }
  private revision = 0;
  private runSequence = 0;
  private reviewTarget = { c: 0, v: 0, run: '' };
  constructor() {
    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe((p) => void this.load(p.get('id')));
  }
  private guard() {
    const generation = this.revision,
      alive = this.scope.guard();
    return () => alive() && generation === this.revision;
  }
  async load(id: string | null) {
    ++this.revision;
    ++this.runSequence;
    this.busy.set(false);
    this.loading.set(true);
    this.current.set(null);
    this.detail.set(null);
    this.error.set('');
    const valid = this.guard();
    try {
      await this.session.load();
      if (!valid() || !this.session.me()) return;
      if (!this.session.has('quality')) throw new ClientValidationError('featureAccess');
      const [sets, feedback] = await Promise.all([this.api.sets(), this.api.feedbackList()]);
      if (!valid()) return;
      this.sets.set(sets);
      this.feedback.set(feedback);
      if (id) {
        const current = await this.api.get(id);
        if (!valid()) return;
        this.current.set(current);
        await this.refreshRuns();
      }
    } catch (e) {
      if (valid()) this.error.set(this.scope.message(e));
    } finally {
      if (valid()) this.loading.set(false);
    }
  }
  async refreshRuns() {
    const id = this.current()?.resource.id,
      valid = this.guard();
    if (!id) return;
    try {
      const runs = await this.api.runs(id);
      if (!valid()) return;
      this.runs.set(runs);
      const selected =
        this.detail()?.run.id ?? this.route.snapshot.queryParamMap.get('run') ?? runs[0]?.id;
      if (selected) await this.selectRun(selected);
    } catch (e) {
      if (valid()) this.error.set(this.scope.message(e));
    }
  }
  async removeSet() {
    const current = this.current(),
      valid = this.guard();
    if (!current?.resource.isOwner || this.busy()) return;
    if (
      !(await this.confirm().ask({
        title: `刪除「${current.resource.name}」？`,
        message:
          '評測集將從工作區移除，授權成員無法再開啟。歷史結果與稽核紀錄會保留。建議先匯出需要保存的題庫。',
        confirm: '刪除評測集',
        danger: true,
      })) ||
      !valid()
    )
      return;
    this.busy.set(true);
    this.error.set('');
    try {
      await this.api.remove(current.resource.id);
      if (valid()) await this.router.navigate(['/quality']);
    } catch (e) {
      if (valid()) this.error.set(this.scope.message(e));
    } finally {
      if (valid()) this.busy.set(false);
    }
  }
  async selectRun(id: string) {
    const sequence = ++this.runSequence,
      alive = this.guard();
    const valid = () => alive() && sequence === this.runSequence;
    try {
      const detail = await this.api.detail(id);
      if (!valid()) return;
      this.detail.set(detail);
      if (['queued', 'running'].includes(detail.run.job.status))
        this.scope.later(
          () => {
            if (valid()) void this.selectRun(id);
          },
          2000,
          'evaluation-poll',
        );
    } catch (e) {
      if (valid()) {
        this.detail.set(null);
        this.error.set(this.scope.message(e));
      }
    }
  }
  openSet(value: EvaluationSet | null = null) {
    if (this.busy()) return;
    this.editingId = value?.resource.id ?? null;
    this.form.set('set');
    this.name.set(value?.resource.name ?? '');
    this.description.set(value?.description ?? '');
    this.cases.set(value ? structuredClone(value.cases) : [blankCase()]);
    this.error.set('');
    this.dialog().nativeElement.showModal();
  }
  updateCase(
    index: number,
    field: 'question' | 'reference' | 'requiredTerms' | 'forbiddenTerms',
    value: string,
  ) {
    this.cases.update((items) =>
      items.map((item, i) =>
        i === index
          ? {
              ...item,
              [field]: field.endsWith('Terms')
                ? value
                    .split(/[,，\n]/)
                    .map((x) => x.trim())
                    .filter(Boolean)
                : value,
            }
          : item,
      ),
    );
  }
  addCase() {
    if (this.cases().length < 20) this.cases.update((x) => [...x, blankCase()]);
  }
  removeCase(i: number) {
    if (this.cases().length > 1) this.cases.update((x) => x.filter((_, n) => n !== i));
  }
  exportSet() {
    const value = this.current();
    if (value)
      downloadFile(
        JSON.stringify(
          {
            version: 1,
            name: value.resource.name,
            description: value.description,
            cases: value.cases,
          },
          null,
          2,
        ),
        value.resource.name,
        'json',
      );
  }
  async importSet(event: Event) {
    const input = event.target as HTMLInputElement,
      file = input.files?.[0];
    input.value = '';
    if (!file) return;
    const valid = this.guard();
    try {
      if (file.size > 1400000) throw new ClientValidationError('evaluationFileSize');
      let data;
      try {
        data = JSON.parse(await file.text());
      } catch {
        throw new ClientValidationError('evaluationFormat');
      }
      if (!valid()) return;
      if (
        !data ||
        data.version !== 1 ||
        typeof data.name !== 'string' ||
        typeof data.description !== 'string' ||
        !Array.isArray(data.cases) ||
        data.cases.length < 1 ||
        data.cases.length > 20 ||
        data.cases.some(
          (x: EvaluationCase) =>
            !x ||
            typeof x.question !== 'string' ||
            typeof x.reference !== 'string' ||
            !Array.isArray(x.requiredTerms) ||
            !Array.isArray(x.forbiddenTerms) ||
            [...x.requiredTerms, ...x.forbiddenTerms].some((t) => typeof t !== 'string'),
        )
      )
        throw new ClientValidationError('evaluationFormat');
      this.openSet();
      this.name.set(data.name);
      this.description.set(data.description);
      this.cases.set(data.cases);
    } catch (e) {
      if (valid()) this.error.set(this.scope.message(e));
    }
  }
  async openRun() {
    if (this.busy()) return;
    const valid = this.guard();
    this.busy.set(true);
    this.error.set('');
    try {
      const catalog = await this.nexus.models();
      if (!valid()) return;
      if (!catalog.providerAvailable || !catalog.models.length)
        throw new ApiError(
          503,
          'model_unavailable',
          undefined,
          issueInMessage(catalog.notice) ?? undefined,
        );
      this.models.set(catalog.models);
      this.policy.set(catalog.policy);
      this.variants.set([
        {
          label: '方案 A',
          modelId: catalog.policy.defaultModelId ?? catalog.models[0].id,
          instruction: '',
        },
      ]);
      this.form.set('run');
      this.dialog().nativeElement.showModal();
    } catch (e) {
      if (valid()) this.error.set(this.scope.message(e));
    } finally {
      if (valid()) this.busy.set(false);
    }
  }
  variant(i: number, field: keyof VariantForm, value: string) {
    this.variants.update((x) => x.map((item, n) => (i === n ? { ...item, [field]: value } : item)));
  }
  addVariant() {
    if (this.variants().length < 3)
      this.variants.update((x) => [
        ...x,
        {
          label: `方案 ${String.fromCharCode(65 + x.length)}`,
          modelId: this.policy()?.defaultModelId ?? this.models()[0]?.id ?? null,
          instruction: '',
        },
      ]);
  }
  removeVariant(i: number) {
    this.variants.update((x) => x.filter((_, n) => n !== i));
  }
  close(event?: Event) {
    event?.preventDefault();
    if (!this.busy()) this.dialog().nativeElement.close();
  }
  async save(event: Event) {
    event.preventDefault();
    if (this.busy()) return;
    const valid = this.guard(),
      current = this.current();
    this.busy.set(true);
    this.error.set('');
    try {
      if (this.form() === 'set') {
        const value = await this.api.save(
          this.editingId,
          this.name(),
          this.description(),
          this.cases(),
          current?.version ?? 1,
        );
        if (!valid()) return;
        this.dialog().nativeElement.close();
        this.current.set(value);
        this.sets.update((x) => [value, ...x.filter((y) => y.resource.id !== value.resource.id)]);
        await this.router.navigate(['/quality', value.resource.id]);
        this.notice.set('題庫已儲存。');
      } else if (this.form() === 'run' && current) {
        const run = await this.api.run(current.resource.id, this.variants());
        if (!valid()) return;
        this.dialog().nativeElement.close();
        this.runs.update((x) => [run, ...x]);
        await this.selectRun(run.id);
      } else if (this.form() === 'review') {
        const target = this.reviewTarget;
        await this.api.review(
          target.run,
          target.c,
          target.v,
          this.score() ? Number(this.score()) : null,
          this.reviewNote(),
        );
        if (!valid()) return;
        this.dialog().nativeElement.close();
        await this.selectRun(target.run);
        this.notice.set('人工評分已儲存。');
      }
    } catch (e) {
      if (valid()) this.error.set(this.scope.message(e));
    } finally {
      if (valid()) this.busy.set(false);
    }
  }
  openReview(result: EvaluationResult) {
    const detail = this.detail();
    if (!detail?.canReview || this.busy()) return;
    this.reviewTarget = { c: result.caseIndex, v: result.variantIndex, run: detail.run.id };
    this.score.set(result.reviewScore ? String(result.reviewScore) : '');
    this.reviewNote.set(result.reviewNote);
    this.form.set('review');
    this.dialog().nativeElement.showModal();
  }
  result(c: number, v: number) {
    return this.detail()?.results.find((x) => x.caseIndex === c && x.variantIndex === v);
  }
  async control(retry: boolean) {
    const detail = this.detail(),
      valid = this.guard();
    if (!detail?.run.canControl || this.busy()) return;
    this.busy.set(true);
    try {
      await (retry ? this.jobs.retry(detail.run.job.id) : this.jobs.cancel(detail.run.job.id));
      if (valid()) await this.selectRun(detail.run.id);
    } catch (e) {
      if (valid()) this.error.set(this.scope.message(e));
    } finally {
      if (valid()) this.busy.set(false);
    }
  }
  readonly date = formatDate;
}
