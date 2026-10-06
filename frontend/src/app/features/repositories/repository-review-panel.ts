import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
  untracked,
} from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import type {
  Models,
  RepositoryCommit,
  RepositoryReview,
  RepositoryReviewDetail,
} from '../../core/api/types';
import { NexusApi } from '../../core/api/nexus-api';
import { WorkspaceSession } from '../../core/auth/workspace-session';
import { ViewScope } from '../../shared/browser/view-scope';
import { formatDate, formatModelId } from '../../shared/browser/format';
import { JobProgress } from '../../shared/ui/job-progress';
import { MarkdownView } from '../../shared/ui/markdown-view';
import { Select } from '../../shared/ui/select';
import { ResourceTarget } from '../../shared/ui/resource-target';
import { RepositoriesApi } from './repositories-api';

@Component({
  selector: 'nx-repository-review',
  imports: [Select, JobProgress, MarkdownView, RouterLink, ResourceTarget],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './repository-review-panel.html',
  styleUrl: './repository-review-panel.scss',
})
export class RepositoryReviewPanel {
  readonly repository = input.required<string>();
  readonly head = input('');
  readonly reviewId = input('');
  private readonly api = inject(RepositoriesApi);
  private readonly nexus = inject(NexusApi);
  private readonly scope = inject(ViewScope);
  private readonly router = inject(Router);
  readonly session = inject(WorkspaceSession);
  readonly models = signal<Models | null>(null);
  readonly commits = signal<RepositoryCommit[]>([]);
  readonly reviews = signal<RepositoryReview[]>([]);
  readonly detail = signal<RepositoryReviewDetail | null>(null);
  readonly mode = signal('commit');
  readonly commit = signal('');
  readonly basis = signal('');
  readonly model = signal('');
  readonly note = signal('');
  readonly loading = signal(false);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly date = formatDate;
  readonly modelName = formatModelId;
  readonly choices = computed(
    () => this.models()?.models.map((x) => ({ value: x.id, label: x.displayName })) ?? [],
  );
  readonly historyChoices = computed(() =>
    this.reviews().map((x) => ({
      value: x.id,
      label: x.commit.slice(0, 10) + ' · ' + this.date(x.createdAt) + ' · ' + x.job?.stage,
    })),
  );
  readonly commitChoices = computed(() =>
    this.commits().map((x) => ({
      value: x.sha,
      label: x.sha.slice(0, 10) + ' · ' + x.message.split('\n')[0],
    })),
  );
  readonly active = computed(() =>
    ['queued', 'running'].includes(this.detail()?.review.job?.status || ''),
  );
  private sequence = 0;
  private readSequence = 0;
  private requestKey = '';
  private requestSignature = '';
  constructor() {
    effect(() => {
      const repository = this.repository(),
        id = this.reviewId();
      untracked(() => void this.load(repository, id));
    });
  }
  async load(repository: string, id: string) {
    const sequence = ++this.sequence,
      valid = this.scope.guard();
    ++this.readSequence;
    this.scope.cancel('review');
    this.detail.set(null);
    this.reviews.set([]);
    this.commits.set([]);
    this.loading.set(true);
    this.error.set('');
    try {
      const [models, commits, reviews] = await Promise.all([
        this.nexus.models(),
        this.api.commits(repository),
        this.api.reviews(repository),
      ]);
      if (!valid() || sequence !== this.sequence) return;
      this.models.set(models);
      this.commits.set(commits);
      this.reviews.set(reviews);
      this.commit.set(this.head() || commits[0]?.sha || '');
      this.model.set(models.policy.defaultModelId || models.models[0]?.id || '');
      if (id) await this.read(id);
      else if (reviews[0]) await this.read(reviews[0].id);
    } catch (e) {
      if (valid() && sequence === this.sequence) this.error.set(this.scope.message(e));
    } finally {
      if (valid() && sequence === this.sequence) this.loading.set(false);
    }
  }
  async read(id: string, poll = false) {
    const sequence = ++this.readSequence,
      repository = this.repository(),
      valid = this.scope.guard();
    try {
      const detail = await this.api.review(id);
      if (!valid() || sequence !== this.readSequence || repository !== this.repository()) return;
      if (detail.review.repository !== repository) throw new Error('這份 review 屬於其他程式庫。');
      this.detail.set(detail);
      if (['queued', 'running'].includes(detail.review.job!.status))
        this.scope.later(() => void this.read(id, true), 3000, 'review');
      else if (poll) {
        const reviews = await this.api.reviews(repository);
        if (valid() && sequence === this.readSequence && repository === this.repository())
          this.reviews.set(reviews);
      }
    } catch (e) {
      if (valid() && sequence === this.readSequence) {
        this.detail.set(null);
        this.error.set(this.scope.message(e));
      }
    }
  }
  async choose(id: string) {
    this.scope.cancel('review');
    this.error.set('');
    await this.read(id);
  }
  async create(event: Event) {
    event.preventDefault();
    if (this.busy()) return;
    const sha = /^[\da-f]{40}$|^[\da-f]{64}$/i;
    if (
      !sha.test(this.commit().trim()) ||
      (this.mode() === 'range' && !sha.test(this.basis().trim()))
    ) {
      this.error.set('請選擇或貼上完整的 40／64 位 commit SHA。');
      return;
    }
    const request = {
      repository: this.repository(),
      commit: this.commit().trim(),
      baseCommit: this.mode() === 'range' ? this.basis().trim() : null,
      modelId: this.model(),
      note: this.note(),
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
