import {
  ChangeDetectionStrategy,
  Component,
  computed,
  ElementRef,
  inject,
  linkedSignal,
  signal,
  viewChild,
} from '@angular/core';
import { Router } from '@angular/router';
import { apiResource } from '../../core/api/api-resource';
import { NexusApi } from '../../core/api/nexus-api';
import { ViewScope } from '../../shared/browser/view-scope';
import { CompactDialog } from '../../shared/ui/compact-dialog';
import { Field } from '../../shared/ui/field';
import { Icon } from '../../shared/ui/icon';
import { Notice } from '../../shared/ui/notice';
import { Select } from '../../shared/ui/select';
import { ViewSwitch } from '../../shared/ui/view-switch';
import { JobToasts } from '../tasks/job-toasts';
import { RepositoriesApi } from './repositories-api';
import { RepositoryStore } from './repository-store';

/** What a review reads: one commit, a range, or a pull request's head against its merge base. */
export interface ReviewTarget {
  label: string;
  commit: string | null;
  baseCommit: string | null;
  pullRequest: number | null;
  purpose?: 'review' | 'summary';
}

const purposes = {
  review: '找出有證據的重大缺陷：邏輯錯誤、權限與資料風險、遺漏的邊界條件。',
  summary: '用繁體中文說明改了什麼、為什麼改，以及最需要確認的影響。',
} as const;

/** Starts a background review; it keeps running after the dialog and the page are closed. */
@Component({
  selector: 'nx-repository-review-dialog',
  imports: [CompactDialog, Field, Icon, Notice, Select, ViewSwitch],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    .target {
      display: flex;
      align-items: center;
      gap: var(--p-space-2);
      margin: 0;
      padding: var(--p-space-3);
      border-radius: var(--tile-inner-radius);
      background: var(--tile-inset);
      font: var(--text-caption) / 1.4 var(--font-code);
      overflow-wrap: anywhere;
      nx-icon {
        flex: none;
        color: var(--accent);
        --icon-size: var(--icon-sm);
      }
    }
    .purpose {
      display: grid;
      gap: var(--p-space-2);
    }
  `,
  template: `<dialog
    nxCompactDialog
    #dialog
    class="platform-dialog"
    (cancel)="saving() && $event.preventDefault()"
  >
    <form class="platform-form dialog-scroll" (submit)="save($event)">
      <div class="dialog-heading">
        <h2>交給 AI 初檢</h2>
        <button
          type="button"
          class="icon-button"
          aria-label="關閉"
          [disabled]="saving()"
          (click)="dialog.close()"
        >
          <nx-icon name="close" />
        </button>
      </div>
      <p class="target"><nx-icon name="review" />{{ target()?.label }}</p>
      <div class="purpose">
        <nx-view-switch
          label="檢閱目的"
          appearance="segment"
          [fill]="true"
          [options]="purposeOptions"
          [value]="purpose()"
          (valueChange)="purpose.set($any($event))"
        />
        <p class="form-note">{{ purposeNote() }}</p>
      </div>
      @if (models()?.policy?.allowModelSelection && choices().length > 1) {
        <nx-select
          label="模型"
          [options]="choices()"
          [value]="model()"
          (valueChange)="model.set($event)"
        />
      }
      @if (models()?.notice) {
        <nx-notice tone="info" [message]="models()!.notice" />
      }
      <label
        >特別關注（選填）<textarea
          nxField
          rows="2"
          maxlength="2000"
          [value]="note()"
          placeholder="例如：只看權限判斷，或確認金額計算"
          (input)="note.set($any($event.target).value)"
        ></textarea>
      </label>
      @if (error()) {
        <nx-notice tone="danger" [message]="error()" />
      }
      <p class="form-note">在背景執行並使用你的模型額度；完成時會通知你，可以先離開。</p>
      <div class="dialog-actions">
        <button
          type="button"
          class="secondary-button"
          [disabled]="saving()"
          (click)="dialog.close()"
        >
          取消
        </button>
        <button class="primary-button" [disabled]="saving() || !model()">
          {{ saving() ? '正在安排…' : '開始' }}
        </button>
      </div>
    </form>
  </dialog>`,
})
export class RepositoryReviewDialog {
  private readonly api = inject(RepositoriesApi);
  private readonly nexus = inject(NexusApi);
  private readonly store = inject(RepositoryStore);
  private readonly scope = inject(ViewScope);
  private readonly jobs = inject(JobToasts);
  private readonly router = inject(Router);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  readonly target = signal<ReviewTarget | null>(null);
  readonly purposeOptions = [
    { value: 'review', label: '快速檢閱', icon: 'review' },
    { value: 'summary', label: '變更摘要', icon: 'document' },
  ];
  readonly purpose = signal<keyof typeof purposes>('review');
  readonly purposeNote = computed(() => purposes[this.purpose()]);
  private readonly opened = signal(0);
  private readonly modelsRead = apiResource({
    feature: 'repositories',
    params: () => this.opened() || undefined,
    loader: () => this.nexus.models(),
  });
  readonly models = computed(() => this.modelsRead.value() ?? null);
  readonly choices = computed(
    () => this.models()?.models.map((x) => ({ value: x.id, label: x.displayName })) ?? [],
  );
  readonly model = linkedSignal(() => {
    const models = this.models();
    return models?.policy.defaultModelId || models?.models[0]?.id || '';
  });
  readonly note = signal('');
  readonly saving = signal(false);
  private readonly saveError = signal('');
  readonly error = computed(() => this.saveError() || this.modelsRead.error());
  /** One key per opening, so a retried submit cannot start the same review twice. */
  private key = '';

  open(target: ReviewTarget) {
    this.target.set(target);
    this.purpose.set(target.purpose ?? 'review');
    this.note.set('');
    this.saveError.set('');
    this.key = crypto.randomUUID();
    this.opened.update((x) => x + 1);
    this.dialog().nativeElement.showModal();
  }
  async save(event: Event) {
    event.preventDefault();
    const target = this.target(),
      repository = this.store.key();
    if (!target || !repository || this.saving() || !this.model()) return;
    const valid = this.scope.guard();
    this.saving.set(true);
    this.saveError.set('');
    try {
      const review = await this.api.createReview(repository, {
        commit: target.commit,
        baseCommit: target.baseCommit,
        pullRequest: target.pullRequest,
        modelId: this.model(),
        note: this.note().trim() || null,
        purpose: this.purpose(),
        idempotencyKey: this.key,
      });
      this.jobs.track(review.job);
      if (!valid()) return;
      this.dialog().nativeElement.close();
      void this.router.navigate(this.store.link('reviews', review.id));
    } catch (e) {
      if (valid()) this.saveError.set(this.scope.message(e));
    } finally {
      if (valid()) this.saving.set(false);
    }
  }
}
