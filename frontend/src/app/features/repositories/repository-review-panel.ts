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
  type WritableSignal,
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
  readonly initialDetail = input<RepositoryReviewDetailDto | null>(null);
  readonly initialDetailConsumed = output<void>();
  private readonly api = inject(RepositoriesApi);
  private readonly nexus = inject(NexusApi);
  private readonly scope = inject(ViewScope);
  private readonly router = inject(Router);
  readonly session = inject(WorkspaceSession);
  readonly models = signal<ModelsDto | null>(null);
  readonly commits = signal<RepositoryCommitDto[]>([]);
  readonly reviews = signal<RepositoryReviewDto[]>([]);
  readonly detail = signal<RepositoryReviewDetailDto | null>(null);
  readonly mode = signal('commit');
  readonly purpose = signal('review');
  readonly commit = signal('');
  readonly basis = signal('');
  readonly model = signal('');
  readonly note = signal('');
  readonly commitsLoading = signal(true);
  readonly modelsLoading = signal(true);
  readonly historyLoading = signal(true);
  readonly commitsError = signal('');
  readonly modelsError = signal('');
  readonly historyError = signal('');
  readonly detailLoading = signal(false);
  readonly reportExpanded = signal(false);
  readonly evidenceExpanded = signal(false);
  readonly expandedSections = signal(new Set<number>());
  readonly busy = signal(false);
  readonly error = signal('');
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
  private sequence = 0;
  private readSequence = 0;
  private requestKey = '';
  private requestSignature = '';
  constructor() {
    effect(() => {
      const repository = this.repository();
      untracked(() => void this.load(repository));
    });
    effect(() => {
      const id = this.reviewId(),
        initial = this.initialDetail(),
        repository = this.repository();
      untracked(() => {
        if (!id || this.detail()?.review.id === id) return;
        if (initial?.review.id === id && initial.review.repository === repository) {
          ++this.readSequence;
          this.resetResult();
          this.show(initial);
          this.initialDetailConsumed.emit();
        } else void this.choose(id);
      });
    });
  }
  async load(repository: string) {
    ++this.sequence;
    ++this.readSequence;
    this.resetResult();
    this.reviews.set([]);
    this.commits.set([]);
    this.models.set(null);
    this.model.set('');
    this.commit.set(this.head());
    this.basis.set('');
    this.error.set('');
    await Promise.allSettled([
      this.loadModels(),
      this.loadCommits(repository),
      this.loadHistory(repository),
    ]);
  }
  private async loadResource<T>(
    request: Promise<T>,
    apply: (value: T) => void,
    loading: WritableSignal<boolean>,
    error: WritableSignal<string>,
  ) {
    const sequence = this.sequence,
      valid = this.scope.guard();
    const current = () => valid() && sequence === this.sequence;
    loading.set(true);
    error.set('');
    try {
      const value = await request;
      if (current()) apply(value);
    } catch (e) {
      if (current()) error.set(this.scope.message(e));
    } finally {
      if (current()) loading.set(false);
    }
  }
  loadModels() {
    return this.loadResource(
      this.nexus.models(),
      (models) => {
        this.models.set(models);
        this.model.set(models.policy.defaultModelId || models.models[0]?.id || '');
      },
      this.modelsLoading,
      this.modelsError,
    );
  }
  loadCommits(repository = this.repository()) {
    return this.loadResource(
      this.api.commits(repository),
      (commits) => {
        this.commits.set(commits);
        if (!this.commit()) this.commit.set(commits[0]?.sha || '');
        const index = commits.findIndex((x) => x.sha === this.commit());
        if (!this.basis() && index >= 0) this.basis.set(commits[index + 1]?.sha || '');
      },
      this.commitsLoading,
      this.commitsError,
    );
  }
  loadHistory(repository = this.repository()) {
    return this.loadResource(
      this.api.reviews(repository),
      (reviews) => {
        this.reviews.update((current) => [
          ...current,
          ...reviews.filter((row) => !current.some((x) => x.id === row.id)),
        ]);
      },
      this.historyLoading,
      this.historyError,
    );
  }
  private show(detail: RepositoryReviewDetailDto) {
    this.detail.set(detail);
    if (['queued', 'running'].includes(detail.review.job!.status))
      this.scope.later(() => void this.read(detail.review.id, true), 3000, 'review');
  }
  private resetResult() {
    this.scope.cancel('review');
    this.detail.set(null);
    this.detailLoading.set(false);
    this.reportExpanded.set(false);
    this.evidenceExpanded.set(false);
    this.expandedSections.set(new Set());
  }
  toggleSection(ordinal: number, open: boolean) {
    this.expandedSections.update((current) => {
      const next = new Set(current);
      if (open) next.add(ordinal);
      else next.delete(ordinal);
      return next;
    });
  }
  async read(id: string, poll = false) {
    const sequence = ++this.readSequence,
      repository = this.repository(),
      valid = this.scope.guard();
    if (!poll) {
      this.resetResult();
      this.detailLoading.set(true);
    }
    try {
      const detail = await this.api.review(id);
      if (!valid() || sequence !== this.readSequence || repository !== this.repository()) return;
      if (detail.review.repository !== repository)
        throw new ClientValidationError('reviewRepository');
      this.show(detail);
      if (poll && !this.active()) {
        const reviews = await this.api.reviews(repository);
        if (valid() && sequence === this.readSequence && repository === this.repository())
          this.reviews.set(reviews);
      }
    } catch (e) {
      if (valid() && sequence === this.readSequence) {
        this.detail.set(null);
        this.error.set(this.scope.message(e));
      }
    } finally {
      if (valid() && sequence === this.readSequence) this.detailLoading.set(false);
    }
  }
  async choose(id: string) {
    this.error.set('');
    await this.read(id);
  }
  async create(event: Event) {
    event.preventDefault();
    if (this.busy() || !this.model() || this.detailLoading()) return;
    const sha = /^[\da-f]{40}$|^[\da-f]{64}$/i;
    if (
      !sha.test(this.commit().trim()) ||
      (this.mode() === 'range' && !sha.test(this.basis().trim()))
    ) {
      this.error.set('請選擇或貼上完整的 40／64 位 commit SHA。');
      return;
    }
    if (this.mode() === 'range' && this.commit().toLowerCase() === this.basis().toLowerCase()) {
      this.error.set('起點與終點必須是不同的 commit。');
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
    this.error.set('');
    try {
      const review = await this.api.createReview({ ...request, idempotencyKey: this.requestKey });
      if (!valid() || repository !== this.repository()) return;
      this.requestSignature = '';
      this.reviews.update((rows) => [review, ...rows.filter((x) => x.id !== review.id)]);
      await this.read(review.id);
      void this.router.navigate(['/repositories'], {
        queryParams: { review: review.id },
        replaceUrl: true,
      });
    } catch (e) {
      if (valid() && repository === this.repository()) this.error.set(this.scope.message(e));
    } finally {
      if (valid()) this.busy.set(false);
    }
  }
  async changeJob(retry: boolean) {
    const id = this.detail()?.review.id,
      valid = this.scope.guard();
    if (!id || this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    try {
      await this.api.reviewJob(id, retry);
      if (valid()) await this.read(id);
    } catch (e) {
      if (valid()) this.error.set(this.scope.message(e));
    } finally {
      if (valid()) this.busy.set(false);
    }
  }
}
