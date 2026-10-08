import { Card } from '../../shared/ui/card';
import { StatusBadge } from '../../shared/ui/status-badge';
import { EmptyState } from '../../shared/ui/empty-state';
import { SearchField } from '../../shared/ui/search-field';
import { CompactDialog } from '../../shared/ui/compact-dialog';
import { Field } from '../../shared/ui/field';
import { ClientValidationError } from '../../core/api/safe-errors';
import { IssueCode } from '../../shared/ui/issue-code';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import type {
  Collection,
  DocumentInfo,
  KnowledgeSearch,
  AttachmentPolicy,
} from '../../core/api/types';
import { WorkspaceSession } from '../../core/auth/workspace-session';
import { ViewScope } from '../../shared/browser/view-scope';
import { FeaturePage } from '../../shared/ui/feature-page';
import { Select } from '../../shared/ui/select';
import { Icon } from '../../shared/ui/icon';
import { ActionMenu, type MenuAction } from '../../shared/ui/action-menu';
import { ResourceSharing } from '../../shared/ui/resource-sharing';
import { FileDrop } from '../../shared/browser/file-drop';
import { WorkspaceApi } from '../workspace/workspace-api';
import { KnowledgeApi } from './knowledge-api';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { LibraryPicker } from '../files/library-picker';
import type { LibraryFile } from '../../core/api/types';
import { TextSourceEditor } from './text-source-editor';
import { ReaderLink } from '../../shared/browser/reader-link';
import { RetrievalResults } from '../../shared/ui/retrieval-results';

@Component({
  selector: 'nx-knowledge-page',
  imports: [
    StatusBadge,
    Card,
    EmptyState,
    SearchField,
    CompactDialog,
    Field,
    IssueCode,
    FeaturePage,
    Icon,
    Select,
    ActionMenu,
    ResourceSharing,
    FileDrop,
    RouterLink,
    LibraryPicker,
    ReaderLink,
    RetrievalResults,
    TextSourceEditor,
  ],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './knowledge-page.html',
})
export class KnowledgePage {
  private readonly api = inject(KnowledgeApi);
  private readonly uploads = inject(WorkspaceApi);
  private readonly scope = inject(ViewScope);
  readonly session = inject(WorkspaceSession);
  readonly collections = signal<Collection[]>([]);
  readonly selected = signal(inject(ActivatedRoute).snapshot.queryParamMap.get('collection') || '');
  readonly documents = signal<DocumentInfo[]>([]);
  readonly policy = signal<AttachmentPolicy | null>(null);
  readonly loading = signal(true);
  readonly loadingDocuments = signal(false);
  readonly error = signal('');
  readonly notice = signal('');
  readonly uploading = signal(false);
  readonly uploadLabel = signal('');
  readonly filter = signal('');
  readonly query = signal('');
  readonly searching = signal(false);
  readonly result = signal<KnowledgeSearch | null>(null);
  readonly editorName = signal('');
  readonly editorDescription = signal('');
  readonly editorId = signal<string | null>(null);
  readonly editorError = signal('');
  readonly saving = signal(false);
  readonly deleteTarget = signal<{ id: string; name: string; collection: boolean } | null>(null);
  readonly editorDialog = viewChild.required<ElementRef<HTMLDialogElement>>('editorDialog');
  readonly deleteDialog = viewChild.required<ElementRef<HTMLDialogElement>>('deleteDialog');
  readonly textEditor = viewChild.required(TextSourceEditor);
  readonly sharing = viewChild(ResourceSharing);
  readonly current = computed(() =>
    this.collections().find((x) => x.resource.id === this.selected()),
  );
  readonly choices = computed(() =>
    this.collections().map((x) => ({
      value: x.resource.id,
      label: x.resource.name,
      description: `${x.readyDocuments} / ${x.documents} 份文件可查詢${x.resource.isOwner ? '' : ' · 已共用'}`,
    })),
  );
  readonly visible = computed(() =>
    this.documents().filter((x) => x.fileName.toLowerCase().includes(this.filter().toLowerCase())),
  );
  readonly accept = computed(
    () => this.policy()?.extensions.join(',') || '.pdf,.docx,.txt,.md,.png,.jpg,.jpeg,.webp',
  );
  readonly collectionActions = computed<MenuAction[]>(() => [
    {
      id: 'edit',
      label: '修改名稱與說明',
      icon: 'edit',
      disabled: !this.current()?.resource.canEdit,
    },
    { id: 'access', label: '存取權限', icon: 'lock', disabled: !this.current()?.resource.isOwner },
    {
      id: 'delete',
      label: '移除此知識庫',
      icon: 'trash',
      danger: true,
      disabled: !this.current()?.resource.isOwner,
    },
  ]);
  private documentVersion = 0;
  constructor() {
    void this.load();
  }
  async load() {
    const valid = this.scope.guard();
    this.error.set('');
    try {
      await this.session.load();
      if (!valid() || !this.session.me()) return;
      if (!this.session.has('knowledge')) throw new ClientValidationError('featureAccess');
      const rows = await this.api.collections(),
        policy = await this.uploads.attachmentPolicy();
      if (!valid()) return;
      this.collections.set(rows);
      this.policy.set(policy);
      if (!rows.some((x) => x.resource.id === this.selected()))
        this.selected.set(rows[0]?.resource.id || '');
      if (this.selected()) await this.loadDocuments();
    } catch (error) {
      if (valid()) this.error.set(this.scope.message(error));
    } finally {
      if (valid()) this.loading.set(false);
    }
  }
  async select(id: string) {
    this.selected.set(id);
    this.result.set(null);
    this.query.set('');
    this.filter.set('');
    await this.loadDocuments();
  }
  async addLibrary(file: LibraryFile) {
    const collection = this.selected(),
      valid = this.scope.guard();
    if (!collection || this.uploading() || !this.current()?.resource.canEdit) return;
    this.uploading.set(true);
    this.error.set('');
    try {
      await this.api.add(collection, file.file.id);
      if (valid()) {
        this.notice.set('已從檔案庫加入來源，索引會在背景建立。');
        await this.load();
      }
    } catch (error) {
      if (valid()) this.error.set(this.scope.message(error));
    } finally {
      if (valid()) this.uploading.set(false);
    }
  }
  async loadDocuments(poll = false) {
    const id = this.selected(),
      version = ++this.documentVersion,
      guard = this.scope.guard(),
      valid = () => guard() && version === this.documentVersion && id === this.selected();
    if (!id) return;
    if (!poll) this.loadingDocuments.set(true);
    try {
      const rows = await this.api.documents(id);
      if (!valid()) return;
      this.documents.set(rows);
      if (rows.some((x) => ['queued', 'running', 'processing'].includes(x.status)))
        this.scope.later(
          () => {
            if (id === this.selected()) void this.loadDocuments(true);
          },
          2500,
          'documents',
        );
    } catch (error) {
      if (valid()) this.error.set(this.scope.message(error));
    } finally {
      if (valid()) this.loadingDocuments.set(false);
    }
  }
  openEditor(edit = false) {
    const current = edit ? this.current() : null;
    this.editorId.set(current?.resource.id || null);
    this.editorName.set(current?.resource.name || '');
    this.editorDescription.set(current?.description || '');
    this.editorError.set('');
    this.editorDialog().nativeElement.showModal();
  }
  async save(event: Event) {
    event.preventDefault();
    if (this.saving()) return;
    const valid = this.scope.guard();
    this.saving.set(true);
    this.editorError.set('');
    try {
      if (this.editorId())
        await this.api.update(this.editorId()!, this.editorName(), this.editorDescription());
      else {
        const created = await this.api.create(this.editorName(), this.editorDescription());
        if (valid()) this.selected.set(created.resource.id);
      }
      if (valid()) {
        this.editorDialog().nativeElement.close();
        this.notice.set('知識庫已儲存。');
        await this.load();
      }
    } catch (error) {
      if (valid()) this.editorError.set(this.scope.message(error));
    } finally {
      if (valid()) this.saving.set(false);
    }
  }
  cancel(event: Event) {
    if (this.saving()) event.preventDefault();
  }
  action(id: string) {
    if (id === 'edit') this.openEditor(true);
    if (id === 'access') void this.sharing()?.open();
    if (id === 'delete' && this.current())
      this.confirmDelete(this.current()!.resource.id, this.current()!.resource.name, true);
  }
  async upload(files: FileList | File[]) {
    const collection = this.selected(),
      valid = this.scope.guard(),
      policy = this.policy();
    if (this.uploading() || !collection || !policy || !this.current()?.resource.canEdit) return;
    const candidates = Array.from(files);
    if (candidates.length > 10) {
      this.error.set('每批最多上傳十份文件。');
      return;
    }
    if (
      candidates.some(
        (x) =>
          x.size > policy.maxFileBytes ||
          !policy.extensions.includes('.' + x.name.split('.').pop()?.toLowerCase()),
      )
    ) {
      this.error.set(
        `支援 ${policy.extensions.slice(0, 8).join('、')}，每份最多 ${policy.maxFileBytes / 1048576} MB。`,
      );
      return;
    }
    this.uploading.set(true);
    this.error.set('');
    const controller = new AbortController();
    try {
      for (const file of candidates) {
        if (!valid()) return;
        this.uploadLabel.set(`正在上傳 ${file.name}`);
        const uploaded = await this.uploads.upload(file, controller.signal);
        if (!valid()) return;
        await this.api.add(collection, uploaded.id);
        if (valid() && this.selected() === collection) await this.loadDocuments();
      }
      if (valid()) {
        this.notice.set('文件已上傳。文字辨識與索引會在背景繼續處理。');
        this.collections.set(await this.api.collections());
      }
    } catch (error) {
      if (valid()) this.error.set(this.scope.message(error));
    } finally {
      if (valid()) {
        this.uploading.set(false);
        this.uploadLabel.set('');
      }
    }
  }
  async search(event: Event) {
    event.preventDefault();
    if (this.searching() || !this.query().trim()) return;
    const valid = this.scope.guard(),
      collection = this.selected();
    this.searching.set(true);
    this.error.set('');
    try {
      const result = await this.api.search(this.query(), [collection]);
      if (valid() && this.selected() === collection) this.result.set(result);
    } catch (error) {
      if (valid()) this.error.set(this.scope.message(error));
    } finally {
      if (valid()) this.searching.set(false);
    }
  }
  confirmDelete(id: string, name: string, collection = false) {
    this.deleteTarget.set({ id, name, collection });
    this.editorError.set('');
    this.deleteDialog().nativeElement.showModal();
  }
  async remove() {
    const target = this.deleteTarget(),
      valid = this.scope.guard();
    if (!target || this.saving()) return;
    this.saving.set(true);
    try {
      await (target.collection ? this.api.removeCollection(target.id) : this.api.remove(target.id));
      if (valid()) {
        this.deleteDialog().nativeElement.close();
        this.notice.set('已移除來源。');
        await this.load();
      }
    } catch (error) {
      if (valid()) this.editorError.set(this.scope.message(error));
    } finally {
      if (valid()) this.saving.set(false);
    }
  }
  async reindex(document: DocumentInfo) {
    const valid = this.scope.guard();
    this.error.set('');
    try {
      await this.api.reindex(document.id);
      if (valid()) {
        this.notice.set('已安排重新索引，既有完成頁面會沿用。');
        await this.loadDocuments();
      }
    } catch (error) {
      if (valid()) this.error.set(this.scope.message(error));
    }
  }
  actions(document: DocumentInfo): MenuAction[] {
    return [
      ...(document.textVersion > 0
        ? [
            {
              id: 'text',
              label: '編輯純文字',
              icon: 'edit',
              disabled:
                !document.canEdit || ['queued', 'running', 'processing'].includes(document.status),
            },
          ]
        : []),
      {
        id: 'reindex',
        label: ['failed', 'cancelled'].includes(document.status) ? '重試索引' : '重新索引',
        icon: 'repeat',
        disabled:
          !document.canEdit || ['queued', 'running', 'processing'].includes(document.status),
      },
      { id: 'delete', label: '移除文件', icon: 'trash', danger: true, disabled: !document.canEdit },
    ];
  }
  documentAction(action: string, document: DocumentInfo) {
    if (action === 'text') void this.textEditor().open(this.selected(), document);
    else if (action === 'delete') this.confirmDelete(document.id, document.fileName);
    else if (action === 'reindex') void this.reindex(document);
  }
  textSaved() {
    this.notice.set('文字來源已儲存，索引正在背景更新。');
    void this.load();
  }
  status(value: string) {
    return (
      (
        {
          ready: '可查詢',
          queued: '等待處理',
          running: '處理中',
          processing: '處理中',
          failed: '需要重試',
          cancelled: '已取消',
        } as Record<string, string>
      )[value] || value
    );
  }
}
