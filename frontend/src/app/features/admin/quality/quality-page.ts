import { map } from 'rxjs';
import { apiResource } from '@core/api/api-resource';
import { Notice } from '@shared/ui/notice';
import { ToastService } from '@shared/ui/toast-service';
import { EmptyState } from '@shared/ui/empty-state';
import { Card } from '@shared/ui/card';
import { DialogHeader } from '@shared/ui/dialog-header';
import { CompactDialog } from '@shared/ui/compact-dialog';
import { Field } from '@shared/ui/field';
import { ApiError, ClientValidationError, issueInMessage } from '@core/errors/safe-errors';
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
import { WorkspaceSession } from '@core/auth/workspace-session';
import { WorkspaceApi } from '@core/api/workspace-api';
import type {
  EvaluationCase,
  EvaluationDetailDto,
  EvaluationResultDto,
  EvaluationRunDto,
  EvaluationSetDto,
  FeedbackDto,
  ModelDto,
  ModelPolicyDto,
} from '@core/api/schema';
import { ViewScope } from '@shared/browser/view-scope';
import {
  formatDate,
  formatModelName,
  formatModelDisplayName,
  relativeTime,
} from '@shared/browser/format';
import { downloadFile } from '@shared/browser/download';
import { FeaturePage } from '@core/layout/feature-page';
import { Select } from '@shared/ui/select';
import { Icon } from '@shared/ui/icon';
import { MarkdownView } from '@shared/markdown/markdown-view';
import { JobProgress } from '@features/work/jobs/job-progress';
import { ResourceSharing } from '@features/work/collaboration/resource-sharing';
import { JobsApi } from '@features/work/jobs/jobs-api';
import { JobToasts } from '@features/work/jobs/job-toasts';
import { QualityApi } from './quality-api';
import { ConfirmDialog } from '@shared/ui/confirm-dialog';
import { ActionMenu, type MenuAction } from '@shared/ui/action-menu';
import { Panel } from '@shared/ui/panel';
import { ResourceCard } from '@shared/ui/resource-card';
import { ResourceGallery } from '@shared/ui/resource-gallery';
import { accessLabel } from '@features/work/collaboration/access-label';
import { evaluationSetTour, qualityTour } from './quality-tour';
import { routeOf } from '@core/api/catalog';

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
    DialogHeader,
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
  /** 範本裡的連結由功能目錄取得網址。 */
  readonly routeOf = routeOf;
  readonly modelName = formatModelDisplayName;
  readonly session = inject(WorkspaceSession);
  private readonly api = inject(QualityApi);
  private readonly workspaceApi = inject(WorkspaceApi);
  private readonly jobs = inject(JobsApi);
  private readonly jobToasts = inject(JobToasts);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly scope = inject(ViewScope);
  readonly id = toSignal(this.route.paramMap.pipe(map((part) => part.get('id'))), {
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
  /** 畫面上的執行：先是連結的那個，然後是最新的，直到使用者選了別的。 */
  private readonly runId = linkedSignal<{ id: string | null; runs: EvaluationRunDto[] }, string>({
    source: () => ({ id: this.id(), runs: this.runs() }),
    computation: (source, previous) =>
      (previous?.value && previous.source.id === source.id ? previous.value : null) ??
      (previous ? null : this.route.snapshot.queryParamMap.get('run')) ??
      source.runs[0]?.id ??
      '',
  });
  /** 排隊中或執行中的評估每 2 秒重新讀取一次。 */
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
    this.sets().filter((item) =>
      item.resource.name.toLowerCase().includes(this.filter().toLowerCase()),
    ),
  );
  readonly visibleFeedback = computed(() =>
    this.feedback().filter((item2) =>
      item2.conversationTitle.toLowerCase().includes(this.filter().toLowerCase()),
    ),
  );
  /** 頁面標題下方，已開啟集合的描述與其大小、版本。 */
  readonly summary = computed(() => {
    const set = this.current();
    if (!set) return '';
    const facts = `${set.cases.length} 題 · v${set.version}`;
    return set.description ? `${set.description} · ${facts}` : facts;
  });
  /** 目前分頁的集合導覽，或已開啟集合的導覽（依此檢視者在該處可做的事決定）。 */
  readonly tour = computed(() => {
    const resource = this.current()?.resource;
    return this.id()
      ? evaluationSetTour({
          edit: !!resource?.canEdit,
          owner: !!resource?.isOwner,
          review: !!this.detail()?.canReview,
        })
      : qualityTour({ feedback: this.tab() === 'feedback', chat: this.session.has('chat') });
  });
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
    this.models().map((item) => ({ value: item.id, label: formatModelName(item) })),
  );
  readonly score = signal('');
  readonly reviewNote = signal('');
  readonly scores = [
    { value: '', label: '未評分' },
    ...[1, 2, 3, 4, 5].map((pointX) => ({ value: String(pointX), label: `${pointX} 分` })),
  ];
  readonly runOptions = computed(() =>
    this.runs().map((item3) => ({
      value: item3.id,
      label: `${this.date(item3.createdAt)} · 題庫 v${item3.setVersion}`,
      description: item3.job.stage,
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
      if (!valid()) return;
      // 頁面在 /quality 與 /quality/:id 之間保持掛載，所以列表在這裡移除它。
      this.setsRead.value.update((all) =>
        all?.filter((item) => item.resource.id !== current.resource.id),
      );
      this.setsRead.reload();
      await this.router.navigate([routeOf('quality')]);
    } catch (error2) {
      if (valid()) this.actionError.set(this.scope.message(error2));
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
      items.map((item, index2) =>
        index2 === index
          ? {
              ...item,
              [field]: field.endsWith('Terms')
                ? value
                    .split(/[,，\n]/)
                    .map((item) => item.trim())
                    .filter(Boolean)
                : value,
            }
          : item,
      ),
    );
  }
  addCase() {
    if (this.cases().length < 20) this.cases.update((items2) => [...items2, blankCase()]);
  }
  removeCase(index3: number) {
    if (this.cases().length > 1)
      this.cases.update((item) => item.filter((_, count) => count !== index3));
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
          (item4: EvaluationCase) =>
            !item4 ||
            typeof item4.question !== 'string' ||
            typeof item4.reference !== 'string' ||
            !Array.isArray(item4.requiredTerms) ||
            !Array.isArray(item4.forbiddenTerms) ||
            [...item4.requiredTerms, ...item4.forbiddenTerms].some(
              (target2) => typeof target2 !== 'string',
            ),
        )
      )
        throw new ClientValidationError('evaluationFormat');
      this.openSet();
      this.name.set(data.name);
      this.description.set(data.description);
      this.cases.set(data.cases);
    } catch (error3) {
      if (valid()) this.actionError.set(this.scope.message(error3));
    }
  }
  async openRun() {
    if (this.busy()) return;
    const valid = this.guard();
    this.busy.set(true);
    this.actionError.set('');
    try {
      const catalog = await this.workspaceApi.models();
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
    } catch (error4) {
      if (valid()) this.actionError.set(this.scope.message(error4));
    } finally {
      if (valid()) this.busy.set(false);
    }
  }
  variant(index4: number, field: keyof VariantForm, value: string) {
    this.variants.update((items3) =>
      items3.map((item, count2) => (index4 === count2 ? { ...item, [field]: value } : item)),
    );
  }
  addVariant() {
    if (this.variants().length < 3)
      this.variants.update((items4) => [
        ...items4,
        {
          label: `方案 ${String.fromCharCode(65 + items4.length)}`,
          modelId: this.policy()?.defaultModelId ?? this.models()[0]?.id ?? null,
          instruction: '',
        },
      ]);
  }
  removeVariant(index5: number) {
    this.variants.update((item) => item.filter((_, count3) => count3 !== index5));
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
        this.setsRead.value.update((item5) => [
          value,
          ...(item5 ?? []).filter((item6) => item6.resource.id !== value.resource.id),
        ]);
        await this.router.navigate([routeOf('quality'), value.resource.id]);
        this.toast.success('題庫已儲存');
      } else if (this.form() === 'run' && current) {
        const run = await this.api.run(current.resource.id, this.variants());
        this.jobToasts.track(run.job);
        if (!valid()) return;
        this.dialog().nativeElement.close();
        this.runsRead.value.update((item7) => item7 && { ...item7, rows: [run, ...item7.rows] });
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
    } catch (error5) {
      if (valid()) this.actionError.set(this.scope.message(error5));
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
  result(column: number, value2: number) {
    return this.detail()?.results.find(
      (item) => item.caseIndex === column && item.variantIndex === value2,
    );
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
    } catch (error6) {
      if (valid()) this.actionError.set(this.scope.message(error6));
    } finally {
      if (valid()) this.busy.set(false);
    }
  }
  readonly date = formatDate;
}
