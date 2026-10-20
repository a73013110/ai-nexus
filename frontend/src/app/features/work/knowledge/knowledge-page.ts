import { map } from 'rxjs';
import { apiResource } from '@core/api/api-resource';
import { Notice } from '@shared/ui/notice';
import { ToastService } from '@shared/ui/toast-service';
import { StatusBadge } from '@shared/ui/status-badge';
import { EmptyState } from '@shared/ui/empty-state';
import { SearchField } from '@shared/ui/search-field';
import { DialogHeader } from '@shared/ui/dialog-header';
import { CompactDialog } from '@shared/ui/compact-dialog';
import { ConfirmDialog } from '@shared/ui/confirm-dialog';
import { Field } from '@shared/ui/field';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  effect,
  inject,
  linkedSignal,
  signal,
  viewChild,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import type {
  AttachmentPolicyDto,
  CollectionDto,
  DocumentDto,
  KnowledgeSearchDto,
  LibraryFileDto,
} from '@core/api/schema';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { ViewScope } from '@shared/browser/view-scope';
import { FeaturePage } from '@core/layout/feature-page';
import { Icon } from '@shared/ui/icon';
import { ActionMenu, type MenuAction } from '@shared/ui/action-menu';
import { Panel } from '@shared/ui/panel';
import { ResourceCard } from '@shared/ui/resource-card';
import { ResourceGallery } from '@shared/ui/resource-gallery';
import { ResourceSharing } from '@features/work/collaboration/resource-sharing';
import { accessLabel } from '@features/work/collaboration/access-label';
import { FileDrop } from '@shared/browser/file-drop';
import { acceptsFile, FilesApi } from '@features/work/files/files-api';
import { KnowledgeApi } from './knowledge-api';
import { LibraryPicker } from '@features/work/files/file-library/library-picker';
import { TextSourceEditor } from './documents/text-source-editor';
import { ReaderLink } from '@shared/browser/reader-link';
import { RetrievalResults } from './retrieval/retrieval-results';
import { JobToasts } from '@features/work/jobs/job-toasts';
import { documentBusy, documentStatus } from './documents/document-status';
import { KNOWLEDGE_TOUR, collectionTour } from './knowledge-tour';
import { routeOf } from '@core/api/catalog';

/** 模組網址列出集合；`/:id` 則是單一集合的來源與查詢。 */
@Component({
  selector: 'nx-knowledge-page',
  imports: [
    Notice,
    StatusBadge,
    EmptyState,
    SearchField,
    CompactDialog,
    DialogHeader,
    ConfirmDialog,
    Field,
    FeaturePage,
    Icon,
    ActionMenu,
    Panel,
    ResourceCard,
    ResourceGallery,
    ResourceSharing,
    RouterLink,
    FileDrop,
    LibraryPicker,
    ReaderLink,
    RetrievalResults,
    TextSourceEditor,
  ],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './knowledge-page.scss',
  templateUrl: './knowledge-page.html',
})
export class KnowledgePage {
  /** 範本裡的連結由功能目錄取得網址。 */
  readonly routeOf = routeOf;
  private readonly api = inject(KnowledgeApi);
  private readonly uploads = inject(FilesApi);
  private readonly scope = inject(ViewScope);
  private readonly router = inject(Router);
  readonly session = inject(WorkspaceSession);
  readonly id = toSignal(inject(ActivatedRoute).paramMap.pipe(map((part) => part.get('id'))), {
    initialValue: null,
  });
  private readonly collectionsRead = apiResource({
    feature: 'knowledge',
    loader: () => this.api.collections(),
  });
  private readonly policyRead = apiResource({
    feature: 'knowledge',
    loader: () => this.uploads.policy(),
  });
  private readonly collections = computed<CollectionDto[]>(
    () => this.collectionsRead.value() ?? [],
  );
  readonly current = computed(() =>
    this.collections().find((item) => item.resource.id === this.id()),
  );
  /** 只要任何文件仍在處理中，就會重新讀取文件。 */
  private readonly documentsRead = apiResource({
    feature: 'knowledge',
    params: () => this.id() || undefined,
    loader: async (id) => ({ id, rows: await this.api.documents(id) }),
    poll: (value) => (value?.rows.some((item) => documentBusy(item.status)) ? 2500 : null),
  });
  readonly documents = computed<DocumentDto[]>(() => {
    const value = this.documentsRead.value();
    return value && value.id === this.id() ? value.rows : [];
  });
  readonly policy = computed<AttachmentPolicyDto | null>(() => this.policyRead.value() ?? null);
  readonly loading = this.collectionsRead.loading;
  readonly loadingDocuments = this.documentsRead.loading;
  private readonly actionError = signal('');
  readonly error = computed(
    () =>
      this.actionError() ||
      this.collectionsRead.error() ||
      this.policyRead.error() ||
      this.documentsRead.error(),
  );
  readonly toast = inject(ToastService);
  private readonly jobToasts = inject(JobToasts);
  readonly access = accessLabel;
  readonly status = documentStatus;
  readonly term = signal('');
  readonly ownership = signal('all');
  readonly ownerships = [
    { value: 'all', label: '全部' },
    { value: 'mine', label: '我擁有' },
    { value: 'shared', label: '共用給我' },
  ];
  readonly gallery = computed(() => {
    const term = this.term().toLowerCase(),
      ownership = this.ownership();
    return this.collections().filter(
      (item) =>
        (ownership === 'all' || item.resource.isOwner === (ownership === 'mine')) &&
        item.resource.name.toLowerCase().includes(term),
    );
  });
  readonly uploading = signal(false);
  readonly uploadLabel = signal('');
  // 篩選、查詢與其結果，都屬於畫面上的那個集合。
  readonly filter = linkedSignal(() => (this.id(), ''));
  readonly query = linkedSignal(() => (this.id(), ''));
  readonly result = linkedSignal<KnowledgeSearchDto | null>(() => (this.id(), null));
  readonly searching = signal(false);
  readonly editorName = signal('');
  readonly editorDescription = signal('');
  readonly editorId = signal<string | null>(null);
  readonly editorError = signal('');
  readonly saving = signal(false);
  private readonly editorDialog = viewChild.required<ElementRef<HTMLDialogElement>>('editorDialog');
  private readonly confirm = viewChild.required(ConfirmDialog);
  readonly textEditor = viewChild.required(TextSourceEditor);
  private readonly sharing = viewChild(ResourceSharing);
  /** 集合的導覽，或已開啟集合的導覽（依此檢視者在該處可以變更的內容決定）。 */
  readonly tour = computed(() => {
    const resource = this.current()?.resource;
    return this.id()
      ? collectionTour({
          edit: !!resource?.canEdit,
          manage: !!(resource?.canEdit || resource?.isOwner),
        })
      : KNOWLEDGE_TOUR;
  });
  readonly visible = computed(() =>
    this.documents().filter((item) =>
      item.fileName.toLowerCase().includes(this.filter().toLowerCase()),
    ),
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
  constructor() {
    effect(() => this.documents().forEach((item) => this.jobToasts.observeDocument(item)));
  }
  /** 重新讀取集合與已開啟集合的文件。 */
  load() {
    this.actionError.set('');
    this.collectionsRead.reload();
    this.documentsRead.reload();
  }
  async addLibrary(file: LibraryFileDto) {
    const collection = this.id(),
      valid = this.scope.guard();
    if (!collection || this.uploading() || !this.current()?.resource.canEdit) return;
    this.uploading.set(true);
    this.actionError.set('');
    try {
      this.jobToasts.trackDocument(await this.api.add(collection, file.file.id));
      if (valid()) {
        this.toast.success('已從檔案庫加入來源，索引會在背景建立');
        this.load();
      }
    } catch (error) {
      if (valid()) this.actionError.set(this.scope.message(error));
    } finally {
      if (valid()) this.uploading.set(false);
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
      const id = this.editorId();
      if (id) await this.api.update(id, this.editorName(), this.editorDescription());
      else {
        const created = await this.api.create(this.editorName(), this.editorDescription());
        if (valid()) void this.router.navigate([routeOf('knowledge'), created.resource.id]);
      }
      if (valid()) {
        this.editorDialog().nativeElement.close();
        this.toast.success('知識庫已儲存');
        this.load();
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
    const current = this.current();
    if (!current) return;
    if (id === 'edit') this.openEditor(true);
    if (id === 'access') void this.sharing()?.open();
    if (id === 'delete') void this.remove(current.resource.id, current.resource.name, true);
  }
  chooseFiles(picker: HTMLInputElement) {
    void this.upload(picker.files!);
    picker.value = '';
  }
  async upload(files: FileList | File[]) {
    const collection = this.id(),
      valid = this.scope.guard(),
      policy = this.policy();
    if (this.uploading() || !collection || !policy || !this.current()?.resource.canEdit) return;
    const candidates = Array.from(files);
    if (candidates.length > 10) {
      this.actionError.set('每批最多上傳十份文件。');
      return;
    }
    if (candidates.some((file) => !acceptsFile(policy, file))) {
      this.actionError.set(
        `支援 ${policy.extensions.slice(0, 8).join('、')}，每份最多 ${policy.maxFileBytes / 1048576} MB。`,
      );
      return;
    }
    this.uploading.set(true);
    this.actionError.set('');
    const controller = new AbortController();
    try {
      for (const file of candidates) {
        if (!valid()) return;
        this.uploadLabel.set(`正在上傳 ${file.name}`);
        const uploaded = await this.uploads.upload(file, controller.signal);
        if (!valid()) return;
        this.jobToasts.trackDocument(await this.api.add(collection, uploaded.id));
        if (valid() && this.id() === collection) this.documentsRead.reload();
      }
      if (valid()) {
        this.toast.success('文件已上傳', '文字辨識與索引會在背景繼續處理。');
        this.collectionsRead.reload();
      }
    } catch (error) {
      if (valid()) this.actionError.set(this.scope.message(error));
    } finally {
      if (valid()) {
        this.uploading.set(false);
        this.uploadLabel.set('');
      }
    }
  }
  async search(event: Event) {
    event.preventDefault();
    const collection = this.id();
    if (this.searching() || !this.query().trim() || !collection) return;
    const valid = this.scope.guard();
    this.searching.set(true);
    this.actionError.set('');
    try {
      const result = await this.api.search(this.query(), [collection]);
      if (valid() && this.id() === collection) this.result.set(result);
    } catch (error) {
      if (valid()) this.actionError.set(this.scope.message(error));
    } finally {
      if (valid()) this.searching.set(false);
    }
  }
  /** 確認後移除來源或整個集合；原始檔案仍保留在檔案庫中。 */
  private async remove(id: string, name: string, collection = false) {
    const valid = this.scope.guard();
    if (
      !(await this.confirm().ask({
        title: collection ? '移除知識庫' : '移除文件',
        message: `將移除「${name}」${collection ? '與其中所有文件' : ''}的來源與索引。原檔仍保留在檔案庫；已生成的對話文字會保留，來源引用連結將失效。`,
        confirm: '移除來源',
        danger: true,
      })) ||
      !valid()
    )
      return;
    this.actionError.set('');
    try {
      await (collection ? this.api.removeCollection(id) : this.api.remove(id));
      if (!valid()) return;
      this.toast.success('已移除來源');
      if (collection) void this.router.navigate([routeOf('knowledge')]);
      this.load();
    } catch (error) {
      if (valid()) this.actionError.set(this.scope.message(error));
    }
  }
  private async reindex(document: DocumentDto) {
    const valid = this.scope.guard();
    this.actionError.set('');
    try {
      this.jobToasts.trackDocument(await this.api.reindex(document.id));
      if (valid()) {
        this.toast.success('已安排重新索引，既有完成頁面會沿用');
        this.documentsRead.reload();
      }
    } catch (error) {
      if (valid()) this.actionError.set(this.scope.message(error));
    }
  }
  actions(document: DocumentDto): MenuAction[] {
    const busy = documentBusy(document.status);
    return [
      ...(document.textVersion > 0
        ? [{ id: 'text', label: '編輯純文字', icon: 'edit', disabled: !document.canEdit || busy }]
        : []),
      {
        id: 'reindex',
        label: ['failed', 'cancelled'].includes(document.status) ? '重試索引' : '重新索引',
        icon: 'repeat',
        disabled: !document.canEdit || busy,
      },
      { id: 'delete', label: '移除文件', icon: 'trash', danger: true, disabled: !document.canEdit },
    ];
  }
  documentAction(action: string, document: DocumentDto) {
    const collection = this.id();
    if (action === 'text' && collection) void this.textEditor().open(collection, document);
    else if (action === 'delete') void this.remove(document.id, document.fileName);
    else if (action === 'reindex') void this.reindex(document);
  }
  textSaved() {
    this.toast.success('文字來源已儲存，索引正在背景更新');
    this.load();
  }
}
