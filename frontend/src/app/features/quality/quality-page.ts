import { map } from 'rxjs';
import { apiResource } from '../../core/api/api-resource';
import { Notice } from '../../shared/ui/notice';
import { ToastService } from '../../shared/ui/toast-service';
import { EmptyState } from '../../shared/ui/empty-state';
import { Card } from '../../shared/ui/card';
import { CompactDialog } from '../../shared/ui/compact-dialog';
import { Field } from '../../shared/ui/field';
import { ApiError, ClientValidationError, issueInMessage } from '../../core/errors/safe-errors';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  inject,
  signal,
  viewChild,
  linkedSignal,
} from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { DecimalPipe } from '@angular/common';
import { WorkspaceSession } from '../../core/auth/workspace-session';
import { NexusApi } from '../../core/api/nexus-api';
import type {
  EvaluationCase,
  EvaluationDetailDto,
  EvaluationResultDto,
  EvaluationRunDto,
  EvaluationSetDto,
  FeedbackDto,
  ModelDto,
  ModelPolicyDto,
} from '../../core/api/schema';
import { ViewScope } from '../../shared/browser/view-scope';
import {
  formatDate,
  formatModelName,
  formatModelDisplayName,
  relativeTime,
} from '../../shared/browser/format';
import { downloadFile } from '../../shared/browser/download';
import { FeaturePage } from '../../core/layout/feature-page';
import { Select } from '../../shared/ui/select';
import { Icon } from '../../shared/ui/icon';
import { MarkdownView } from '../../shared/markdown/markdown-view';
import { JobProgress } from '../tasks/job-progress';
import { ResourceSharing } from '../sharing/resource-sharing';
import { JobsApi } from '../tasks/jobs-api';
import { JobToasts } from '../tasks/job-toasts';
import { QualityApi } from './quality-api';
import { ConfirmDialog } from '../../shared/ui/confirm-dialog';
import { ActionMenu, type MenuAction } from '../../shared/ui/action-menu';
import { Panel } from '../../shared/ui/panel';
import { ResourceCard } from '../../shared/ui/resource-card';
import { ResourceGallery } from '../../shared/ui/resource-gallery';
import { accessLabel } from '../sharing/access-label';

type VariantForm = { label: string; modelId: string | null; instruction: string };
const blankCase = (): EvaluationCase => ({
  question: '',
  reference: '',
  requiredTerms: [],
  forbiddenTerms: [],
});
@Component({
  selector: 'nx-quality-page',
  styleUrl: './quality-page.scss',
  imports: [
    Notice,
    EmptyState,
    Card,
    CompactDialog,
    Field,
    FeaturePage,
    ConfirmDialog,
    RouterLink,
    Icon,
    Select,
    MarkdownView,
    JobProgress,
    ResourceSharing,
    DecimalPipe,
    ActionMenu,
    Panel,
    ResourceCard,
    ResourceGallery,
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
  private readonly jobToasts = inject(JobToasts);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly scope = inject(ViewScope);
  readonly id = toSignal(this.route.paramMap.pipe(map((p) => p.get('id'))), {
    initialValue: null,
  });
  private readonly setsRead = apiResource({ feature: 'quality', loader: () => this.api.sets() });
  private readonly feedbackRead = apiResource({
    feature: 'quality',
    loader: () => this.api.feedbackList(),
  });
  private readonly currentRead = apiResource({
    feature: 'quality',
    params: () => this.id() || undefined,
    loader: (id) => this.api.get(id),
  });
  private readonly runsRead = apiResource({
    feature: 'quality',
    params: () => this.id() || undefined,
    loader: async (id) => ({ id, rows: await this.api.runs(id) }),
  });
  readonly sets = computed<EvaluationSetDto[]>(() => this.setsRead.value() ?? []);
  readonly confirm = viewChild.required(ConfirmDialog);
  readonly current = computed<EvaluationSetDto | null>(() => {
    const value = this.currentRead.value();
    return value && value.resource.id === this.id() ? value : null;
  });
  readonly runs = computed<EvaluationRunDto[]>(() => {
    const value = this.runsRead.value();
    return value && value.id === this.id() ? value.rows : [];
  });
  /** The run on screen: the linked one, then the newest, until the user picks another. */
  private readonly runId = linkedSignal<{ id: string | null; runs: EvaluationRunDto[] }, string>({
    source: () => ({ id: this.id(), runs: this.runs() }),
    computation: (source, previous) =>
      (previous?.value && previous.source.id === source.id ? previous.value : null) ??
      (previous ? null : this.route.snapshot.queryParamMap.get('run')) ??
      source.runs[0]?.id ??
      '',
  });
  /** A queued or running evaluation is read again every 2 seconds. */
  private readonly detailRead = apiResource({
    feature: 'quality',
    params: () => this.runId() || undefined,
    loader: (id) => this.api.detail(id),
    poll: (detail) => (['queued', 'running'].includes(detail?.run.job.status ?? '') ? 2000 : null),
  });
  readonly detail = computed<EvaluationDetailDto | null>(() => {
    const value = this.detailRead.value();
    return value && value.run.id === this.runId() ? value : null;
  });
  readonly feedback = computed<FeedbackDto[]>(() => this.feedbackRead.value() ?? []);
  readonly tab = signal('sets');
  readonly tabs = [
    { value: 'sets', label: '評測集' },
    { value: 'feedback', label: '我的回饋' },
  ];
  readonly setsLoading = this.setsRead.loading;
  readonly feedbackLoading = this.feedbackRead.loading;
  readonly currentLoading = this.currentRead.loading;
  readonly busy = signal(false);
  readonly actionError = signal('');
  readonly error = computed(
    () =>
      this.actionError() ||
      this.setsRead.error() ||
      this.feedbackRead.error() ||
      this.currentRead.error() ||
      this.runsRead.error() ||
      this.detailRead.error(),
  );
  readonly toast = inject(ToastService);
  readonly filter = signal('');
  readonly visible = computed(() =>
    this.sets().filter((x) => x.resource.name.toLowerCase().includes(this.filter().toLowerCase())),
  );
  readonly visibleFeedback = computed(() =>
    this.feedback().filter((x) =>
      x.conversationTitle.toLowerCase().includes(this.filter().toLowerCase()),
    ),
  );
  /** The open set's description with its size and version, under the page title. */
  readonly summary = computed(() => {
    const set = this.current();
    if (!set) return '';
    const facts = `${set.cases.length} 題 · v${set.version}`;
    return set.description ? `${set.description} · ${facts}` : facts;
  });
  readonly listActions: MenuAction[] = [
    { id: 'import', label: '匯入題庫', icon: 'upload' },
    { id: 'retrieval', label: '檢索評測', icon: 'chart' },
  ];
  readonly setActions = computed<MenuAction[]>(() => {
    const set = this.current();
    if (!set) return [];
    return [
      ...(set.resource.canEdit
        ? [{ id: 'edit', label: '編輯題庫', icon: 'edit', disabled: this.busy() }]
        : []),
      { id: 'export', label: '匯出題庫', icon: 'download' },
      ...(set.resource.isOwner
        ? [
            { id: 'access', label: '存取權限', icon: 'lock' },
            {
              id: 'delete',
              label: '刪除評測集',
              icon: 'trash',
              danger: true,
              disabled: this.busy(),
            },
          ]
        : []),
    ];
  });
  readonly importInput = viewChild<ElementRef<HTMLInputElement>>('importInput');
  readonly access = accessLabel;
  readonly when = relativeTime;
  readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  readonly sharing = viewChild(ResourceSharing);
  readonly form = signal('set');
  readonly name = signal('');
  readonly description = signal('');
  readonly cases = signal<EvaluationCase[]>([blankCase()]);
  readonly variants = signal<VariantForm[]>([]);
  readonly models = signal<ModelDto[]>([]);
  readonly policy = signal<ModelPolicyDto | null>(null);
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
  private reviewTarget = { c: 0, v: 0, run: '' };
  constructor() {
    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe(() => {
      ++this.revision;
      this.busy.set(false);
      this.actionError.set('');
    });
  }
  reload() {
    this.actionError.set('');
    this.setsRead.reload();
    this.feedbackRead.reload();
    this.currentRead.reload();
    this.runsRead.reload();
  }
  listAction(action: string) {
    if (action === 'import') this.importInput()?.nativeElement.click();
    else if (action === 'retrieval') void this.router.navigate(['/quality/retrieval']);
  }
  setAction(action: string) {
    const set = this.current();
    if (!set) return;
    if (action === 'edit') this.openSet(set);
    else if (action === 'export') this.exportSet();
    else if (action === 'access') void this.sharing()?.open();
    else if (action === 'delete') void this.removeSet();
  }
  private guard() {
    const generation = this.revision,
      alive = this.scope.guard();
    return () => alive() && generation === this.revision;
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
    this.actionError.set('');
    try {
      await this.api.remove(current.resource.id);
      if (valid()) await this.router.navigate(['/quality']);
    } catch (e) {
      if (valid()) this.actionError.set(this.scope.message(e));
    } finally {
      if (valid()) this.busy.set(false);
    }
  }
  selectRun(id: string) {
    if (id === this.runId()) this.detailRead.reload();
    else this.runId.set(id);
  }
  openSet(value: EvaluationSetDto | null = null) {
    if (this.busy()) return;
    this.editingId = value?.resource.id ?? null;
    this.form.set('set');
    this.name.set(value?.resource.name ?? '');
    this.description.set(value?.description ?? '');
    this.cases.set(value ? structuredClone(value.cases) : [blankCase()]);
    this.actionError.set('');
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
      if (valid()) this.actionError.set(this.scope.message(e));
    }
  }
  async openRun() {
    if (this.busy()) return;
    const valid = this.guard();
    this.busy.set(true);
    this.actionError.set('');
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
      if (valid()) this.actionError.set(this.scope.message(e));
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
    this.actionError.set('');
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
        this.currentRead.value.set(value);
        this.setsRead.value.update((x) => [
          value,
          ...(x ?? []).filter((y) => y.resource.id !== value.resource.id),
        ]);
        await this.router.navigate(['/quality', value.resource.id]);
        this.toast.success('題庫已儲存');
      } else if (this.form() === 'run' && current) {
        const run = await this.api.run(current.resource.id, this.variants());
        this.jobToasts.track(run.job);
        if (!valid()) return;
        this.dialog().nativeElement.close();
        this.runsRead.value.update((x) => x && { ...x, rows: [run, ...x.rows] });
        this.selectRun(run.id);
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
        this.selectRun(target.run);
        this.toast.success('人工評分已儲存');
      }
    } catch (e) {
      if (valid()) this.actionError.set(this.scope.message(e));
    } finally {
      if (valid()) this.busy.set(false);
    }
  }
  openReview(result: EvaluationResultDto) {
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
      if (retry) this.jobToasts.track(await this.jobs.retry(detail.run.job.id));
      else await this.jobs.cancel(detail.run.job.id);
      if (valid()) this.selectRun(detail.run.id);
    } catch (e) {
      if (valid()) this.actionError.set(this.scope.message(e));
    } finally {
      if (valid()) this.busy.set(false);
    }
  }
  readonly date = formatDate;
}
