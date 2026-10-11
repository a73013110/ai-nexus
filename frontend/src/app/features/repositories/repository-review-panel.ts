import { apiResource } from '../../core/api/api-resource';
import { Notice } from '../../shared/ui/notice';
import { Card } from '../../shared/ui/card';
import { Field } from '../../shared/ui/field';
import { ClientValidationError } from '../../core/errors/safe-errors';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
  untracked,
  linkedSignal,
} from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import type {
  ModelsDto,
  RepositoryCommitDto,
  RepositoryReviewDetailDto,
  RepositoryReviewDto,
} from '../../core/api/schema';
import { NexusApi } from '../../core/api/nexus-api';
import { WorkspaceSession } from '../../core/auth/workspace-session';
import { ViewScope } from '../../shared/browser/view-scope';
import { formatDate, formatModelDisplayName } from '../../shared/browser/format';
import { JobProgress } from '../tasks/job-progress';
import { MarkdownView } from '../../shared/markdown/markdown-view';
import { Select } from '../../shared/ui/select';
import { ResourceTarget } from '../../shared/ui/resource-target';
import { RepositoriesApi } from './repositories-api';
import { RepositoryCommitPicker } from './repository-commit-picker';
import { InferenceSignal } from '../../shared/ui/inference-signal';

@Component({
  selector: 'nx-repository-review',
  imports: [
    Notice,
    Card,
    Field,
    Select,
    JobProgress,
    MarkdownView,
    RouterLink,
    ResourceTarget,
    RepositoryCommitPicker,
    InferenceSignal,
  ],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './repository-review-panel.html',
  styleUrl: './repository-review-panel.scss',
})
export class RepositoryReviewPanel {
  readonly repository = input.required<string>();
  readonly head = input('');
  readonly reviewId = input('');
  /** A review the page already read (opened by link); a finished one is shown without reading it again. */
  readonly initialDetail = input<RepositoryReviewDetailDto | null>(null);
  /** The page's read was taken over, so coming back to this tab reads the review again. */
  readonly initialDetailConsumed = output<void>();
  private readonly api = inject(RepositoriesApi);
  private readonly nexus = inject(NexusApi);
  private readonly scope = inject(ViewScope);
  private readonly router = inject(Router);
  readonly session = inject(WorkspaceSession);
  private readonly modelsRead = apiResource({
    feature: 'repositories',
    loader: () => this.nexus.models(),
  });
  readonly models = computed<ModelsDto | null>(() => this.modelsRead.value() ?? null);
  private readonly commitsRead = apiResource({
    feature: 'repositories',
    params: () => this.repository(),
    loader: async (repository) => ({ repository, rows: await this.api.commits(repository) }),
  });
  readonly commits = computed<RepositoryCommitDto[]>(() => {
    const value = this.commitsRead.value();
    return value && value.repository === this.repository() ? value.rows : [];
  });
  private readonly historyRead = apiResource({
    feature: 'repositories',
    params: () => this.repository(),
    loader: async (repository) => ({ repository, rows: await this.api.reviews(repository) }),
  });
  readonly reviews = computed<RepositoryReviewDto[]>(() => {
    const value = this.historyRead.value();
    return value && value.repository === this.repository() ? value.rows : [];
  });
  /** The review on screen: the linked one until the user picks another from the history. */
  private readonly selectedId = linkedSignal(() => this.reviewId());
  /** The page's finished linked review, kept until the user picks a review or asks to read it again. */
  private readonly seeded = linkedSignal<
    RepositoryReviewDetailDto | null,
    RepositoryReviewDetailDto | null
  >({
    source: this.initialDetail,
    computation: (initial, previous) =>
      initial &&
      initial.review.id === this.reviewId() &&
      initial.review.repository === this.repository() &&
      !['queued', 'running'].includes(initial.review.job?.status || '')
        ? initial
        : (previous?.value ?? null),
  });
  /** A queued or running review is read again every 3 seconds. */
  private readonly detailRead = apiResource({
    feature: 'repositories',
    params: () =>
      this.selectedId() && this.selectedId() !== this.seeded()?.review.id
        ? { id: this.selectedId(), repository: this.repository() }
        : undefined,
    loader: async ({ id, repository }) => {
      const detail = await this.api.review(id);
      if (detail.review.repository !== repository)
        throw new ClientValidationError('reviewRepository');
      return detail;
    },
    poll: (detail) =>
      ['queued', 'running'].includes(detail?.review.job?.status || '') ? 3000 : null,
  });
  readonly detail = computed<RepositoryReviewDetailDto | null>(() => {
    const id = this.selectedId(),
      repository = this.repository(),
      matches = (value: RepositoryReviewDetailDto | null | undefined) =>
        value && value.review.id === id && value.review.repository === repository ? value : null;
    return (
      matches(this.detailRead.value()) ?? matches(this.seeded()) ?? matches(this.initialDetail())
    );
  });
  readonly detailLoading = computed(() => this.detailRead.loading() && !this.detail());
  readonly mode = signal('commit');
  readonly purpose = signal('review');
  /** Defaults follow the repository: its head (or newest) commit and the one before it. */
  readonly commit = linkedSignal<
    { repository: string; head: string; commits: RepositoryCommitDto[] },
    string
  >({
    source: () => ({ repository: this.repository(), head: this.head(), commits: this.commits() }),
    computation: (source, previous) =>
      previous?.value && previous.source.repository === source.repository
        ? previous.value
        : source.head || source.commits[0]?.sha || '',
  });
  readonly basis = linkedSignal<{ repository: string; commits: RepositoryCommitDto[] }, string>({
    source: () => ({ repository: this.repository(), commits: this.commits() }),
    computation: (source, previous) => {
      if (previous?.value && previous.source.repository === source.repository)
        return previous.value;
      const index = source.commits.findIndex((x) => x.sha === untracked(this.commit));
      return index >= 0 ? source.commits[index + 1]?.sha || '' : '';
    },
  });
  readonly model = linkedSignal(() => {
    this.repository();
    const models = this.models();
    return models?.policy.defaultModelId || models?.models[0]?.id || '';
  });
  readonly note = signal('');
  readonly commitsLoading = this.commitsRead.refreshing;
  readonly modelsLoading = this.modelsRead.refreshing;
  readonly historyLoading = this.historyRead.refreshing;
  readonly commitsError = this.commitsRead.error;
  readonly modelsError = this.modelsRead.error;
  readonly historyError = this.historyRead.error;
  readonly reportExpanded = signal(false);
  readonly evidenceExpanded = signal(false);
  readonly expandedSections = signal(new Set<number>());
  readonly busy = signal(false);
  readonly actionError = signal('');
  readonly error = computed(() => this.actionError() || this.detailRead.error());
  readonly date = formatDate;
  readonly modelName = formatModelDisplayName;
  readonly choices = computed(
    () => this.models()?.models.map((x) => ({ value: x.id, label: x.displayName })) ?? [],
  );
  readonly historyChoices = computed(() =>
    this.reviews().map((x) => ({
      value: x.id,
      label: x.commit.slice(0, 10) + ' · ' + this.date(x.createdAt) + ' · ' + x.job?.stage,
    })),
  );
  readonly longReport = computed(() => {
    const output = this.detail()?.report?.output ?? '';
    return output.length > 1200 || output.split('\n').length > 24;
  });
  readonly binarySections = computed(() => this.detail()?.sections.filter((x) => x.binary) ?? []);
  readonly active = computed(() =>
    ['queued', 'running'].includes(this.detail()?.review.job?.status || ''),
  );
  private requestKey = '';
  private requestSignature = '';
  constructor() {
    effect(() => {
      if (this.seeded()) untracked(() => this.initialDetailConsumed.emit());
    });
    // A review that just finished changes the history labels, so the history is read again.
    let wasActive = false;
    effect(() => {
      const active = this.active();
      if (wasActive && !active) untracked(() => this.historyRead.reload());
      wasActive = active;
    });
    // Expanded sections belong to the review they were opened in.
    effect(() => {
      this.selectedId();
      this.repository();
      untracked(() => {
        this.reportExpanded.set(false);
        this.evidenceExpanded.set(false);
        this.expandedSections.set(new Set());
      });
    });
  }
  loadModels() {
    this.modelsRead.reload();
  }
  loadCommits() {
    this.commitsRead.reload();
  }
  loadHistory() {
    this.historyRead.reload();
  }
  toggleSection(ordinal: number, open: boolean) {
    this.expandedSections.update((current) => {
      const next = new Set(current);
      if (open) next.add(ordinal);
      else next.delete(ordinal);
      return next;
    });
  }
  choose(id: string) {
    this.actionError.set('');
    // Dropping the page's copy starts a read of the same review by itself.
    const seeded = this.seeded();
    this.seeded.set(null);
    if (id !== this.selectedId()) this.selectedId.set(id);
    else if (!seeded) this.detailRead.reload();
  }
  async create(event: Event) {
    event.preventDefault();
    if (this.busy() || !this.model() || this.detailLoading()) return;
    const sha = /^[\da-f]{40}$|^[\da-f]{64}$/i;
    if (
      !sha.test(this.commit().trim()) ||
      (this.mode() === 'range' && !sha.test(this.basis().trim()))
    ) {
      this.actionError.set('請選擇或貼上完整的 40／64 位 commit SHA。');
      return;
    }
    if (this.mode() === 'range' && this.commit().toLowerCase() === this.basis().toLowerCase()) {
      this.actionError.set('起點與終點必須是不同的 commit。');
      return;
    }
    const request = {
      repository: this.repository(),
      commit: this.commit().trim(),
      baseCommit: this.mode() === 'range' ? this.basis().trim() : null,
      modelId: this.model(),
      note: this.note(),
      purpose: this.purpose(),
    };
    const signature = JSON.stringify(request);
    if (signature !== this.requestSignature) {
      this.requestSignature = signature;
      this.requestKey = crypto.randomUUID();
    }
    const valid = this.scope.guard(),
      repository = this.repository();
    this.busy.set(true);
    this.actionError.set('');
    try {
      const review = await this.api.createReview({ ...request, idempotencyKey: this.requestKey });
      if (!valid() || repository !== this.repository()) return;
      this.requestSignature = '';
      this.historyRead.value.update(
        (value) =>
          value && { ...value, rows: [review, ...value.rows.filter((x) => x.id !== review.id)] },
      );
      this.choose(review.id);
      void this.router.navigate(['/repositories'], {
        queryParams: { review: review.id },
        replaceUrl: true,
      });
    } catch (e) {
      if (valid() && repository === this.repository()) this.actionError.set(this.scope.message(e));
    } finally {
      if (valid()) this.busy.set(false);
    }
  }
  async changeJob(retry: boolean) {
    const id = this.detail()?.review.id,
      valid = this.scope.guard();
    if (!id || this.busy()) return;
    this.busy.set(true);
    this.actionError.set('');
    try {
      await this.api.reviewJob(id, retry);
      if (valid()) this.choose(id);
    } catch (e) {
      if (valid()) this.actionError.set(this.scope.message(e));
    } finally {
      if (valid()) this.busy.set(false);
    }
  }
}
