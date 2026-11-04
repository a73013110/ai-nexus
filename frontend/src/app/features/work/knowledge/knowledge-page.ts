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
import { action } from '@shared/browser/action';
import { ViewScope } from '@shared/browser/view-scope';
import { FeaturePage } from '@core/layout/feature-page';
import { Icon } from '@shared/ui/icon';
import { PendingButton } from '@shared/ui/pending-button';
import { ActionMenu, type MenuAction } from '@shared/ui/action-menu';
import { Panel } from '@shared/ui/panel';
import { ResourceCard } from '@shared/ui/resource-card';
import { ResourceGallery } from '@shared/ui/resource-gallery';
import { ResourceSharing } from '@features/work/collaboration/resource-sharing';
import {
  accessLabel,
  filterResources,
  OWNERSHIP_FILTERS,
} from '@features/work/collaboration/access-label';
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
    PendingButton,
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
  readonly collectionsRefreshing = this.collectionsRead.refreshing;
  readonly loadingDocuments = this.documentsRead.loading;
  /** 已有文件列時的重讀；輪詢不算（`refreshing` 本來就不含輪詢）。 */
  readonly documentsRefreshing = computed(
    () => this.documentsRead.refreshing() && !this.documentsRead.loading(),
  );
  readonly refreshing = computed(
    () => this.collectionsRead.refreshing() || this.documentsRead.refreshing(),
  );
  /** 移除或重新索引中的知識庫與文件：只有那一列的選單鈕轉圈。 */
  readonly working = action({ concurrent: true });
  readonly uploading = action();
  readonly searching = action();
  readonly saving = action();
  readonly error = computed(
    () =>
      this.working.error() ||
      this.uploading.error() ||
      this.searching.error() ||
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
  readonly ownerships = OWNERSHIP_FILTERS;
  readonly gallery = computed(() =>
    filterResources(this.collections(), this.ownership(), this.term()),
  );
  // 篩選、查詢與其結果，都屬於畫面上的那個集合。
  readonly filter = linkedSignal(() => (this.id(), ''));
  readonly query = linkedSignal(() => (this.id(), ''));
  readonly result = linkedSignal<KnowledgeSearchDto | null>(() => (this.id(), null));
  readonly editorName = signal('');
  readonly editorDescription = signal('');
  readonly editorId = signal<string | null>(null);
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
    for (const write of [this.working, this.uploading, this.searching]) write.error.set('');
    this.collectionsRead.reload();
    this.documentsRead.reload();
  }
  async addLibrary(file: LibraryFileDto) {
    const collection = this.id();
    if (!collection || !this.current()?.resource.canEdit) return;
    const outcome = await this.uploading.run(async () =>
      // 索引在背景建立：已離開頁面也照樣通知。
      this.jobToasts.trackDocument(await this.api.add(collection, file.file.id)),
    );
    if (!outcome.ok) return;
    this.toast.success('已從檔案庫加入來源，索引會在背景建立');
    this.load();
  }
  openEditor(edit = false) {
    const current = edit ? this.current() : null;
    this.editorId.set(current?.resource.id || null);
    this.editorName.set(current?.resource.name || '');
    this.editorDescription.set(current?.description || '');
    this.saving.error.set('');
    this.editorDialog().nativeElement.showModal();
  }
  async save(event: Event) {
    event.preventDefault();
    const id = this.editorId();
    const outcome = await this.saving.run(() =>
      id
        ? this.api.update(id, this.editorName(), this.editorDescription())
        : this.api.create(this.editorName(), this.editorDescription()),
    );
    if (!outcome.ok) return;
    if (!id && outcome.value)
      void this.router.navigate([routeOf('knowledge'), outcome.value.resource.id]);
    this.editorDialog().nativeElement.close();
    this.toast.success('知識庫已儲存');
    this.load();
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
      policy = this.policy();
    if (this.uploading.pending() || !collection || !policy || !this.current()?.resource.canEdit)
      return;
    const candidates = Array.from(files);
    if (candidates.length > 10) {
      this.uploading.fail('每批最多上傳十份文件。');
      return;
    }
    if (candidates.some((file) => !acceptsFile(policy, file))) {
      this.uploading.fail(
        `支援 ${policy.extensions.slice(0, 8).join('、')}，每份最多 ${policy.maxFileBytes / 1048576} MB。`,
      );
      return;
    }
    // 與檔案庫、專案相同：一則提示從「正在上傳」換成完成；失敗或離開則撤回。
    const progress = 'knowledge-upload';
    const outcome = await this.uploading.run(async ({ alive, signal }) => {
      for (const file of candidates) {
        if (!alive()) return;
        this.toast.show({
          id: progress,
          tone: 'info',
          title: '正在上傳 ' + file.name,
          duration: 0,
        });
        const uploaded = await this.uploads.upload(file, signal);
        if (!alive()) return;
        this.jobToasts.trackDocument(await this.api.add(collection, uploaded.id));
        if (alive() && this.id() === collection) this.documentsRead.reload();
      }
    });
    if (!outcome.ok) {
      this.toast.dismiss(progress);
      return;
    }
    this.toast.show({
      id: progress,
      tone: 'success',
      title: '文件已上傳',
      description: '文字辨識與索引會在背景繼續處理。',
    });
    this.collectionsRead.reload();
  }
  async search(event: Event) {
    event.preventDefault();
    const collection = this.id();
    if (!this.query().trim() || !collection) return;
    const outcome = await this.searching.run(() => this.api.search(this.query(), [collection]), {
      current: () => this.id() === collection,
    });
    if (outcome.ok) this.result.set(outcome.value);
  }
  /** 確認後移除來源或整個集合；原始檔案仍保留在檔案庫中。 */
  private async remove(id: string, name: string, collection = false) {
    if (
      !(await this.confirm().ask({
        title: collection ? '移除知識庫' : '移除文件',
        message: `將移除「${name}」${collection ? '與其中所有文件' : ''}的來源與索引。原檔仍保留在檔案庫；已生成的對話文字會保留，來源引用連結將失效。`,
        confirm: '移除來源',
        danger: true,
      }))
    )
      return;
    const outcome = await this.working.run(
      () => (collection ? this.api.removeCollection(id) : this.api.remove(id)),
      { key: id },
    );
    if (!outcome.ok) return;
    this.toast.success('已移除來源');
    if (collection) void this.router.navigate([routeOf('knowledge')]);
    this.load();
  }
  private async reindex(document: DocumentDto) {
    const outcome = await this.working.run(
      async () => this.jobToasts.trackDocument(await this.api.reindex(document.id)),
      { key: document.id },
    );
    if (!outcome.ok) return;
    this.toast.success('已安排重新索引，既有完成頁面會沿用');
    this.documentsRead.reload();
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
