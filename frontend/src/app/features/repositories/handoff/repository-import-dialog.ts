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
import { RouterLink } from '@angular/router';
import { apiResource } from '@core/api/api-resource';
import type { CollectionDto } from '@core/api/schema';
import { ViewScope } from '@shared/browser/view-scope';
import { CompactDialog } from '@shared/ui/compact-dialog';
import { DialogHeader } from '@shared/ui/dialog-header';
import { Notice } from '@shared/ui/notice';
import { Select } from '@shared/ui/select';
import { ToastService } from '@shared/ui/toast-service';
import { KnowledgeApi } from '@features/knowledge/knowledge-api';
import { JobToasts } from '@features/jobs/job-toasts';
import { RepositoriesApi } from '../repositories-api';
import { RepositoryStore, shortSha } from '../repository-store';

/** Imports the text files under a folder, at the pinned commit, into a knowledge collection. */
@Component({
  selector: 'nx-repository-import-dialog',
  imports: [RouterLink, CompactDialog, DialogHeader, Notice, Select],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<dialog
    nxCompactDialog
    #dialog
    class="platform-dialog"
    (cancel)="saving() ? $event.preventDefault() : null"
  >
    <nx-dialog-header heading="匯入知識庫" closeLabel="關閉匯入知識庫" />
    <div class="dialog-body">
      <p class="form-note">
        匯入 <strong>{{ folder() || store.fullName() }}</strong> 底下的文字檔（版本
        {{ short(commit()) }}），在背景逐一建立檢索索引；二進位與過大的檔案會略過。
      </p>
      @if (error()) {
        <nx-notice tone="danger" [message]="error()" />
      }
      @if (loading()) {
        <p class="form-note" role="status">正在載入可編輯的知識庫…</p>
      } @else if (!choices().length) {
        <p>目前沒有可加入的知識庫。</p>
        <a class="secondary-button" routerLink="/knowledge" (click)="dialog.close()"
          >前往建立知識庫</a
        >
      } @else {
        <nx-select
          label="目標知識庫"
          [options]="choices()"
          [value]="selected()"
          (valueChange)="selected.set($event)"
        />
      }
    </div>
    @if (!loading() && choices().length) {
      <footer class="dialog-foot">
        <button
          type="button"
          class="secondary-button"
          [disabled]="saving()"
          (click)="dialog.close()"
        >
          取消
        </button>
        <button
          type="button"
          class="primary-button"
          [disabled]="saving() || !selected()"
          (click)="save()"
        >
          {{ saving() ? '正在安排…' : '開始匯入' }}
        </button>
      </footer>
    }
  </dialog>`,
})
export class RepositoryImportDialog {
  private readonly api = inject(RepositoriesApi);
  private readonly knowledge = inject(KnowledgeApi);
  private readonly scope = inject(ViewScope);
  private readonly toast = inject(ToastService);
  private readonly jobs = inject(JobToasts);
  readonly store = inject(RepositoryStore);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  readonly folder = signal('');
  readonly commit = signal('');
  readonly short = shortSha;
  /** Each opening reads the collections again; nothing is read before the first one. */
  private readonly opened = signal(0);
  private readonly collectionsRead = apiResource({
    params: () => this.opened() || undefined,
    loader: () => this.knowledge.collections(),
  });
  private readonly collections = computed<CollectionDto[]>(
    () => this.collectionsRead.value() ?? [],
  );
  readonly choices = computed(() =>
    this.collections()
      .filter((x) => x.resource.canEdit)
      .map((x) => ({
        value: x.resource.id,
        label: x.resource.name,
        description: x.resource.isOwner ? '私人或由你共用的知識庫' : '共用知識庫 · 成員可閱讀',
      })),
  );
  readonly selected = linkedSignal(() => this.choices()[0]?.value ?? '');
  readonly loading = this.collectionsRead.refreshing;
  readonly saving = signal(false);
  private readonly saveError = signal('');
  readonly error = computed(() => this.saveError() || this.collectionsRead.error());

  open(folder: string, commit: string) {
    this.folder.set(folder);
    this.commit.set(commit);
    this.saveError.set('');
    this.opened.update((x) => x + 1);
    this.dialog().nativeElement.showModal();
  }
  async save() {
    const key = this.store.key(),
      collection = this.choices().find((x) => x.value === this.selected());
    if (!key || !collection || this.saving()) return;
    const valid = this.scope.guard();
    this.saving.set(true);
    this.saveError.set('');
    try {
      const result = await this.api.import(key, {
        commit: this.commit(),
        path: this.folder(),
        collectionId: collection.value,
      });
      this.jobs.track(result.job);
      this.toast.info(
        `開始匯入 ${result.files} 個檔案`,
        `完成後可在「${collection.label}」查詢；可以先離開這個頁面。`,
      );
      if (valid()) this.dialog().nativeElement.close();
    } catch (e) {
      if (valid()) this.saveError.set(this.scope.message(e));
    } finally {
      if (valid()) this.saving.set(false);
    }
  }
}
