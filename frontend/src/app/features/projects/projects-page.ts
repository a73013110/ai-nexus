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
import { ViewScope } from '@shared/browser/view-scope';
import { FileDrop } from '@shared/browser/file-drop';
import { FeaturePage } from '@core/layout/feature-page';
import { Icon } from '@shared/ui/icon';
import { ConfirmDialog } from '@shared/ui/confirm-dialog';
import { ResourceSharing } from '@features/collaboration/resource-sharing';
import { TextTools } from '@features/artifacts/text-tools';
import { ArtifactsApi } from '@features/artifacts/artifacts-api';
import { AttachmentsApi } from '@features/attachments/attachments-api';
import { KnowledgeApi } from '@features/knowledge/knowledge-api';
import { ProjectsApi } from './projects-api';
import { PROJECTS_TOUR, projectTour } from './projects-tour';
import { ConversationDraftTransfer } from '@core/preferences/conversation-draft-transfer';
import { ActionMenu, type MenuAction } from '@shared/ui/action-menu';
import { Panel } from '@shared/ui/panel';
import { ResourceCard } from '@shared/ui/resource-card';
import { ResourceGallery } from '@shared/ui/resource-gallery';
import { StatusBadge } from '@shared/ui/status-badge';
import { relativeTime } from '@shared/browser/format';
import { accessLabel } from '@features/collaboration/access-label';
import { documentBusy, documentStatus } from '@features/knowledge/documents/document-status';

@Component({
  selector: 'nx-projects-page',
  styleUrl: './projects-page.scss',
  imports: [
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
  readonly session = inject(WorkspaceSession);
  private readonly api = inject(ProjectsApi);
  private readonly transfer = inject(ConversationDraftTransfer);
  private readonly attachments = inject(AttachmentsApi);
  private readonly knowledge = inject(KnowledgeApi);
  private readonly artifacts = inject(ArtifactsApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly scope = inject(ViewScope);
  readonly id = toSignal(this.route.paramMap.pipe(map((part) => part.get('id'))), {
    initialValue: null,
  });
  private readonly projectsRead = apiResource({
    feature: 'projects',
    loader: () => this.api.list(),
  });
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
  readonly projects = computed<ProjectDto[]>(() => this.projectsRead.value() ?? []);
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
  readonly loading = computed(() => this.projectsRead.loading() || this.currentRead.loading());
  readonly projectsLoading = this.projectsRead.loading;
  /** 具有編輯權限的成員可以變更進行中的專案；已封存的則唯讀。 */
  readonly editable = computed(() => {
    const project = this.current();
    return !!project?.resource.canEdit && !project.isArchived;
  });
  readonly actionError = signal('');
  readonly error = computed(
    () =>
      this.actionError() ||
      this.projectsRead.error() ||
      this.policyRead.error() ||
      this.currentRead.error() ||
      this.detailRead.error(),
  );
  readonly busy = signal(false);
  readonly toast = inject(ToastService);
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
  readonly visible = computed(() =>
    this.projects().filter(
      (item2) =>
        item2.isArchived === this.archived() &&
        item2.resource.name.toLowerCase().includes(this.filter().toLowerCase()),
    ),
  );
  readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  readonly confirm = viewChild.required(ConfirmDialog);
  readonly sharing = viewChild(ResourceSharing);
  readonly tools = viewChild.required(TextTools);
  readonly kind = signal('project');
  readonly name = signal('');
  readonly description = signal('');
  readonly instructions = signal('');
  readonly templateText = signal('');
  private editingId: string | null = null;
  private revision = 0;
  private controller?: AbortController;
  constructor() {
    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe(() => {
      ++this.revision;
      this.controller?.abort();
      this.busy.set(false);
      this.actionError.set('');
    });
    inject(DestroyRef).onDestroy(() => this.controller?.abort());
  }
  private guard() {
    const revision = this.revision,
      valid = this.scope.guard();
    return () => valid() && revision === this.revision;
  }
  refresh() {
    this.detailRead.reload();
  }
  reload() {
    this.actionError.set('');
    this.projectsRead.reload();
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
    if (this.busy()) return;
    const valid = this.guard();
    this.busy.set(true);
    this.actionError.set('');
    try {
      if (this.kind() === 'project') {
        const value = await this.api.save(
          this.editingId,
          this.name(),
          this.description(),
          this.instructions(),
          this.editingId ? (this.current()?.version ?? 1) : 1,
          this.editingId ? (this.current()?.isArchived ?? false) : false,
        );
        if (!valid()) return;
        this.dialog().nativeElement.close();
        this.toast.success('專案設定已儲存');
        this.busy.set(false);
        if (!this.editingId) {
          // 頁面在 /projects 與 /projects/:id 之間保持掛載，所以列表會重新讀取。
          this.projectsRead.reload();
          await this.router.navigate(['/projects', value.resource.id]);
          return;
        }
        this.currentRead.value.set(value);
        this.projectsRead.reload();
      } else {
        const id = this.current()?.resource.id;
        if (!id) return;
        await this.api.saveTemplate(id, this.editingId, this.name(), this.templateText());
        if (!valid()) return;
        this.dialog().nativeElement.close();
        this.refresh();
      }
    } catch (error2) {
      if (valid()) this.actionError.set(this.scope.message(error2));
    } finally {
      if (valid()) this.busy.set(false);
    }
  }
  async start(templateId?: string) {
    const current = this.current(),
      valid = this.guard();
    if (!current || this.busy()) return;
    this.busy.set(true);
    this.actionError.set('');
    try {
      const value = await this.api.start(current.resource.id, templateId);
      if (valid()) {
        this.transfer.put(value.conversation.id, value.prompt);
        await this.router.navigate(['/chat', value.conversation.id]);
      }
    } catch (error3) {
      if (valid()) this.actionError.set(this.scope.message(error3));
    } finally {
      if (valid()) this.busy.set(false);
    }
  }
  async archive() {
    const current = this.current(),
      valid = this.guard();
    if (!current || this.busy()) return;
    this.busy.set(true);
    try {
      const value = await this.api.save(
        current.resource.id,
        current.resource.name,
        current.description,
        current.instructions,
        current.version,
        !current.isArchived,
      );
      if (valid()) {
        this.currentRead.value.set(value);
        this.projectsRead.value.update((all) =>
          all?.map((item) => (item.resource.id === value.resource.id ? value : item)),
        );
        this.toast.success(
          value.isArchived ? '專案已封存' : '專案已還原',
          value.isArchived ? '文件仍可閱讀。' : undefined,
        );
      }
    } catch (error4) {
      if (valid()) this.actionError.set(this.scope.message(error4));
    } finally {
      if (valid()) this.busy.set(false);
    }
  }
  async removeProject() {
    const current = this.current(),
      valid = this.guard();
    if (!current?.resource.isOwner || this.busy()) return;
    if (
      !(await this.confirm().ask({
        title: `刪除「${current.resource.name}」？`,
        message:
          '專案與繼承的成員權限將移除。對話、文件與成果會保留在各自擁有者的工作區；既有的直接分享權限會保留。若只是暫停工作，可選擇封存專案。',
        confirm: '刪除專案',
        danger: true,
      })) ||
      !valid()
    )
      return;
    this.busy.set(true);
    this.actionError.set('');
    try {
      await this.api.remove(current.resource.id);
      if (!valid()) return;
      this.projectsRead.value.update((all) =>
        all?.filter((item) => item.resource.id !== current.resource.id),
      );
      this.projectsRead.reload();
      await this.router.navigate(['/projects']);
    } catch (error5) {
      if (valid()) this.actionError.set(this.scope.message(error5));
    } finally {
      if (valid()) this.busy.set(false);
    }
  }
  chooseFiles(event: Event) {
    const input = event.target as HTMLInputElement;
    if (input.files) void this.upload(Array.from(input.files));
    input.value = '';
  }
  async upload(files: File[]) {
    const current = this.current(),
      policy = this.policyRead.value(),
      valid = this.guard();
    if (!current || !current.resource.canEdit || current.isArchived || this.busy() || !policy)
      return;
    if (files.length > 4 || files.some((file2) => file2.size > policy.maxFileBytes)) {
      this.actionError.set('每批最多 4 份文件，單檔需符合平台容量限制。');
      return;
    }
    this.busy.set(true);
    this.actionError.set('');
    const controller = (this.controller = new AbortController());
    // 一則提示隨批次從上傳進行到完成；其他情況則撤回它。
    const progress = 'project-upload';
    let done = false;
    try {
      for (const file of files) {
        this.toast.show({
          id: progress,
          tone: 'info',
          title: '正在上傳 ' + file.name,
          duration: 0,
        });
        const uploaded = await this.attachments.upload(file, controller.signal);
        if (!valid()) return;
        try {
          await this.api.addFile(current.resource.id, uploaded.id);
        } catch (error6) {
          await this.attachments.removeAttachment(uploaded.id).catch(() => undefined);
          throw error6;
        }
        if (!valid()) return;
      }
      this.toast.show({
        id: progress,
        title: '文件已上傳',
        description: '辨識進度可直接開啟文件查看。',
      });
      done = true;
      this.refresh();
    } catch (error7) {
      if (valid() && !controller.signal.aborted) this.actionError.set(this.scope.message(error7));
    } finally {
      if (!done) this.toast.dismiss(progress);
      if (valid()) this.busy.set(false);
    }
  }
  async remove(kind: string, id: string) {
    const current = this.current(),
      valid = this.guard();
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
    this.busy.set(true);
    try {
      if (kind === 'file') await this.knowledge.remove(id);
      else await this.api.removeTemplate(current.resource.id, id);
      if (valid()) this.refresh();
    } catch (error8) {
      if (valid()) this.actionError.set(this.scope.message(error8));
    } finally {
      if (valid()) this.busy.set(false);
    }
  }
}
