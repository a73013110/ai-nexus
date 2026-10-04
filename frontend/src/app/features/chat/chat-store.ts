import { computed, DestroyRef, effect, inject, Injectable, signal, untracked } from '@angular/core';
import { Router } from '@angular/router';
import { ApiError, NexusApi } from '../../core/api/nexus-api';
import {
  Conversation,
  CreateRun,
  isActive,
  Me,
  Message,
  Model,
  Preferences,
  Run,
  ModelPolicy,
  ContextPreview,
  ContextUsage,
} from '../../core/api/types';
import { ThemeService } from '../../core/preferences/theme-service';
import { RunStream } from '../../core/stream/run-stream';
import { AuthService } from '../../core/auth/auth-service';
import { DraftRepository } from '../../core/preferences/draft-repository';
import { DraftAttachments } from '../attachments/draft-attachments';
import { WorkspaceApi } from '../workspace/workspace-api';
import { MessageTree } from './message-tree';
import type { ConversationBackup, ConversationSettings } from '../../core/api/types';
import { downloadFile } from '../../shared/browser/download';
import { UserSettingsService } from '../../core/preferences/user-settings';
import { WorkspaceSession } from '../../core/auth/workspace-session';
import { KnowledgeSelection } from '../knowledge/knowledge-selection';
import { ProjectsApi } from '../projects/projects-api';

@Injectable({ providedIn: 'root' })
export class ChatStore {
  private readonly projectsApi = inject(ProjectsApi);
  async assignProject(conversation: Conversation, projectId: string | null) {
    if (this.busy()) return false;
    const generation = this.auth.generation();
    try {
      const value = await this.projectsApi.assign(conversation.id, projectId);
      if (generation !== this.auth.generation()) return false;
      if (this.selected()?.id === value.id) this.selected.set(value);
      await this.refreshHistory();
      return true;
    } catch (error) {
      this.report(error);
      return false;
    }
  }
  private readonly api = inject(NexusApi);
  private readonly stream = inject(RunStream);
  private readonly router = inject(Router);
  private readonly themes = inject(ThemeService);
  readonly attachments = inject(DraftAttachments);
  readonly knowledge = inject(KnowledgeSelection);
  readonly drafts = inject(DraftRepository);
  private readonly workspace = inject(WorkspaceApi);
  readonly auth = inject(AuthService);
  readonly personal = inject(UserSettingsService);
  private readonly session = inject(WorkspaceSession);
  readonly me = signal<Me | null>(null);
  readonly conversations = signal<Conversation[]>([]);
  readonly selected = signal<Conversation | null>(null);
  readonly messages = signal<Message[]>([]);
  rateMessage(value: { id: string; rating: number }) {
    this.messages.update((items) =>
      items.map((x) => (x.id === value.id ? { ...x, feedbackRating: value.rating } : x)),
    );
  }
  readonly models = signal<Model[]>([]);
  readonly modelId = signal('');
  readonly policy = signal<ModelPolicy>({
    allowModelSelection: true,
    showModelNames: true,
    defaultModelId: null,
    maxInputCharacters: 12000,
  });
  readonly reasoningEffort = signal('auto');
  readonly contextUsage = signal<ContextUsage | null>(null);
  readonly contextNotice = signal<string | null>(null);
  readonly hasChatAccess = computed(
    () => this.me()?.access?.features?.some((x) => x.id === 'chat') ?? false,
  );
  readonly hasKnowledgeAccess = computed(
    () => this.me()?.access.features?.some((x) => x.id === 'knowledge') ?? false,
  );
  readonly hasArtifactsAccess = computed(
    () => this.me()?.access.features?.some((x) => x.id === 'artifacts') ?? false,
  );
  readonly modelNotice = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  readonly ready = signal(false);
  readonly loading = signal(true);
  readonly loadingConversation = signal(false);
  readonly submitting = signal(false);
  readonly stopping = signal(false);
  readonly liveRun = signal<Run | null>(null);
  readonly streamingText = signal('');
  readonly connection = signal<'connected' | 'reconnecting' | 'disconnected'>('connected');
  readonly pendingSubmission = signal(false);
  readonly hasMore = signal(false);
  readonly search = signal('');
  readonly historyView = signal('active');
  readonly historyLabel = signal('');
  readonly labels = signal<string[]>([]);
  readonly editing = signal<Message | null>(null);
  readonly draft = signal({ text: '' });
  readonly tree = computed(() => new MessageTree(this.messages()));
  readonly visionNotice = computed(() =>
    this.attachments.files().some((x) => x.isImage) &&
    !this.models().find((x) => x.id === this.modelId())?.supportsImages
      ? '目前模型不支援圖片，請切換模型或移除圖片。'
      : '',
  );
  readonly busy = computed(
    () => this.submitting() || this.liveRun() !== null || this.pendingSubmission(),
  );
  readonly canSend = computed(
    () =>
      this.ready() &&
      !this.loading() &&
      this.hasChatAccess() &&
      !!this.modelId() &&
      !this.loadingConversation() &&
      !this.selected()?.isArchived &&
      !this.attachments.uploading() &&
      !this.attachments.files().some((x) => x.analysisMode === 'ocr-required') &&
      !this.knowledge.saving() &&
      !this.visionNotice() &&
      !this.busy() &&
      !this.contextUsage()?.budgetExceeded,
  );
  readonly canUpload = computed(
    () =>
      this.ready() &&
      this.hasChatAccess() &&
      !this.loading() &&
      !this.loadingConversation() &&
      !this.selected()?.isArchived &&
      !this.busy() &&
      !this.attachments.uploading() &&
      !!this.attachments.policy(),
  );
  readonly visibleMessages = computed(() => this.tree().branch(this.selected()?.activeLeafId));
  private initialized: Promise<void> | null = null;
  private subscription: AbortController | null = null;
  private selectionVersion = 0;
  private searchVersion = 0;
  private pending: {
    body: CreateRun;
    key: string;
    mode: 'send' | 'edit' | 'regenerate';
    draftId: string | null;
  } | null = null;
  private preferenceWrite: Promise<void> = Promise.resolve();
  private preferenceVersion = 0;
  private restoringDraft = false;
  private authGeneration = -1;

  constructor() {
    effect(() => {
      const generation = this.auth.generation();
      if (this.authGeneration >= 0 && generation !== this.authGeneration)
        untracked(() => this.resetSession());
    });
    effect((onCleanup) => {
      this.draft();
      this.attachments.files();
      this.selected();
      this.ready();
      this.editing();
      const timer = setTimeout(() => this.persistDraft(), 400);
      onCleanup(() => clearTimeout(timer));
    });
    const save = () => this.persistDraft();
    window.addEventListener('pagehide', save);
    inject(DestroyRef).onDestroy(() => {
      save();
      window.removeEventListener('pagehide', save);
      this.subscription?.abort();
    });
  }

  private persistDraft() {
    const user = this.me();
    if (
      user &&
      this.ready() &&
      !this.loadingConversation() &&
      !this.editing() &&
      this.personal.value().saveLocalDrafts &&
      !this.restoringDraft
    )
      this.drafts.save(
        user.id,
        this.selected()?.id ?? null,
        this.draft().text,
        this.attachments.files().map((file) => file.id),
      );
  }
  private async restoreDraft(id: string | null) {
    const user = this.me();
    if (!user || !this.personal.value().saveLocalDrafts) return;
    const saved = this.drafts.load(user.id, id);
    this.restoringDraft = true;
    if (saved) {
      this.draft.set({ text: saved.text });
      await this.attachments.restore(saved.attachmentIds);
    }
    this.restoringDraft = false;
  }

  initialize(retry = false): Promise<void> {
    if (this.initialized && !retry && this.authGeneration === this.auth.generation())
      return this.initialized;
    if (this.authGeneration >= 0 && this.authGeneration !== this.auth.generation())
      this.resetSession();
    this.authGeneration = this.auth.generation();
    this.initialized = this.bootstrap();
    return this.initialized;
  }

  private async bootstrap() {
    const generation = this.auth.generation();
    this.loading.set(true);
    this.error.set(null);
    try {
      if (!(await this.auth.requireLogin())) return;
      const me = await this.api.me();
      if (generation !== this.auth.generation()) return;
      this.me.set(me);
      this.session.adopt(me);
      await this.personal.load(me.id, true);
      if (generation !== this.auth.generation()) return;
      this.themes.apply({
        theme: me.preferences.theme ?? 'system',
        reducedMotion: me.preferences.reducedMotion ?? false,
        defaultModelId: me.preferences.defaultModelId ?? null,
      });
      if (!this.hasChatAccess()) {
        this.ready.set(false);
        this.error.set('你的角色目前沒有 AI 對話功能，請聯絡管理員。');
        return;
      }
      const catalog = await this.api.models();
      if (generation !== this.auth.generation()) return;
      this.models.set(catalog.models as Model[]);
      this.policy.set(catalog.policy as ModelPolicy);
      this.modelNotice.set(catalog.notice ?? null);
      this.modelId.set(
        catalog.policy.allowModelSelection &&
          catalog.models.some((x) => x.id === me.preferences.defaultModelId)
          ? me.preferences.defaultModelId!
          : catalog.models.some((x) => x.id === catalog.policy.defaultModelId)
            ? catalog.policy.defaultModelId!
            : (catalog.models[0]?.id ?? ''),
      );
      this.reasoningEffort.set(
        this.models()
          .find((x) => x.id === this.modelId())
          ?.reasoningEfforts.includes(this.personal.value().defaultReasoningEffort)
          ? this.personal.value().defaultReasoningEffort
          : (this.models().find((x) => x.id === this.modelId())?.defaultReasoningEffort ?? 'auto'),
      );
      await this.refreshHistory();
      if (generation !== this.auth.generation()) return;
      this.ready.set(true);
      const extensions = await Promise.allSettled([
        this.attachments.initialize(),
        this.workspace.labels(),
        ...(this.me()?.access.features?.some((x) => x.id === 'knowledge')
          ? [this.knowledge.initialize()]
          : []),
      ]);
      if (generation !== this.auth.generation()) return;
      if (extensions[1].status === 'fulfilled') this.labels.set(extensions[1].value);
      if (extensions[0].status === 'rejected')
        this.attachments.error.set('附件服務尚未就緒，請重新連線。');
      if (!this.selected() && !this.draft().text) await this.restoreDraft(null);
      if (me.activeRunId && !this.liveRun()) {
        const active = await this.api.run(me.activeRunId);
        if (generation !== this.auth.generation()) return;
        if (isActive(active.status)) void this.follow(active);
      }
    } catch (error) {
      if (generation === this.auth.generation()) {
        this.ready.set(false);
        this.report(error);
      }
    } finally {
      if (generation === this.auth.generation()) this.loading.set(false);
    }
  }

  async refreshHistory(more = false) {
    const version = ++this.searchVersion;
    const rows = await this.api.conversations(
      this.search(),
      more ? this.conversations().length : 0,
      this.historyView(),
      this.historyLabel(),
    );
    if (version !== this.searchVersion) return;
    this.conversations.update((current) => (more ? [...current, ...rows] : rows));
    this.hasMore.set(rows.length === 100);
  }
  async searchHistory(value: string) {
    this.search.set(value);
    if (this.ready()) {
      try {
        await this.refreshHistory();
      } catch (error) {
        this.report(error);
      }
    }
  }

  async select(id: string | null, force = false) {
    if (id && id === this.selected()?.id && !force) return;
    // Initial route resolution must preserve text entered while bootstrap is still loading.
    if (!id && !this.selected() && this.messages().length === 0 && !this.loadingConversation()) {
      this.loadingConversation.set(false);
      return;
    }
    const version = ++this.selectionVersion;
    this.persistDraft();
    this.editing.set(null);
    this.draft.set({ text: '' });
    this.attachments.reset();
    if (!id) {
      await this.knowledge.load(null);
      this.selected.set(null);
      this.messages.set([]);
      this.loadingConversation.set(true);
      try {
        await this.restoreDraft(null);
      } finally {
        if (version === this.selectionVersion) this.loadingConversation.set(false);
      }
      return;
    }
    this.loadingConversation.set(true);
    try {
      const detail = await this.api.conversation(id);
      if (version !== this.selectionVersion) return;
      this.selected.set(detail.conversation as Conversation);
      this.messages.set(detail.messages as Message[]);
      if (this.me()?.access.features?.some((x) => x.id === 'knowledge'))
        await this.knowledge.load(id);
      if (version !== this.selectionVersion) return;
      await this.restoreDraft(id);
      if (version !== this.selectionVersion) return;
      if (detail.activeRun && this.liveRun()?.id !== detail.activeRun.id)
        void this.follow(detail.activeRun as Run);
    } catch (error) {
      if (version === this.selectionVersion) {
        this.report(error);
        this.selected.set(null);
        this.messages.set([]);
      }
    } finally {
      if (version === this.selectionVersion) this.loadingConversation.set(false);
    }
  }

  async send() {
    const generation = this.auth.generation();
    const text = this.draft().text.trim();
    if (!text || !this.canSend()) return;
    this.submitting.set(true);
    this.error.set(null);
    try {
      let conversation = this.selected();
      const draftId = conversation?.id ?? null;
      if (!conversation) {
        conversation = await this.api.createConversation();
        if (generation !== this.auth.generation()) return;
        await this.knowledge.bindNew(conversation.id);
        if (generation !== this.auth.generation()) return;
        this.selected.set(conversation);
        await this.router.navigate(['/chat', conversation.id]);
      }
      const edit = this.editing();
      this.pending = {
        key: crypto.randomUUID(),
        mode: edit ? 'edit' : 'send',
        draftId,
        body: {
          conversationId: conversation.id,
          modelId: this.modelId(),
          prompt: text,
          parentMessageId: edit ? edit.parentId : conversation.activeLeafId,
          regenerateUserMessageId: null,
          reasoningEffort: this.reasoningEffort(),
          attachmentIds: this.attachments.files().map((file) => file.id),
        },
      };
      await this.submitPending();
    } catch (error) {
      if (generation === this.auth.generation()) this.submissionError(error);
    } finally {
      if (generation === this.auth.generation()) this.submitting.set(false);
    }
  }

  async retrySubmission() {
    const generation = this.auth.generation();
    if (!this.pending || this.submitting()) return;
    this.submitting.set(true);
    this.error.set(null);
    try {
      await this.submitPending();
    } catch (error) {
      if (generation === this.auth.generation()) this.submissionError(error);
    } finally {
      if (generation === this.auth.generation()) this.submitting.set(false);
    }
  }

  private async submitPending() {
    const generation = this.auth.generation();
    const pending = this.pending!;
    const run = await this.api.createRun(pending.body, pending.key);
    if (generation !== this.auth.generation()) return;
    this.pending = null;
    this.pendingSubmission.set(false);
    if (pending.mode === 'send' && this.me()) {
      this.drafts.clear(this.me()!.id, pending.draftId);
      this.drafts.clear(this.me()!.id, run.conversationId);
    }
    if (this.selected()?.id === run.conversationId && pending.mode !== 'regenerate') {
      this.draft.set({ text: '' });
      this.attachments.reset();
      this.editing.set(null);
      if (pending.mode === 'edit') await this.restoreDraft(run.conversationId);
    }
    if (generation !== this.auth.generation()) return;
    this.liveRun.set(run);
    let snapshot = run;
    try {
      const detail = await this.api.conversation(run.conversationId);
      if (generation !== this.auth.generation()) return;
      if (this.selected()?.id === run.conversationId) {
        this.selected.set(detail.conversation as Conversation);
        this.messages.set(detail.messages as Message[]);
      }
      snapshot = (detail.activeRun as Run) ?? run;
    } catch (error) {
      if (generation === this.auth.generation()) this.report(error);
    }
    if (generation !== this.auth.generation()) return;
    void this.follow(snapshot);
    await this.refreshHistory().catch((error) => this.report(error));
  }

  private submissionError(error: unknown) {
    this.report(error);
    this.pendingSubmission.set(!!this.pending && error instanceof ApiError && error.status === 0);
    if (!this.pendingSubmission()) this.pending = null;
  }

  async regenerate(message: Message) {
    const generation = this.auth.generation();
    if (this.busy() || !message.parentId || !this.modelId()) return;
    this.submitting.set(true);
    this.error.set(null);
    this.pending = {
      key: crypto.randomUUID(),
      mode: 'regenerate',
      draftId: this.selected()!.id,
      body: {
        conversationId: this.selected()!.id,
        modelId: this.modelId(),
        prompt: null,
        parentMessageId: null,
        regenerateUserMessageId: message.parentId,
        reasoningEffort: this.reasoningEffort(),
        attachmentIds: null,
      },
    };
    try {
      await this.submitPending();
    } catch (error) {
      if (generation === this.auth.generation()) this.submissionError(error);
    } finally {
      if (generation === this.auth.generation()) this.submitting.set(false);
    }
  }

  edit(message: Message) {
    if (!this.busy()) {
      this.persistDraft();
      this.editing.set(message);
      this.draft.set({ text: message.content });
      this.attachments.reset(message.attachments ?? []);
    }
  }
  cancelEdit() {
    this.editing.set(null);
    this.draft.set({ text: '' });
    this.attachments.reset();
    void this.restoreDraft(this.selected()?.id ?? null);
  }

  async selectVersion(message: Message, direction: number) {
    if (this.busy()) return;
    const leaf = this.tree().versionLeaf(message, direction);
    if (!leaf) return;
    try {
      await this.api.branch(this.selected()!.id, leaf);
      await this.select(this.selected()!.id, true);
      await this.refreshHistory();
    } catch (error) {
      this.report(error);
    }
  }

  async stop() {
    const generation = this.auth.generation();
    const run = this.liveRun();
    if (!run || this.stopping()) return;
    this.stopping.set(true);
    try {
      const final = await this.api.cancel(run.id);
      if (generation !== this.auth.generation()) return;
      this.subscription?.abort();
      this.streamingText.set(final.content);
      await this.finish(final);
    } catch (error) {
      if (generation === this.auth.generation()) this.report(error);
    } finally {
      if (generation === this.auth.generation()) this.stopping.set(false);
    }
  }

  async resume() {
    const generation = this.auth.generation();
    const run = this.liveRun();
    if (!run) return;
    try {
      this.error.set(null);
      const current = await this.api.run(run.id);
      if (generation === this.auth.generation()) await this.follow(current);
    } catch (error) {
      this.report(error);
    }
  }

  private async follow(run: Run) {
    this.subscription?.abort();
    const controller = (this.subscription = new AbortController());
    this.liveRun.set(run);
    try {
      const terminal = await this.stream.follow(run, controller.signal, {
        content: (content) => {
          if (!controller.signal.aborted) this.streamingText.set(content);
        },
        status: (current) => {
          if (!controller.signal.aborted) this.liveRun.set(current);
        },
        connection: (state) => {
          if (!controller.signal.aborted) this.connection.set(state);
        },
      });
      if (!controller.signal.aborted) await this.finish(terminal);
    } catch (error) {
      if (!controller.signal.aborted) {
        this.connection.set('disconnected');
        if (error instanceof ApiError && [401, 403, 404].includes(error.status)) {
          this.liveRun.set(null);
          if (error.status !== 404) this.ready.set(false);
        }
        this.report(error);
      }
    }
  }

  private async finish(run: Run) {
    const generation = this.auth.generation();
    if (this.selected()?.id === run.conversationId) {
      const detail = await this.api.conversation(run.conversationId);
      if (generation !== this.auth.generation()) return;
      if (this.selected()?.id === run.conversationId) {
        this.selected.set(detail.conversation as Conversation);
        this.messages.set(detail.messages as Message[]);
      }
    }
    if (this.liveRun()?.id === run.id) this.liveRun.set(null);
    this.connection.set('connected');
    await this.refreshHistory();
    if (generation === this.auth.generation() && run.status === 'completed')
      this.personal.notifyCompleted();
  }

  async rename(id: string, title: string) {
    try {
      const updated = await this.api.rename(id, title);
      if (this.selected()?.id === id) this.selected.set(updated);
      await this.refreshHistory();
      return true;
    } catch (error) {
      this.report(error);
      return false;
    }
  }
  async remove(id: string) {
    try {
      await this.api.delete(id);
      if (this.selected()?.id === id) await this.router.navigate(['/chat']);
      await this.refreshHistory();
      return true;
    } catch (error) {
      this.report(error);
      return false;
    }
  }
  async savePreferences(value: Preferences) {
    const generation = this.auth.generation();
    const version = ++this.preferenceVersion;
    this.themes.apply(value);
    if (!this.ready()) return;
    this.preferenceWrite = this.preferenceWrite.then(async () => {
      if (generation !== this.auth.generation()) return;
      try {
        const saved = await this.api.preferences(value);
        if (generation !== this.auth.generation()) return;
        this.me.update((current) => (current ? { ...current, preferences: saved } : current));
        if (version === this.preferenceVersion) this.themes.apply(saved);
      } catch (error) {
        if (version === this.preferenceVersion) {
          const confirmed = this.me()?.preferences;
          if (confirmed) this.themes.apply(confirmed);
          this.report(error);
        }
      }
    });
    await this.preferenceWrite;
  }
  report(error: unknown) {
    this.error.set(error instanceof Error ? error.message : '操作失敗，請稍後重試。');
  }

  chooseModel(id: string) {
    if (!this.policy().allowModelSelection || this.busy()) return;
    const model = this.models().find((x) => x.id === id);
    if (!model) return;
    this.modelId.set(id);
    this.reasoningEffort.set(model.defaultReasoningEffort);
    this.contextUsage.set(null);
  }

  async previewContext(body: ContextPreview, signal: AbortSignal) {
    try {
      const usage = await this.api.context(body, signal);
      if (!signal.aborted) {
        this.contextUsage.set(usage);
        this.contextNotice.set(null);
      }
    } catch {
      if (!signal.aborted) {
        this.contextUsage.set(null);
        this.contextNotice.set('暫時無法取得 Context 預估，仍可提交訊息。');
      }
    }
  }

  async filterHistory(view = this.historyView(), label = this.historyLabel()) {
    this.historyView.set(view);
    this.historyLabel.set(label);
    try {
      await this.refreshHistory();
    } catch (error) {
      this.report(error);
    }
  }
  async organize(conversation: Conversation, settings: ConversationSettings) {
    const generation = this.auth.generation();
    try {
      const saved = await this.workspace.settings(conversation.id, settings);
      if (generation !== this.auth.generation()) return false;
      if (this.selected()?.id === saved.id) this.selected.set(saved);
      await this.refreshHistory();
      const labels = await this.workspace.labels();
      if (generation !== this.auth.generation()) return false;
      this.labels.set(labels);
      return true;
    } catch (error) {
      this.report(error);
      return false;
    }
  }
  async duplicate() {
    const generation = this.auth.generation();
    const selected = this.selected();
    if (!selected || this.busy()) return;
    try {
      const copy = await this.workspace.duplicate(selected.id);
      if (generation !== this.auth.generation()) return;
      await this.refreshHistory();
      await this.router.navigate(['/chat', copy.id]);
    } catch (error) {
      this.report(error);
    }
  }
  async exportBackup() {
    const generation = this.auth.generation();
    const selected = this.selected();
    if (!selected || this.busy()) return;
    try {
      const backup = await this.workspace.export(selected.id);
      if (generation !== this.auth.generation()) return;
      downloadFile(JSON.stringify(backup, null, 2), selected.title, 'json');
    } catch (error) {
      this.report(error);
    }
  }
  async importBackup(file: File) {
    const generation = this.auth.generation();
    if (file.size > 8 * 1024 * 1024) {
      this.error.set('文字備份最多 8 MB。');
      return;
    }
    try {
      const body = JSON.parse(await file.text()) as ConversationBackup;
      if (generation !== this.auth.generation()) return;
      const imported = await this.workspace.import(body);
      if (generation !== this.auth.generation()) return;
      await this.refreshHistory();
      const labels = await this.workspace.labels();
      if (generation !== this.auth.generation()) return;
      this.labels.set(labels);
      await this.router.navigate(['/chat', imported.id]);
    } catch (error) {
      this.report(error instanceof SyntaxError ? new Error('備份不是有效的 JSON 格式。') : error);
    }
  }
  removeAttachment(id: string) {
    const inHistory = this.messages().some((message) =>
      message.attachments?.some((file) => file.id === id),
    );
    void this.attachments.remove(id, inHistory);
  }

  async logout() {
    try {
      this.persistDraft();
      await this.auth.logout();
      this.resetSession();
    } catch (error) {
      this.report(error);
    }
  }
  private resetSession() {
    this.knowledge.reset();
    this.persistDraft();
    this.subscription?.abort();
    this.initialized = null;
    this.pending = null;
    this.pendingSubmission.set(false);
    this.submitting.set(false);
    this.stopping.set(false);
    this.loadingConversation.set(false);
    this.restoringDraft = false;
    this.me.set(null);
    this.ready.set(false);
    this.selected.set(null);
    this.conversations.set([]);
    this.messages.set([]);
    this.models.set([]);
    this.liveRun.set(null);
    this.streamingText.set('');
    this.draft.set({ text: '' });
    this.contextUsage.set(null);
    this.contextNotice.set(null);
    this.attachments.reset();
    this.labels.set([]);
    this.editing.set(null);
    this.historyView.set('active');
    this.historyLabel.set('');
    this.search.set('');
    this.error.set(null);
    this.selectionVersion++;
    this.searchVersion++;
    this.preferenceVersion++;
    this.authGeneration = this.auth.generation();
  }
}
