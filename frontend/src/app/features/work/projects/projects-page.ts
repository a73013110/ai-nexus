import { endpointTable, isPaged, textFilter } from '@shared/ui/table-model';
import { TablePagination } from '@shared/ui/data-table';
import { map } from 'rxjs';
import { apiResource } from '@core/api/api-resource';
import { Notice } from '@shared/ui/notice';
import { ToastService } from '@shared/ui/toast-service';
import { EmptyState } from '@shared/ui/empty-state';
import { DialogHeader } from '@shared/ui/dialog-header';
import { CompactDialog } from '@shared/ui/compact-dialog';
import { Field } from '@shared/ui/field';
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
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ReaderLink } from '@shared/browser/reader-link';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { WorkspaceSession } from '@core/auth/workspace-session';
import type {
  ArtifactSummaryDto,
  ConversationDto,
  DocumentDto,
  ProjectDto,
  ProjectTemplateDto,
} from '@core/api/schema';
import { action } from '@shared/browser/action';
import { ViewScope } from '@shared/browser/view-scope';
import { FileDrop } from '@shared/browser/file-drop';
import { FeaturePage } from '@core/layout/feature-page';
import { Icon } from '@shared/ui/icon';
import { ConfirmDialog } from '@shared/ui/confirm-dialog';
import { ResourceSharing } from '@features/work/collaboration/resource-sharing';
import { TextTools } from '@features/work/artifacts/text-tools';
import { ArtifactsApi } from '@features/work/artifacts/artifacts-api';
import { acceptsFile, FilesApi } from '@features/work/files/files-api';
import { KnowledgeApi } from '@features/work/knowledge/knowledge-api';
import { ProjectsApi } from './projects-api';
import { PROJECTS_TOUR, projectTour } from './projects-tour';
import { ConversationDraftTransfer } from '@core/preferences/conversation-draft-transfer';
import { PendingButton } from '@shared/ui/pending-button';
import { ActionMenu, type MenuAction } from '@shared/ui/action-menu';
import { Panel } from '@shared/ui/panel';
import { ResourceCard } from '@shared/ui/resource-card';
import { ResourceGallery } from '@shared/ui/resource-gallery';
import { StatusBadge } from '@shared/ui/status-badge';
import { relativeTime } from '@shared/browser/format';
import { accessLabel } from '@features/work/collaboration/access-label';
import { documentBusy, documentStatus } from '@features/work/knowledge/documents/document-status';
import { routeOf } from '@core/api/catalog';

@Component({
  selector: 'nx-projects-page',
  styleUrl: './projects-page.scss',
  imports: [
    TablePagination,
    DialogHeader,
    Notice,
    EmptyState,
    CompactDialog,
    Field,
    FeaturePage,
    Icon,
    RouterLink,
    ReaderLink,
    FileDrop,
    ConfirmDialog,
    ResourceSharing,
    TextTools,
    ActionMenu,
    PendingButton,
    Panel,
    ResourceCard,
    ResourceGallery,
    StatusBadge,
  ],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './projects-page.html',
})
export class ProjectsPage {
  /** 範本裡的連結由功能目錄取得網址。 */
  readonly routeOf = routeOf;
  readonly session = inject(WorkspaceSession);
  private readonly api = inject(ProjectsApi);
  private readonly transfer = inject(ConversationDraftTransfer);
  private readonly attachments = inject(FilesApi);
  private readonly knowledge = inject(KnowledgeApi);
  private readonly artifacts = inject(ArtifactsApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  readonly id = toSignal(this.route.paramMap.pipe(map((part) => part.get('id'))), {
    initialValue: null,
  });
  /** 卡片格線一頁 40 張；資料在門檻內時搜尋與換頁都在本機。 */
  readonly table = endpointTable({
    feature: 'projects',
    params: computed(() => ({ archived: this.archived() })),
    load: ({ archived }, table, signal) => this.api.list({ archived, ...table }, signal),
    columns: [
      { id: 'name', label: '名稱', filter: 'text', value: (row) => row.resource.name },
      {
        id: 'updatedAt',
        label: '更新時間',
        sort: 'desc',
        time: true,
        value: (row) => row.resource.updatedAt,
      },
    ],
    sort: { column: 'updatedAt', direction: 'desc' },
    pageSize: 40,
    pageSizes: [40],
  });
  readonly paged = computed(() => isPaged(this.table));
  private readonly policyRead = apiResource({
    feature: 'projects',
    loader: () => this.attachments.policy(),
  });
  private readonly currentRead = apiResource({
    feature: 'projects',
    params: () => this.id() || undefined,
    loader: (id) => this.api.get(id),
  });
  /** 已開啟專案的檔案、範本與相關工作；檔案處理中會重新讀取。 */
  private readonly detailRead = apiResource({
    feature: 'projects',
    params: () => this.id() || undefined,
    loader: async (id) => {
      const [files, templates, conversations, outputs] = await Promise.all([
        this.api.files(id),
        this.api.templates(id),
        this.session.has('chat') ? this.api.conversations(id) : Promise.resolve([]),
        this.session.has('artifacts') ? this.artifacts.list() : Promise.resolve([]),
      ]);
      return {
        id,
        files,
        templates,
        conversations,
        outputs: outputs.filter((item) => item.projectId === id),
      };
    },
    poll: (detail) => (detail?.files.some((item) => documentBusy(item.status)) ? 2000 : null),
  });
  /** 所要求的專案載入或失敗時，絕不顯示另一個專案的資料。 */
  readonly current = computed<ProjectDto | null>(() => {
    const value = this.currentRead.value();
    return value && value.resource.id === this.id() ? value : null;
  });
  private readonly detail = computed(() => {
    const value = this.detailRead.value();
    return value && value.id === this.id() ? value : null;
  });
  readonly files = computed<DocumentDto[]>(() => this.detail()?.files ?? []);
  readonly templates = computed<ProjectTemplateDto[]>(() => this.detail()?.templates ?? []);
  readonly conversations = computed<ConversationDto[]>(() => this.detail()?.conversations ?? []);
  readonly outputs = computed<ArtifactSummaryDto[]>(() => this.detail()?.outputs ?? []);
  readonly loading = computed(() => this.table.loading() || this.currentRead.loading());
  readonly projectsLoading = this.table.loading;
  /** 具有編輯權限的成員可以變更進行中的專案；已封存的則唯讀。 */
  readonly editable = computed(() => {
    const project = this.current();
    return !!project?.resource.canEdit && !project.isArchived;
  });
  readonly error = computed(
    () =>
      this.working.error() ||
      this.table.error() ||
      this.policyRead.error() ||
      this.currentRead.error() ||
      this.detailRead.error(),
  );
  /**
   * 進行中的動作（`save`、`start:<範本 id>`、`archive`、`delete`、`upload`、`remove:<id>`）：
   * 按下的那顆按鈕轉圈，其他寫入等它完成。
   */
  readonly working = action();
  readonly busy = this.working.pending;
  /** 使用者看得到的重讀（重試、寫入後重讀）；處理中文件的輪詢不算。 */
  readonly refreshing = computed(
    () => this.table.refreshing() || this.currentRead.refreshing() || this.detailRead.refreshing(),
  );
  readonly projectsRefreshing = this.table.refreshing;
  /** 詳情（範本、對話、文件、成果）還沒讀到：各面板畫骨架、不顯示 0 與空狀態。 */
  readonly detailLoading = computed(() => !this.detail() && this.detailRead.refreshing());
  /** 已有詳情時重讀（新增、移除、上傳後）：面板內容保留並淡化。 */
  readonly detailRefreshing = computed(() => !!this.detail() && this.detailRead.refreshing());
  private readonly toast = inject(ToastService);
  readonly filter = signal('');
  readonly archived = signal(false);
  readonly scopes = [
    { value: 'active', label: '進行中' },
    { value: 'archived', label: '已封存' },
  ];
  readonly access = accessLabel;
  readonly when = relativeTime;
  readonly status = documentStatus;
  readonly projectActions = computed<MenuAction[]>(() => {
    const project = this.current();
    if (!project) return [];
    return [
      ...(project.resource.canEdit
        ? [
            { id: 'edit', label: '編輯專案', icon: 'edit', disabled: this.busy() },
            {
              id: 'archive',
              label: project.isArchived ? '還原專案' : '封存專案',
              icon: project.isArchived ? 'restore' : 'archive',
              disabled: this.busy(),
            },
          ]
        : []),
      ...(project.resource.isOwner
        ? [
            { id: 'access', label: '成員與權限', icon: 'lock' },
            { id: 'delete', label: '刪除專案', icon: 'trash', danger: true, disabled: this.busy() },
          ]
        : []),
    ];
  });
  /** 集合的導覽，或已開啟專案的導覽（依此檢視者在該處可用的功能決定）。 */
  readonly tour = computed(() =>
    this.id()
      ? projectTour({
          chat: this.session.has('chat'),
          artifacts: this.session.has('artifacts'),
          manage: this.projectActions().length > 0,
        })
      : PROJECTS_TOUR,
  );
  readonly visible = this.table.rows;
  search(text: string) {
    this.filter.set(text);
    this.table.setFilter('name', textFilter(text));
  }
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  private readonly confirm = viewChild.required(ConfirmDialog);
  private readonly sharing = viewChild(ResourceSharing);
  readonly tools = viewChild.required(TextTools);
  readonly kind = signal<'project' | 'template'>('project');
  readonly name = signal('');
  readonly description = signal('');
  readonly instructions = signal('');
  readonly templateText = signal('');
  private editingId: string | null = null;
  constructor() {
    // 列表與 /:id 共用同一個元件，切換專案時元件不會銷毀，ViewScope 擋不住前一個專案晚到的寫入結果；
    // 換路由就放棄送出中的動作（中止上傳）並解除 busy。
    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe(() => this.working.reset());
    inject(DestroyRef).onDestroy(() => this.working.cancel('upload'));
  }
  private refresh() {
    this.detailRead.reload();
  }
  reload() {
    this.working.error.set('');
    this.table.reload();
    this.currentRead.reload();
    this.detailRead.reload();
  }
  projectAction(action: string) {
    const project = this.current();
    if (!project) return;
    if (action === 'edit') this.openProject(project);
    else if (action === 'archive') void this.archive();
    else if (action === 'access') void this.sharing()?.open();
    else if (action === 'delete') void this.removeProject();
  }

  openProject(value: ProjectDto | null = null) {
    if (this.busy()) return;
    this.kind.set('project');
    this.editingId = value?.resource.id ?? null;
    this.name.set(value?.resource.name ?? '');
    this.description.set(value?.description ?? '');
    this.instructions.set(value?.instructions ?? '');
    this.dialog().nativeElement.showModal();
  }
  openTemplate(value: ProjectTemplateDto | null = null) {
    if (this.busy()) return;
    this.kind.set('template');
    this.editingId = value?.id ?? null;
    this.name.set(value?.title ?? '');
    this.templateText.set(value?.content ?? '');
    this.dialog().nativeElement.showModal();
  }
  close(event?: Event) {
    event?.preventDefault();
    if (!this.busy()) this.dialog().nativeElement.close();
  }
  async save(event: Event) {
    event.preventDefault();
    if (this.kind() === 'project') await this.saveProject();
    else await this.saveTemplate();
  }
  private async saveProject() {
    const editingId = this.editingId,
      current = this.current();
    const outcome = await this.working.run(
      () =>
        this.api.save(
          editingId,
          this.name(),
          this.description(),
          this.instructions(),
          editingId ? (current?.version ?? 1) : 1,
          editingId ? (current?.isArchived ?? false) : false,
        ),
      { key: 'save' },
    );
    if (!outcome.ok) return;
    const value = outcome.value;
    this.dialog().nativeElement.close();
    this.toast.success('專案設定已儲存');
    // 頁面在 /projects 與 /projects/:id 之間保持掛載，所以列表會重新讀取。
    this.table.reload();
    if (editingId) this.currentRead.value.set(value);
    else await this.router.navigate([routeOf('projects'), value.resource.id]);
  }
  private async saveTemplate() {
    const id = this.current()?.resource.id;
    if (!id) return;
    const outcome = await this.working.run(
      () => this.api.saveTemplate(id, this.editingId, this.name(), this.templateText()),
      { key: 'save' },
    );
    if (!outcome.ok) return;
    this.dialog().nativeElement.close();
    this.refresh();
  }
  async start(templateId?: string) {
    const current = this.current();
    if (!current) return;
    const outcome = await this.working.run(() => this.api.start(current.resource.id, templateId), {
      key: 'start:' + (templateId ?? ''),
    });
    if (!outcome.ok) return;
    this.transfer.put(outcome.value.conversation.id, outcome.value.prompt);
    await this.router.navigate([routeOf('chat'), outcome.value.conversation.id]);
  }
  async archive() {
    const current = this.current();
    if (!current) return;
    const outcome = await this.working.run(
      () =>
        this.api.save(
          current.resource.id,
          current.resource.name,
          current.description,
          current.instructions,
          current.version,
          !current.isArchived,
        ),
      { key: 'archive' },
    );
    if (!outcome.ok) return;
    const value = outcome.value;
    this.currentRead.value.set(value);
    this.table.reload();
    this.toast.success(
      value.isArchived ? '專案已封存' : '專案已還原',
      value.isArchived ? '文件仍可閱讀。' : undefined,
    );
  }
  async removeProject() {
    const current = this.current();
    if (!current?.resource.isOwner || this.busy()) return;
    if (
      !(await this.confirm().ask({
        title: `刪除「${current.resource.name}」？`,
        message:
          '專案與繼承的成員權限將移除。對話、文件與成果會保留在各自擁有者的工作區。既有的直接分享權限會保留。若只是暫停工作，可選擇封存專案。',
        confirm: '刪除專案',
        danger: true,
      })) ||
      // 確認期間換到另一個專案就不刪。
      this.current()?.resource.id !== current.resource.id
    )
      return;
    const outcome = await this.working.run(() => this.api.remove(current.resource.id), {
      key: 'delete',
    });
    if (!outcome.ok) return;
    this.table.dropRows((item) => item.resource.id === current.resource.id);
    this.table.reload();
    await this.router.navigate([routeOf('projects')]);
  }
  chooseFiles(event: Event) {
    const input = event.target as HTMLInputElement;
    if (input.files) void this.upload(Array.from(input.files));
    input.value = '';
  }
  async upload(files: File[]) {
    const current = this.current(),
      policy = this.policyRead.value();
    if (!current || !current.resource.canEdit || current.isArchived || this.busy() || !policy)
      return;
    if (files.length > 4 || files.some((file) => !acceptsFile(policy, file))) {
      this.working.fail('每批最多 4 份文件，單檔需符合平台格式與容量限制。');
      return;
    }
    // 一則提示隨批次從上傳進行到完成；其他情況則撤回它。
    const progress = 'project-upload';
    const outcome = await this.working.run(
      async ({ alive, signal }) => {
        for (const file of files) {
          this.toast.show({
            id: progress,
            tone: 'info',
            title: '正在上傳 ' + file.name,
            duration: 0,
          });
          const uploaded = await this.attachments.upload(file, signal);
          if (!alive()) return;
          try {
            await this.api.addFile(current.resource.id, uploaded.id);
          } catch (error) {
            // 沒加進專案的附件不留著佔空間。
            await this.attachments.removeAttachment(uploaded.id).catch(() => undefined);
            throw error;
          }
          if (!alive()) return;
        }
      },
      { key: 'upload' },
    );
    if (!outcome.ok) {
      this.toast.dismiss(progress);
      return;
    }
    this.toast.show({
      id: progress,
      title: '文件已上傳',
      description: '辨識進度可直接開啟文件查看。',
    });
    this.refresh();
  }
  async remove(kind: 'file' | 'template', id: string) {
    const current = this.current();
    if (
      !current ||
      this.busy() ||
      !(await this.confirm().ask({
        title: kind === 'file' ? '移除專案文件' : '移除專案範本',
        message: '移除後，專案成員將無法再使用此項目。既有對話不受影響。',
        confirm: '移除',
        danger: true,
      }))
    )
      return;
    const outcome = await this.working.run(
      () =>
        kind === 'file'
          ? this.knowledge.remove(id)
          : this.api.removeTemplate(current.resource.id, id),
      { key: 'remove:' + id },
    );
    if (outcome.ok) this.refresh();
  }
}
