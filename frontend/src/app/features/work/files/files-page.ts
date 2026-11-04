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
  effect,
  inject,
  untracked,
  viewChild,
} from '@angular/core';
import type { AttachmentPolicyDto, LibraryFileDto } from '@core/api/schema';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { action } from '@shared/browser/action';
import { ViewScope } from '@shared/browser/view-scope';
import { FileDrop } from '@shared/browser/file-drop';
import { formatBytes } from '@shared/browser/format';
import { FeaturePage } from '@core/layout/feature-page';
import { Icon } from '@shared/ui/icon';
import { PendingButton } from '@shared/ui/pending-button';
import { Select } from '@shared/ui/select';
import { NameDialog } from '@shared/ui/name-dialog';
import { ConfirmDialog } from '@shared/ui/confirm-dialog';
import { TablePagination } from '@shared/ui/data-table';
import { acceptsFile, FilesApi } from './files-api';
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
    PendingButton,
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
  private readonly api = inject(FilesApi);
  readonly knowledge = viewChild.required(AddToKnowledge);
  private readonly names = viewChild.required(NameDialog);
  private readonly confirm = viewChild.required(ConfirmDialog);
  private readonly gallery = viewChild.required<ElementRef<HTMLElement>>('gallery');
  private readonly policyRead = apiResource({ loader: () => this.api.policy() });
  readonly policy = computed<AttachmentPolicyDto | null>(() => this.policyRead.value() ?? null);
  readonly uploading = action();
  /** 刪除檔案（`key` 是檔案 id）：只有那張卡片的選單鈕轉圈，其他卡片照常可操作。 */
  readonly deleting = action();
  readonly isDeleting = (id: string) => this.deleting.pendingFor(id);
  readonly error = computed(
    () =>
      this.uploading.error() ||
      this.deleting.error() ||
      this.policyRead.error() ||
      this.store.error(),
  );
  private readonly toast = inject(ToastService);
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
  constructor() {
    this.store.load();
    inject(DestroyRef).onDestroy(() => this.uploading.cancel());
    // 分頁在格線下方：換頁或換條件後捲回格線開頭，否則畫面會停在新一頁的最後幾張。背景重讀不改 revision。
    effect(() => {
      if (!this.store.table.revision()) return;
      untracked(() => {
        const gallery = this.gallery().nativeElement;
        if (gallery.getBoundingClientRect().top < 0) gallery.scrollIntoView({ block: 'start' });
      });
    });
  }
  reload() {
    this.uploading.error.set('');
    this.deleting.error.set('');
    this.policyRead.reload();
    this.store.load();
  }
  chooseFiles(picker: HTMLInputElement) {
    void this.upload(picker.files!);
    picker.value = '';
  }
  async upload(files: FileList | File[]) {
    const candidates = Array.from(files),
      policy = this.policy();
    if (this.uploading.pending() || !policy || !candidates.length) return;
    if (
      candidates.length > 10 ||
      candidates.some((file) => !file.size || !acceptsFile(policy, file))
    ) {
      this.uploading.fail(
        `每批最多十份檔案，每份最多 ${formatBytes(policy.maxFileBytes)}。支援 ${policy.extensions.join('、')}。`,
      );
      return;
    }
    // 一則提示從第一個檔案追蹤到完成；失敗時撤回它。
    const progress = 'library-upload';
    let saved = 0;
    const outcome = await this.uploading.run(async ({ alive, signal }) => {
      for (const file of candidates) {
        if (!alive()) return;
        this.toast.show({
          id: progress,
          tone: 'info',
          title: '正在保存 ' + file.name,
          duration: 0,
        });
        const uploaded = await this.api.upload(file, signal);
        if (!alive()) return;
        await this.api.retainFile(uploaded.id);
        saved++;
      }
    });
    if (outcome.ok)
      this.toast.show({
        id: progress,
        title: `${saved} 份檔案已保存`,
        description: '可預覽、再次加入對話，或指定加入知識庫。',
      });
    else this.toast.dismiss(progress);
    // 中途失敗時已保存的那幾份也要出現在列表。
    if (outcome.ok || outcome.error) this.store.load();
  }
  async remove(item: LibraryFileDto) {
    if (this.deleting.pending() || !item.canDelete) return;
    if (
      !(await this.confirm().ask({
        title: '刪除檔案',
        message: `將永久刪除「${item.file.fileName}」與其私人閱讀文字。`,
        confirm: '刪除檔案',
        danger: true,
      }))
    )
      return;
    const outcome = await this.deleting.run(() => this.api.removeFile(item.file.id), {
      key: item.file.id,
    });
    if (!outcome.ok) return;
    this.toast.success('檔案已刪除');
    // 刪掉本頁最後一張就回上一頁；換頁本身會重讀。
    if (this.store.items().length === 1 && this.store.table.pageIndex()) this.store.table.turn(-1);
    else this.store.load();
  }
  rename(item: LibraryFileDto) {
    if (this.isDeleting(item.file.id)) return;
    this.names().open({
      title: '重新命名檔案',
      value: item.file.fileName,
      maxLength: 180,
      description: '副檔名須保留。既有分享與知識庫來源保留當時的名稱。',
      save: async (name) => {
        await this.api.renameFile(item.file.id, name, item.file.fileName);
        this.toast.success('檔案名稱已更新');
        this.store.load();
      },
    });
  }
  added() {
    this.toast.success('已加入知識庫', '文字辨識與索引會在背景處理。');
    this.store.load();
  }
}
