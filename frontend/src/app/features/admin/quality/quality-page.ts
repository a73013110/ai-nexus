import { map } from 'rxjs';
import { apiResource } from '@core/api/api-resource';
import { Notice } from '@shared/ui/notice';
import { PendingButton } from '@shared/ui/pending-button';
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
  ModelDto,
  ModelPolicyDto,
} from '@core/api/schema';
import { action } from '@shared/browser/action';
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
import { endpointTable, isPaged, textFilter } from '@shared/ui/table-model';
import { TablePagination } from '@shared/ui/data-table';
import { isActive } from '@core/api/generation-status';

type VariantForm = { label: string; modelId: string | null; instruction: string };
type EvaluationExport = { name: string; description: string; cases: EvaluationCase[] };
/** 題數與方案數上限與後端驗證一致；表單按鈕、計數與匯入檢查共用。 */
const MAX_CASES = 20;
const MAX_VARIANTS = 3;
const blankCase = (): EvaluationCase => ({
  question: '',
  reference: '',
  requiredTerms: [],
  forbiddenTerms: [],
});
/** 匯入檔只接受 version 1 匯出格式，逐欄檢查型別，不把任意 JSON 帶進表單。 */
function isEvaluationExport(data: unknown): data is EvaluationExport {
  const file = data as (EvaluationExport & { version?: unknown }) | null;
  return (
    !!file &&
    file.version === 1 &&
    typeof file.name === 'string' &&
    typeof file.description === 'string' &&
    Array.isArray(file.cases) &&
    file.cases.length >= 1 &&
    file.cases.length <= MAX_CASES &&
    file.cases.every(
      (item) =>
        !!item &&
        typeof item.question === 'string' &&
        typeof item.reference === 'string' &&
        Array.isArray(item.requiredTerms) &&
        Array.isArray(item.forbiddenTerms) &&
        [...item.requiredTerms, ...item.forbiddenTerms].every((term) => typeof term === 'string'),
    )
  );
}
@Component({
  selector: 'nx-quality-page',
  styleUrl: './quality-page.scss',
  imports: [
    TablePagination,
    DialogHeader,
    Notice,
    EmptyState,
    Card,
    CompactDialog,
    Field,
    FeaturePage,
    ConfirmDialog,
    PendingButton,
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
  private readonly jobToasts = inject(JobToasts);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  readonly id = toSignal(this.route.paramMap.pipe(map((part) => part.get('id'))), {
    initialValue: null,
  });
  private readonly setsRead = apiResource({ feature: 'quality', loader: () => this.api.sets() });
  /** 卡片格線一頁 40 張；資料在門檻內時搜尋與換頁都在本機。 */
  readonly feedback = endpointTable({
    feature: 'quality',
    params: () => true,
    load: (_, table, signal) => this.api.feedbackList(table, signal),
    columns: [
      { id: 'conversationTitle', label: '對話', filter: 'text' },
      { id: 'updatedAt', label: '時間', sort: 'desc', time: true },
    ],
    sort: { column: 'updatedAt', direction: 'desc' },
    pageSize: 40,
    pageSizes: [40],
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
    poll: (detail) => (detail && isActive(detail.run.job.status) ? 2000 : null),
  });
  readonly detail = computed<EvaluationDetailDto | null>(() => {
    const value = this.detailRead.value();
    return value && value.run.id === this.runId() ? value : null;
  });
  readonly tab = signal('sets');
  readonly tabs = [
    { value: 'sets', label: '評測集' },
    { value: 'feedback', label: '我的回饋' },
  ];
  readonly setsLoading = this.setsRead.loading;
  readonly feedbackLoading = this.feedback.loading;
  readonly feedbackPaged = computed(() => isPaged(this.feedback));
  readonly currentLoading = this.currentRead.loading;
  /** 比較紀錄清單第一次讀取（或換評測集）：面板顯示骨架，不先給空狀態。 */
  readonly runsLoading = this.runsRead.loading;
  /** 讀取失敗後按重試：重試鈕轉圈。 */
  readonly refreshing = computed(
    () =>
      this.setsRead.refreshing() ||
      this.feedback.refreshing() ||
      this.currentRead.refreshing() ||
      this.runsRead.refreshing(),
  );
  /** 寫入與準備：`key` 是觸發的控制項（`run`、`control`、`delete`、`save`、`import`），只有它轉圈，其他寫入由 `busy` 擋下。 */
  readonly working = action();
  readonly busy = this.working.pending;
  readonly error = computed(
    () =>
      this.working.error() ||
      this.setsRead.error() ||
      this.feedback.error() ||
      this.currentRead.error() ||
      this.runsRead.error() ||
      this.detailRead.error(),
  );
  private readonly toast = inject(ToastService);
  readonly filter = signal('');
  private readonly needle = computed(() => this.filter().toLowerCase());
  readonly visible = computed(() =>
    this.sets().filter((item) => item.resource.name.toLowerCase().includes(this.needle())),
  );
  readonly visibleFeedback = this.feedback.rows;
  search(text: string) {
    this.filter.set(text);
    this.feedback.setFilter('conversationTitle', textFilter(text));
  }
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
  readonly active = isActive;
  readonly maxCases = MAX_CASES;
  readonly maxVariants = MAX_VARIANTS;
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
    ...[1, 2, 3, 4, 5].map((point) => ({ value: String(point), label: `${point} 分` })),
  ];
  readonly runOptions = computed(() =>
    this.runs().map((item) => ({
      value: item.id,
      label: `${this.date(item.createdAt)} · 題庫 v${item.setVersion}`,
      description: item.job.stage,
    })),
  );
  private editingId: string | null = null;
  editing() {
    return this.editingId !== null;
  }
  private reviewTarget = { c: 0, v: 0, run: '' };
  constructor() {
    // 導覽不受 busy 鎖住：在 /quality 與 /quality/:id 之間切換時頁面元件被重用，
    // 進行中的寫入不能把結果套到另一個評測集，所以換路由就放棄它並解除 busy。
    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe(() => this.working.reset());
  }
  reload() {
    this.working.error.set('');
    this.setsRead.reload();
    this.feedback.reload();
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
  async removeSet() {
    const current = this.current();
    if (!current?.resource.isOwner || this.busy()) return;
    if (
      !(await this.confirm().ask({
        title: `刪除「${current.resource.name}」？`,
        message:
          '評測集將從工作區移除，授權成員無法再開啟。歷史結果與稽核紀錄會保留。建議先匯出需要保存的題庫。',
        confirm: '刪除評測集',
        danger: true,
      })) ||
      // 確認期間換到另一個評測集就不刪。
      this.current()?.resource.id !== current.resource.id
    )
      return;
    const outcome = await this.working.run(() => this.api.remove(current.resource.id), {
      key: 'delete',
    });
    if (!outcome.ok) return;
    // 頁面在 /quality 與 /quality/:id 之間保持掛載，所以列表在這裡移除它。
    this.setsRead.value.update((all) =>
      all?.filter((item) => item.resource.id !== current.resource.id),
    );
    this.setsRead.reload();
    await this.router.navigate([routeOf('quality')]);
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
    this.working.error.set('');
    this.dialog().nativeElement.showModal();
  }
  updateCase(
    index: number,
    field: 'question' | 'reference' | 'requiredTerms' | 'forbiddenTerms',
    value: string,
  ) {
    this.cases.update((items) =>
      items.map((item, position) =>
        position === index
          ? {
              ...item,
              [field]: field.endsWith('Terms')
                ? value
                    .split(/[,，\n]/)
                    .map((term) => term.trim())
                    .filter(Boolean)
                : value,
            }
          : item,
      ),
    );
  }
  addCase() {
    if (this.cases().length < MAX_CASES) this.cases.update((items) => [...items, blankCase()]);
  }
  removeCase(index: number) {
    if (this.cases().length > 1)
      this.cases.update((items) => items.filter((_, position) => position !== index));
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
    const outcome = await this.working.run(
      async () => {
        if (file.size > 1400000) throw new ClientValidationError('evaluationFileSize');
        let data: unknown;
        try {
          data = JSON.parse(await file.text());
        } catch {
          throw new ClientValidationError('evaluationFormat');
        }
        if (!isEvaluationExport(data)) throw new ClientValidationError('evaluationFormat');
        return data;
      },
      { key: 'import' },
    );
    if (!outcome.ok) return;
    this.openSet();
    this.name.set(outcome.value.name);
    this.description.set(outcome.value.description);
    this.cases.set(outcome.value.cases);
  }
  async openRun() {
    const outcome = await this.working.run(
      async () => {
        const catalog = await this.workspaceApi.models();
        if (!catalog.providerAvailable || !catalog.models.length)
          throw new ApiError(
            503,
            'model_unavailable',
            undefined,
            issueInMessage(catalog.notice) ?? undefined,
          );
        return catalog;
      },
      { key: 'run' },
    );
    if (!outcome.ok) return;
    this.models.set(outcome.value.models);
    this.policy.set(outcome.value.policy);
    this.variants.set([this.blankVariant(0)]);
    this.form.set('run');
    this.dialog().nativeElement.showModal();
  }
  /** 第 index 個方案的預設值：依序命名 A、B、C，模型用政策預設或清單第一個。 */
  private blankVariant(index: number): VariantForm {
    return {
      label: `方案 ${String.fromCharCode(65 + index)}`,
      modelId: this.policy()?.defaultModelId ?? this.models()[0]?.id ?? null,
      instruction: '',
    };
  }
  variant(index: number, field: keyof VariantForm, value: string) {
    this.variants.update((items) =>
      items.map((item, position) => (position === index ? { ...item, [field]: value } : item)),
    );
  }
  addVariant() {
    if (this.variants().length < MAX_VARIANTS)
      this.variants.update((items) => [...items, this.blankVariant(items.length)]);
  }
  removeVariant(index: number) {
    this.variants.update((items) => items.filter((_, position) => position !== index));
  }
  close(event?: Event) {
    event?.preventDefault();
    if (!this.busy()) this.dialog().nativeElement.close();
  }
  async save(event: Event) {
    event.preventDefault();
    const current = this.current();
    if (this.form() === 'set') await this.saveSet(current);
    else if (this.form() === 'run' && current) await this.startRun(current);
    else if (this.form() === 'review') await this.saveReview(this.reviewTarget);
  }
  private async saveSet(current: EvaluationSetDto | null) {
    const outcome = await this.working.run(
      () =>
        this.api.save(
          this.editingId,
          this.name(),
          this.description(),
          this.cases(),
          current?.version ?? 1,
        ),
      { key: 'save' },
    );
    if (!outcome.ok) return;
    const value = outcome.value;
    this.dialog().nativeElement.close();
    this.currentRead.value.set(value);
    this.setsRead.value.update((items) => [
      value,
      ...(items ?? []).filter((item) => item.resource.id !== value.resource.id),
    ]);
    await this.router.navigate([routeOf('quality'), value.resource.id]);
    this.toast.success('題庫已儲存');
  }
  private async startRun(current: EvaluationSetDto) {
    const outcome = await this.working.run(
      async () => {
        const run = await this.api.run(current.resource.id, this.variants());
        // 評測在背景進行：已離開頁面也照樣追蹤。
        this.jobToasts.track(run.job);
        return run;
      },
      { key: 'save' },
    );
    if (!outcome.ok) return;
    const run = outcome.value;
    this.dialog().nativeElement.close();
    this.runsRead.value.update((read) => read && { ...read, rows: [run, ...read.rows] });
    this.selectRun(run.id);
  }
  private async saveReview(target: { c: number; v: number; run: string }) {
    const outcome = await this.working.run(
      () =>
        this.api.review(
          target.run,
          target.c,
          target.v,
          this.score() ? Number(this.score()) : null,
          this.reviewNote(),
        ),
      { key: 'save' },
    );
    if (!outcome.ok) return;
    this.dialog().nativeElement.close();
    this.selectRun(target.run);
    this.toast.success('人工評分已儲存');
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
  result(caseIndex: number, variantIndex: number) {
    return this.detail()?.results.find(
      (item) => item.caseIndex === caseIndex && item.variantIndex === variantIndex,
    );
  }
  async control(retry: boolean) {
    const detail = this.detail();
    if (!detail?.run.canControl) return;
    const outcome = await this.working.run(() => this.jobToasts.control(detail.run.job.id, retry), {
      key: 'control',
    });
    if (outcome.ok) this.selectRun(detail.run.id);
  }
  readonly date = formatDate;
}
