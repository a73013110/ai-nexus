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
import { action } from '@shared/browser/action';
import { ViewScope } from '@shared/browser/view-scope';
import { CompactDialog } from '@shared/ui/compact-dialog';
import { DialogHeader } from '@shared/ui/dialog-header';
import { Notice } from '@shared/ui/notice';
import { Select } from '@shared/ui/select';
import { ToastService } from '@shared/ui/toast-service';
import { KnowledgeApi } from '@features/work/knowledge/knowledge-api';
import { JobToasts } from '@features/work/jobs/job-toasts';
import { RepositoriesApi } from '../repositories-api';
import { RepositoryStore, shortSha } from '../repository-store';
import { routeOf } from '@core/api/catalog';
import { DialogActions } from '@shared/ui/dialog-actions';

/** 以釘選的提交，把資料夾下的文字檔匯入知識庫集合。 */
@Component({
  selector: 'nx-repository-import-dialog',
  imports: [DialogActions, RouterLink, CompactDialog, DialogHeader, Notice, Select],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<dialog nxCompactDialog #dialog class="platform-dialog" [busy]="saving.pending()">
    <nx-dialog-header heading="匯入知識庫" closeLabel="關閉匯入知識庫" />
    <div class="dialog-body">
      <p class="form-note">
        匯入 <strong>{{ folder() || store.fullName() }}</strong> 底下的文字檔（版本
        {{ short(commit()) }}），在背景逐一建立檢索索引。二進位與過大的檔案會略過。
      </p>
      @if (error()) {
        <nx-notice tone="danger" [message]="error()" />
      }
      @if (!loading() && !choices().length) {
        @if (!error()) {
          <p>目前沒有可加入的知識庫。</p>
          <a class="secondary-button" [routerLink]="routeOf('knowledge')" (click)="dialog.close()"
            >前往建立知識庫</a
          >
        }
      } @else {
        <nx-select
          label="目標知識庫"
          [options]="choices()"
          [value]="selected()"
          [loading]="loading()"
          [disabled]="saving.pending()"
          (valueChange)="selected.set($event)"
        />
      }
    </div>
    @if (loading() || choices().length) {
      <nx-dialog-actions
        label="開始匯入"
        pendingLabel="安排中…"
        [pending]="saving.pending()"
        [disabled]="loading() || !selected()"
        [submit]="false"
        (confirm)="save()"
        (dismiss)="dialog.close()"
      />
    }
  </dialog>`,
})
export class RepositoryImportDialog {
  /** 範本裡的連結由功能目錄取得網址。 */
  readonly routeOf = routeOf;
  private readonly api = inject(RepositoriesApi);
  private readonly knowledge = inject(KnowledgeApi);
  private readonly toast = inject(ToastService);
  private readonly jobs = inject(JobToasts);
  readonly store = inject(RepositoryStore);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  readonly folder = signal('');
  readonly commit = signal('');
  readonly short = shortSha;
  /** 每次開啟都會重新讀取集合；第一次開啟前不讀取任何東西。 */
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
      .filter((collection) => collection.resource.canEdit)
      .map((collection) => ({
        value: collection.resource.id,
        label: collection.resource.name,
        description: collection.resource.isOwner
          ? '私人或由你共用的知識庫'
          : '共用知識庫 · 成員可閱讀',
      })),
  );
  readonly selected = linkedSignal(() => this.choices()[0]?.value ?? '');
  readonly loading = this.collectionsRead.refreshing;
  readonly saving = action();
  readonly error = computed(() => this.saving.error() || this.collectionsRead.error());

  open(folder: string, commit: string) {
    this.folder.set(folder);
    this.commit.set(commit);
    this.saving.error.set('');
    this.opened.update((count) => count + 1);
    this.dialog().nativeElement.showModal();
  }
  async save() {
    const key = this.store.key(),
      collection = this.choices().find((item) => item.value === this.selected());
    if (!key || !collection || this.saving.pending() || this.loading()) return;
    const outcome = await this.saving.run(async () => {
      const result = await this.api.import(key, {
        commit: this.commit(),
        path: this.folder(),
        collectionId: collection.value,
      });
      // 匯入在背景進行：已離開頁面也照樣追蹤與提示。
      this.jobs.track(result.job);
      this.toast.info(
        `開始匯入 ${result.files} 個檔案`,
        `完成後可在「${collection.label}」查詢。可以先離開這個頁面。`,
      );
    });
    if (outcome.ok) this.dialog().nativeElement.close();
  }
}
