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
import { apiResource } from '@core/api/api-resource';
import { WorkspaceApi } from '@core/api/workspace-api';
import { action } from '@shared/browser/action';
import { ViewScope } from '@shared/browser/view-scope';
import { CompactDialog } from '@shared/ui/compact-dialog';
import { DialogHeader } from '@shared/ui/dialog-header';
import { Field } from '@shared/ui/field';
import { Icon } from '@shared/ui/icon';
import { Notice } from '@shared/ui/notice';
import { PendingButton } from '@shared/ui/pending-button';
import { Select } from '@shared/ui/select';
import { ViewSwitch } from '@shared/ui/view-switch';
import { JobToasts } from '@features/work/jobs/job-toasts';
import { RepositoriesApi } from '../repositories-api';
import { RepositoryStore } from '../repository-store';

/** 審查讀取的內容：一個提交、一個範圍，或拉取請求的 head 對比其合併基底。 */
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

/** 啟動背景審查；對話框與頁面關閉後它仍會繼續執行。 */
@Component({
  selector: 'nx-repository-review-dialog',
  imports: [CompactDialog, DialogHeader, Field, Icon, Notice, PendingButton, Select, ViewSwitch],
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
  template: `<dialog nxCompactDialog #dialog class="platform-dialog" [busy]="saving.pending()">
    <form class="dialog-form" (submit)="save($event)">
      <nx-dialog-header heading="交給 AI 初檢" />
      <div class="dialog-body">
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
        @if (showModels()) {
          <nx-select
            label="模型"
            [options]="choices()"
            [value]="model()"
            [loading]="modelsLoading()"
            [disabled]="saving.pending()"
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
            [readOnly]="saving.pending()"
            [value]="note()"
            placeholder="例如：只看權限判斷，或確認金額計算"
            (input)="note.set($any($event.target).value)"
          ></textarea>
        </label>
        @if (error()) {
          <nx-notice tone="danger" [message]="error()" />
        }
      </div>
      <footer class="dialog-foot">
        <p class="dialog-note">在背景執行並使用你的模型額度；完成時會通知你，可以先離開。</p>
        <button
          type="button"
          class="secondary-button"
          [attr.aria-disabled]="saving.pending() || null"
          (click)="saving.pending() || dialog.close()"
        >
          取消
        </button>
        <button
          nxPending
          class="primary-button"
          [pending]="saving.pending()"
          [disabled]="modelsLoading() || !model()"
        >
          {{ saving.pending() ? '安排中…' : '開始' }}
        </button>
      </footer>
    </form>
  </dialog>`,
})
export class RepositoryReviewDialog {
  private readonly api = inject(RepositoriesApi);
  private readonly workspaceApi = inject(WorkspaceApi);
  private readonly store = inject(RepositoryStore);
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
    loader: () => this.workspaceApi.models(),
  });
  readonly models = computed(() => this.modelsRead.value() ?? null);
  readonly choices = computed(
    () => this.models()?.models.map((item) => ({ value: item.id, label: item.displayName })) ?? [],
  );
  /** 每次開啟都重讀模型清單；讀取中模型下拉轉圈，開始鈕等它。 */
  readonly modelsLoading = this.modelsRead.refreshing;
  /** 第一次讀取時還不知道能不能選模型，先顯示讀取中的下拉；之後依政策與可選數量決定。 */
  readonly showModels = computed(() => {
    const models = this.models();
    if (!models) return this.modelsLoading();
    return models.policy.allowModelSelection && this.choices().length > 1;
  });
  readonly model = linkedSignal(() => {
    const models = this.models();
    return models?.policy.defaultModelId || models?.models[0]?.id || '';
  });
  readonly note = signal('');
  readonly saving = action();
  readonly error = computed(() => this.saving.error() || this.modelsRead.error());
  /** 每次開啟一個 key，因此重試的送出不會啟動同一個審查兩次。 */
  private key = '';

  open(target: ReviewTarget) {
    this.target.set(target);
    this.purpose.set(target.purpose ?? 'review');
    this.note.set('');
    this.saving.error.set('');
    this.key = crypto.randomUUID();
    this.opened.update((count) => count + 1);
    this.dialog().nativeElement.showModal();
  }
  async save(event: Event) {
    event.preventDefault();
    const target = this.target(),
      repository = this.store.key();
    if (!target || !repository || this.saving.pending() || !this.model()) return;
    const outcome = await this.saving.run(async () => {
      const review = await this.api.createReview(repository, {
        commit: target.commit,
        baseCommit: target.baseCommit,
        pullRequest: target.pullRequest,
        modelId: this.model(),
        note: this.note().trim() || null,
        purpose: this.purpose(),
        idempotencyKey: this.key,
      });
      // 已離開頁面也要追蹤：工作在背景完成時仍會通知。
      this.jobs.track(review.job);
      return review;
    });
    if (!outcome.ok) return;
    this.dialog().nativeElement.close();
    void this.router.navigate(this.store.link('reviews', outcome.value.id));
  }
}
