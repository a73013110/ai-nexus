import { map } from 'rxjs';
import { apiResource } from '../../core/api/api-resource';
import { Notice } from '../../shared/ui/notice';
import { EmptyState } from '../../shared/ui/empty-state';
import { Card } from '../../shared/ui/card';
import { CompactDialog } from '../../shared/ui/compact-dialog';
import { Field } from '../../shared/ui/field';
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
import { ReaderLink } from '../../shared/browser/reader-link';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { WorkspaceSession } from '../../core/auth/workspace-session';
import type {
  ArtifactSummaryDto,
  ConversationDto,
  DocumentDto,
  ProjectDto,
  ProjectTemplateDto,
} from '../../core/api/schema';
import { ViewScope } from '../../shared/browser/view-scope';
import { FileDrop } from '../../shared/browser/file-drop';
import { FeaturePage } from '../../core/layout/feature-page';
import { Icon } from '../../shared/ui/icon';
import { ConfirmDialog } from '../../shared/ui/confirm-dialog';
import { ResourceSharing } from '../sharing/resource-sharing';
import { TextTools } from '../artifacts/text-tools';
import { ArtifactsApi } from '../artifacts/artifacts-api';
import { WorkspaceApi } from '../workspace/workspace-api';
import { KnowledgeApi } from '../knowledge/knowledge-api';
import { ProjectsApi } from './projects-api';
import { ConversationDraftTransfer } from '../../core/preferences/conversation-draft-transfer';
import { SearchField } from '../../shared/ui/search-field';

@Component({
  selector: 'nx-projects-page',
  styleUrl: './projects-page.scss',
  imports: [
    Notice,
    EmptyState,
    Card,
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
    SearchField,
  ],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './projects-page.html',
})
export class ProjectsPage {
  readonly session = inject(WorkspaceSession);
  private readonly api = inject(ProjectsApi);
  private readonly transfer = inject(ConversationDraftTransfer);
  private readonly workspace = inject(WorkspaceApi);
  private readonly knowledge = inject(KnowledgeApi);
  private readonly artifacts = inject(ArtifactsApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly scope = inject(ViewScope);
  private readonly id = toSignal(this.route.paramMap.pipe(map((p) => p.get('id'))), {
    initialValue: null,
  });
  private readonly projectsRead = apiResource({
    feature: 'projects',
    loader: () => this.api.list(),
  });
  private readonly policyRead = apiResource({
    feature: 'projects',
    loader: () => this.workspace.attachmentPolicy(),
  });
  private readonly currentRead = apiResource({
    feature: 'projects',
    params: () => this.id() || undefined,
    loader: (id) => this.api.get(id),
  });
  /** Files, templates and related work of the open project; re-read while files are processed. */
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
        outputs: outputs.filter((x) => x.projectId === id),
      };
    },
    poll: (detail) =>
      detail?.files.some((x) => ['queued', 'running'].includes(x.status)) ? 2000 : null,
  });
  readonly projects = computed<ProjectDto[]>(() => this.projectsRead.value() ?? []);
  /** Another project's data is never shown while the requested one loads or fails. */
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
  readonly notice = signal('');
  readonly filter = signal('');
  readonly archived = signal(false);
  readonly visible = computed(() =>
    this.projects().filter(
      (x) =>
        x.isArchived === this.archived() &&
        x.resource.name.toLowerCase().includes(this.filter().toLowerCase()),
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
      this.notice.set('');
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
        this.notice.set('專案設定已儲存。');
        this.busy.set(false);
        if (!this.editingId) {
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
    } catch (e) {
      if (valid()) this.actionError.set(this.scope.message(e));
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
    } catch (e) {
      if (valid()) this.actionError.set(this.scope.message(e));
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
          all?.map((x) => (x.resource.id === value.resource.id ? value : x)),
        );
        this.notice.set(value.isArchived ? '專案已封存，文件仍可閱讀。' : '專案已還原。');
      }
    } catch (e) {
      if (valid()) this.actionError.set(this.scope.message(e));
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
      if (valid()) await this.router.navigate(['/projects']);
    } catch (e) {
      if (valid()) this.actionError.set(this.scope.message(e));
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
    if (files.length > 4 || files.some((f) => f.size > policy.maxFileBytes)) {
      this.actionError.set('每批最多 4 份文件，單檔需符合平台容量限制。');
      return;
    }
    this.busy.set(true);
    this.actionError.set('');
    const controller = (this.controller = new AbortController());
    try {
      for (const file of files) {
        this.notice.set('正在上傳 ' + file.name);
        const uploaded = await this.workspace.upload(file, controller.signal);
        if (!valid()) return;
        try {
          await this.api.addFile(current.resource.id, uploaded.id);
        } catch (e) {
          await this.workspace.removeAttachment(uploaded.id).catch(() => undefined);
          throw e;
        }
        if (!valid()) return;
      }
      this.notice.set('文件已上傳，辨識進度可直接開啟文件查看。');
      this.refresh();
    } catch (e) {
      if (valid() && !controller.signal.aborted) this.actionError.set(this.scope.message(e));
    } finally {
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
    } catch (e) {
      if (valid()) this.actionError.set(this.scope.message(e));
    } finally {
      if (valid()) this.busy.set(false);
    }
  }
}
