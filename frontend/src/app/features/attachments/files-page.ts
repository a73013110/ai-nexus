import { Notice } from '@shared/ui/notice';
import { ToastService } from '@shared/ui/toast-service';
import { EmptyState } from '@shared/ui/empty-state';
import { ResourceGallery } from '@shared/ui/resource-gallery';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import type { AttachmentPolicyDto, LibraryFileDto } from '@core/api/schema';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { ViewScope } from '@shared/browser/view-scope';
import { FileDrop } from '@shared/browser/file-drop';
import { formatBytes } from '@shared/browser/format';
import { FeaturePage } from '@core/layout/feature-page';
import { Icon } from '@shared/ui/icon';
import { Select } from '@shared/ui/select';
import { NameDialog } from '@shared/ui/name-dialog';
import { ConfirmDialog } from '@shared/ui/confirm-dialog';
import { TablePagination } from '@shared/ui/data-table';
import { AttachmentsApi } from './attachments-api';
import { apiResource } from '@core/api/api-resource';
import { FileBrowser } from './file-library/file-browser';
import { FileLibraryStore } from './file-library-store';
import { AddToKnowledge } from './file-library/add-to-knowledge';
import { StorageUsage } from './file-library/storage-usage';
import { filesTour } from './files-tour';

@Component({
  selector: 'nx-files-page',
  imports: [
    Notice,
    EmptyState,
    ResourceGallery,
    FeaturePage,
    Icon,
    Select,
    FileDrop,
    FileBrowser,
    AddToKnowledge,
    ConfirmDialog,
    NameDialog,
    StorageUsage,
    TablePagination,
  ],
  providers: [ViewScope, FileLibraryStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './files-page.scss',
  templateUrl: './files-page.html',
})
export class FilesPage {
  readonly store = inject(FileLibraryStore);
  readonly session = inject(WorkspaceSession);
  private readonly api = inject(AttachmentsApi);
  private readonly scope = inject(ViewScope);
  readonly knowledge = viewChild.required(AddToKnowledge);
  readonly names = viewChild.required(NameDialog);
  private readonly confirm = viewChild.required(ConfirmDialog);
  private readonly gallery = viewChild.required<ElementRef<HTMLElement>>('gallery');
  private readonly policyRead = apiResource({ loader: () => this.api.policy() });
  readonly policy = computed<AttachmentPolicyDto | null>(() => this.policyRead.value() ?? null);
  readonly uploading = signal(false);
  readonly busy = signal(false);
  readonly actionError = signal('');
  readonly error = computed(
    () => this.actionError() || this.policyRead.error() || this.store.error(),
  );
  readonly toast = inject(ToastService);
  readonly bytes = formatBytes;
  readonly tour = computed(() => filesTour({ knowledge: this.session.has('knowledge') }));
  readonly accept = computed(() => this.policy()?.extensions.join(',') || '');
  readonly kinds = [
    { value: 'all', label: '全部' },
    { value: 'images', label: '圖片' },
    { value: 'documents', label: '文件' },
  ];
  readonly sources = [
    { value: 'all', label: '全部來源' },
    { value: 'chat', label: '對話附件' },
    { value: 'knowledge', label: '知識庫來源' },
    { value: 'projects', label: '專案文件' },
    { value: 'library', label: '尚未引用' },
  ];
  private controller?: AbortController;
  constructor() {
    this.store.load();
    inject(DestroyRef).onDestroy(() => this.controller?.abort());
  }
  reload() {
    this.actionError.set('');
    this.policyRead.reload();
    this.store.load();
  }
  /** 分頁在格線下方，翻頁後捲回格線開頭，否則畫面會停在新一頁的最後幾張。 */
  turn(index: number) {
    this.store.select(index);
    const gallery = this.gallery().nativeElement;
    if (gallery.getBoundingClientRect().top < 0) gallery.scrollIntoView({ block: 'start' });
  }
  async upload(files: FileList | File[]) {
    const candidates = Array.from(files),
      policy = this.policy();
    if (this.uploading() || !policy || !candidates.length) return;
    if (
      candidates.length > 10 ||
      candidates.some(
        (file) =>
          !file.size ||
          file.size > policy.maxFileBytes ||
          !policy.extensions.includes('.' + file.name.split('.').pop()?.toLowerCase()),
      )
    ) {
      this.actionError.set(
        `每批最多十份檔案，每份最多 ${this.bytes(policy.maxFileBytes)}。支援 ${policy.extensions.join('、')}。`,
      );
      return;
    }
    const valid = this.scope.guard();
    this.controller = new AbortController();
    this.uploading.set(true);
    this.actionError.set('');
    // One toast follows the batch from the first file to done; a failure withdraws it.
    const progress = 'library-upload';
    let saved = 0,
      done = false;
    try {
      for (const file of candidates) {
        if (!valid()) return;
        this.toast.show({
          id: progress,
          tone: 'info',
          title: '正在保存 ' + file.name,
          duration: 0,
        });
        const uploaded = await this.api.upload(file, this.controller.signal);
        if (!valid()) return;
        await this.api.retainFile(uploaded.id);
        saved++;
      }
      this.toast.show({
        id: progress,
        title: `${saved} 份檔案已保存`,
        description: '可預覽、再次加入對話，或指定加入知識庫。',
      });
      done = true;
    } catch (error) {
      if (valid()) this.actionError.set(this.scope.message(error));
    } finally {
      if (!done) this.toast.dismiss(progress);
      if (valid()) {
        this.uploading.set(false);
        this.store.load();
      }
    }
  }
  async remove(item: LibraryFileDto) {
    if (this.busy() || !item.canDelete) return;
    const valid = this.scope.guard();
    if (
      !(await this.confirm().ask({
        title: '刪除檔案',
        message: `將永久刪除「${item.file.fileName}」與其私人閱讀文字。`,
        confirm: '刪除檔案',
        danger: true,
      })) ||
      !valid()
    )
      return;
    this.busy.set(true);
    this.actionError.set('');
    try {
      await this.api.removeFile(item.file.id);
      if (valid()) {
        this.toast.success('檔案已刪除');
        if (this.store.items().length === 1 && this.store.offset()) this.store.page(-1);
        this.store.load();
      }
    } catch (error) {
      if (valid()) this.actionError.set(this.scope.message(error));
    } finally {
      if (valid()) this.busy.set(false);
    }
  }
  rename(item: LibraryFileDto) {
    if (this.busy()) return;
    const valid = this.scope.guard();
    this.names().open({
      title: '重新命名檔案',
      value: item.file.fileName,
      maxLength: 180,
      description: '副檔名須保留。既有分享與知識庫來源保留當時的名稱。',
      save: async (name) => {
        await this.api.renameFile(item.file.id, name, item.file.fileName);
        if (valid()) {
          this.toast.success('檔案名稱已更新');
          this.store.load();
        }
      },
    });
  }
  added() {
    this.toast.success('已加入知識庫', '文字辨識與索引會在背景處理。');
    this.store.load();
  }
}
