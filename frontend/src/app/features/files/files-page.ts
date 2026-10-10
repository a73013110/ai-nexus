import { Notice } from '../../shared/ui/notice';
import { Card } from '../../shared/ui/card';
import { EmptyState } from '../../shared/ui/empty-state';
import { FilterPanel } from '../../shared/ui/filter-panel';
import { ViewSwitch } from '../../shared/ui/view-switch';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import type { AttachmentPolicyDto, LibraryFileDto } from '../../core/api/schema';
import { WorkspaceSession } from '../../core/auth/workspace-session';
import { ViewScope } from '../../shared/browser/view-scope';
import { FileDrop } from '../../shared/browser/file-drop';
import { formatBytes } from '../../shared/browser/format';
import { FeaturePage } from '../../core/layout/feature-page';
import { Icon } from '../../shared/ui/icon';
import { SearchField } from '../../shared/ui/search-field';
import { Select } from '../../shared/ui/select';
import { NameDialog } from '../../shared/ui/name-dialog';
import { ConfirmDialog } from '../../shared/ui/confirm-dialog';
import { WorkspaceApi } from '../workspace/workspace-api';
import { FilesApi } from './files-api';
import { FileBrowser } from './file-browser';
import { FileLibraryStore } from './file-library-store';
import { AddToKnowledge } from './add-to-knowledge';
import { StorageUsage } from './storage-usage';

@Component({
  selector: 'nx-files-page',
  imports: [
    Notice,
    Card,
    EmptyState,
    FilterPanel,
    ViewSwitch,
    FeaturePage,
    Icon,
    SearchField,
    Select,
    FileDrop,
    FileBrowser,
    AddToKnowledge,
    ConfirmDialog,
    NameDialog,
    RouterLink,
    StorageUsage,
  ],
  providers: [ViewScope, FileLibraryStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './files-page.html',
})
export class FilesPage {
  readonly store = inject(FileLibraryStore);
  readonly session = inject(WorkspaceSession);
  private readonly api = inject(FilesApi);
  private readonly uploads = inject(WorkspaceApi);
  private readonly scope = inject(ViewScope);
  readonly knowledge = viewChild.required(AddToKnowledge);
  readonly names = viewChild.required(NameDialog);
  private readonly confirm = viewChild.required(ConfirmDialog);
  readonly policy = signal<AttachmentPolicyDto | null>(null);
  readonly layout = signal('grid');
  readonly uploading = signal(false);
  readonly uploadLabel = signal('');
  readonly busy = signal(false);
  readonly error = signal('');
  readonly notice = signal('');
  readonly bytes = formatBytes;
  readonly accept = computed(() => this.policy()?.extensions.join(',') || '');
  readonly kinds = [
    { id: 'all', name: '全部檔案' },
    { id: 'images', name: '圖片' },
    { id: 'documents', name: '文件' },
  ];
  readonly kindOptions = this.kinds.map((item) => ({ value: item.id, label: item.name }));
  readonly layoutOptions = [
    { value: 'grid', label: '網格排列', icon: 'dashboard' },
    { value: 'list', label: '清單排列', icon: 'lines' },
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
    void this.load();
    inject(DestroyRef).onDestroy(() => this.controller?.abort());
  }
  async load() {
    const valid = this.scope.guard();
    try {
      await this.session.load();
      if (!valid()) return;
      const policy = await this.uploads.attachmentPolicy();
      if (valid()) {
        this.policy.set(policy);
        await this.store.load();
      }
    } catch (error) {
      if (valid()) this.error.set(this.scope.message(error));
    }
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
      this.error.set(
        `每批最多十份檔案，每份最多 ${this.bytes(policy.maxFileBytes)}。支援 ${policy.extensions.join('、')}。`,
      );
      return;
    }
    const valid = this.scope.guard();
    this.controller = new AbortController();
    this.uploading.set(true);
    this.error.set('');
    this.notice.set('');
    let saved = 0;
    try {
      for (const file of candidates) {
        if (!valid()) return;
        this.uploadLabel.set(`正在保存 ${file.name}`);
        const uploaded = await this.uploads.upload(file, this.controller.signal);
        if (!valid()) return;
        await this.api.retain(uploaded.id);
        saved++;
      }
      if (valid())
        this.notice.set(`${saved} 份檔案已保存。可預覽、再次加入對話，或指定加入知識庫。`);
    } catch (error) {
      if (valid()) this.error.set(this.scope.message(error));
    } finally {
      if (valid()) {
        this.uploading.set(false);
        this.uploadLabel.set('');
        await this.store.load();
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
    this.error.set('');
    try {
      await this.api.remove(item.file.id);
      if (valid()) {
        this.notice.set('檔案已刪除。');
        if (this.store.items().length === 1 && this.store.offset())
          this.store.offset.update((value) => Math.max(0, value - 40));
        await this.store.load();
      }
    } catch (error) {
      if (valid()) this.error.set(this.scope.message(error));
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
        await this.api.rename(item.file.id, name, item.file.fileName);
        if (valid()) {
          this.notice.set('檔案名稱已更新。');
          await this.store.load();
        }
      },
    });
  }
  added() {
    this.notice.set('已加入知識庫。文字辨識與索引會在背景處理。');
    void this.store.load();
  }
}
