import { ApiError, safeMessage } from '@core/errors/safe-errors';
import { NotificationStore } from '@features/platform/notifications/notification-store';
import { computed, DestroyRef, effect, inject, Injectable, signal, untracked } from '@angular/core';
import { Router } from '@angular/router';
import { WorkspaceApi } from '@core/api/workspace-api';
import type {
  ContextPreviewRequest,
  AttachmentDto,
  ContextUsageDto,
  ConversationDto,
  CreateRunRequest,
  MessageDto,
  PreferencesDto,
  RunDto,
} from '@core/api/schema';
import { isActive } from '@core/api/generation-status';
import { ViewMorph } from '@core/layout/view-morph';
import { ThemeService } from '@core/preferences/theme-service';
import { AuthService } from '@core/auth/auth-service';
import { DraftAttachments } from '@features/work/files/draft-attachments';
import { MessageTree } from './state/message-tree';
import { UserSettingsService } from '@core/preferences/user-settings';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { KnowledgeSelection } from '@features/work/knowledge/collections/knowledge-selection';
import { ChatDraft } from './state/chat-draft';
import { ChatHistory } from './state/chat-history';
import { ChatModels } from './state/chat-models';
import { ChatRun } from './state/chat-run';
import { routeOf } from '@core/api/catalog';

/** 送出中的提問在伺服器回傳前的暫時 ID 前綴；對話串據此認出被伺服器副本取代的那則提問。 */
export const PENDING_MESSAGE_PREFIX = 'pending:';

/**
 * 協調聊天頁：啟動、選取對話與送出。有自己生命週期的狀態放在 ChatModels、ChatRun、ChatDraft 與
 * ChatHistory，
 * 下方的別名讓範本只需面對同一組介面。
 */
@Injectable({ providedIn: 'root' })
export class ChatStore {
  private readonly notifications = inject(NotificationStore);
  private readonly api = inject(WorkspaceApi);
  private readonly router = inject(Router);
  private readonly themes = inject(ThemeService);
  private readonly morph = inject(ViewMorph);
  readonly attachments = inject(DraftAttachments);
  readonly knowledge = inject(KnowledgeSelection);
  readonly auth = inject(AuthService);
  readonly personal = inject(UserSettingsService);
  private readonly session = inject(WorkspaceSession);
  readonly model = inject(ChatModels);
  readonly run = inject(ChatRun);
  readonly composer = inject(ChatDraft);
  readonly history = inject(ChatHistory);
  readonly drafts = this.composer.repository;
  readonly me = this.session.me;
  readonly conversations = this.history.conversations;
  readonly selected = signal<ConversationDto | null>(null);
  readonly messages = signal<MessageDto[]>([]);
  readonly models = this.model.models;
  readonly modelId = this.model.modelId;
  readonly policy = this.model.policy;
  readonly reasoningEffort = this.model.reasoningEffort;
  readonly webSearchEnabled = this.model.webSearchEnabled;
  readonly webSearchStatus = this.model.webSearchStatus;
  readonly modelNotice = this.model.notice;
  readonly contextUsage = signal<ContextUsageDto | null>(null);
  /** 較新的用量估算正在路上；`contextUsage` 仍保留上一筆。 */
  readonly contextPending = signal(false);
  readonly contextNotice = signal<string | null>(null);
  readonly hasChatAccess = computed(() => this.session.has('chat'));
  readonly hasKnowledgeAccess = computed(() => this.session.has('knowledge'));
  readonly hasArtifactsAccess = computed(() => this.session.has('artifacts'));
  readonly hasSharing = computed(() => this.session.has('sharing'));
  readonly error = signal<string | null>(null);
  readonly ready = signal(false);
  readonly loading = signal(true);
  readonly loadingConversation = signal(false);
  readonly submitting = signal(false);
  readonly stopping = this.run.stopping;
  readonly liveRun = this.run.live;
  readonly streamingText = this.run.text;
  readonly connection = this.run.connection;
  readonly pendingSubmission = signal(false);
  readonly hasMore = this.history.hasMore;
  readonly search = this.history.search;
  readonly historyView = this.history.view;
  readonly historyLabel = this.history.label;
  readonly labels = this.history.labels;
  readonly editing = this.composer.editing;
  readonly draft = this.composer.content;
  readonly tree = computed(() => new MessageTree(this.messages()));
  readonly visionNotice = computed(() =>
    this.attachments.files().some((item) => item.isImage) && !this.model.current()?.supportsImages
      ? '目前模型不支援圖片，請切換模型或移除圖片。'
      : '',
  );
  readonly busy = computed(
    () => this.submitting() || this.liveRun() !== null || this.pendingSubmission(),
  );
  /** 頁面或對話還在載入，或對話已封存。 */
  readonly conversationLocked = computed(
    () => this.loading() || this.loadingConversation() || !!this.selected()?.isArchived,
  );
  readonly canPickKnowledge = computed(() => !this.conversationLocked() && !this.busy());
  readonly canSend = computed(
    () =>
      this.ready() &&
      this.hasChatAccess() &&
      !!this.modelId() &&
      !this.conversationLocked() &&
      !this.attachments.uploading() &&
      !this.attachments.files().some((item) => item.analysisMode === 'ocr-required') &&
      !this.knowledge.saving() &&
      !this.knowledge.loadFailed() &&
      !this.visionNotice() &&
      !this.busy() &&
      !this.contextUsage()?.budgetExceeded,
  );
  readonly canUpload = computed(
    () =>
      this.ready() &&
      this.hasChatAccess() &&
      !this.conversationLocked() &&
      !this.busy() &&
      !this.attachments.uploading() &&
      !!this.attachments.policy(),
  );
  /** 送出中的提問，在伺服器的版本抵達前先立即顯示。 */
  private readonly prompt = signal<MessageDto | null>(null);
  readonly visibleMessages = computed(() => {
    const prompt = this.prompt();
    return prompt
      ? [...this.tree().branch(prompt.parentId), prompt]
      : this.tree().branch(this.selected()?.activeLeafId);
  });
  /** 目前沒有對話內容，頁面會顯示起始畫面。 */
  readonly starting = computed(() => !this.visibleMessages().length && !this.loadingConversation());
  private unsent: { text: string; editing: MessageDto | null; files: AttachmentDto[] } | null =
    null;
  private initialized: Promise<void> | null = null;
  private selectionVersion = 0;
  private pending: {
    body: CreateRunRequest;
    key: string;
    mode: 'send' | 'edit' | 'regenerate';
    draftId: string | null;
  } | null = null;
  private preferenceWrite: Promise<void> = Promise.resolve();
  private preferenceVersion = 0;
  /** 目前資料所屬的帳號世代；null 表示尚未啟動。世代改變時整份重設。 */
  private account: (() => boolean) | null = null;
  // 儲存要等工作階段的草稿讀取完成；啟動太慢時，不可以用空白草稿覆寫它。
  // 擁有者保存在此處，因為共用工作階段在此 store 重設前就會忘記帳號。
  private draftOwner: string | null = null;

  constructor() {
    effect(() => {
      // 先讀世代建立依賴；尚未啟動時 account 為 null，不讀就不會再被喚醒。
      this.auth.generation();
      if (this.account && !this.account()) untracked(() => this.resetSession());
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
      this.run.reset();
    });
  }

  private persistDraft() {
    if (this.draftOwner && this.ready() && !this.loadingConversation())
      this.composer.save(this.draftOwner, this.selected()?.id ?? null);
  }
  private async restoreDraft(id: string | null) {
    const user = this.me();
    if (user) await this.composer.restore(user.id, id);
  }

  initialize(retry = false): Promise<void> {
    if (this.initialized && !retry && this.account?.()) return this.initialized;
    if (this.account && !this.account()) this.resetSession();
    this.account = this.auth.guard();
    this.initialized = this.bootstrap();
    return this.initialized;
  }

  private async bootstrap() {
    const current = this.auth.guard();
    this.loading.set(true);
    this.error.set(null);
    try {
      // 帳號與設定一起載入；這裡的帳號對進行中的執行是最新的。
      const me = await this.session.load(true);
      if (!me || !current()) return;
      this.themes.apply({
        theme: me.preferences.theme ?? 'system',
        reducedMotion: me.preferences.reducedMotion ?? false,
        defaultModelId: me.preferences.defaultModelId ?? null,
      });
      if (!this.hasChatAccess()) {
        this.ready.set(false);
        this.error.set('你的角色目前沒有對話功能，請聯絡管理員。');
        return;
      }
      // 選用服務不會阻擋輸入框，所以與模型與歷史一起啟動。
      const extensions = Promise.allSettled([
        this.attachments.initialize(),
        this.history.reloadLabels(),
        ...(this.hasKnowledgeAccess() ? [this.knowledge.initialize()] : []),
      ]);
      const [catalog] = await Promise.all([this.api.models(), this.refreshHistory()]);
      if (!current()) return;
      this.model.adopt(
        catalog,
        me.preferences.defaultModelId,
        this.personal.value().defaultReasoningEffort,
      );
      this.ready.set(true);
      void this.model.loadWebSearch();
      const [attachments] = await extensions;
      if (!current()) return;
      if (attachments.status === 'rejected')
        this.attachments.error.set('附件服務尚未就緒，請重新連線。');
      if (!this.selected() && !this.draft().text) await this.restoreDraft(null);
      this.draftOwner = me.id;
      this.persistDraft();
      if (me.activeRunId && !this.liveRun()) {
        const active = await this.api.run(me.activeRunId);
        if (!current()) return;
        if (isActive(active.status)) void this.follow(active);
      }
    } catch (error) {
      if (current()) {
        this.ready.set(false);
        this.report(error);
      }
    } finally {
      if (current()) this.loading.set(false);
    }
  }

  refreshHistory(more = false) {
    return this.history.refresh(more);
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
    // 啟動期間輸入的文字，在初始路由解析時必須保留。
    if (!id && !this.selected() && this.messages().length === 0 && !this.loadingConversation()) {
      this.loadingConversation.set(false);
      return;
    }
    const version = ++this.selectionVersion;
    this.prompt.set(null);
    this.persistDraft();
    this.composer.clear();
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
      await this.morph.run(() => {
        if (version !== this.selectionVersion) return;
        this.selected.set(detail.conversation as ConversationDto);
        this.messages.set(detail.messages as MessageDto[]);
      }, 'conversation-title');
      if (version !== this.selectionVersion) return;
      this.notifications.readConversation(id);
      if (this.hasKnowledgeAccess()) await this.knowledge.load(id);
      if (version !== this.selectionVersion) return;
      await this.restoreDraft(id);
      if (version !== this.selectionVersion) return;
      if (detail.activeRun && this.liveRun()?.id !== detail.activeRun.id)
        void this.follow(detail.activeRun as RunDto);
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
    const text = this.draft().text.trim();
    if (!text || !this.canSend()) return;
    await this.whileSubmitting(async (current) => {
      const edit = this.editing();
      const files = this.attachments.files();
      this.showPrompt(text, edit ? edit.parentId : (this.selected()?.activeLeafId ?? null));
      let conversation = this.selected();
      const draftId = conversation?.id ?? null;
      if (!conversation) {
        conversation = await this.api.createConversation();
        if (!current()) return;
        await this.knowledge.bindNew(conversation.id);
        if (!current()) return;
        this.selected.set(conversation);
        await this.router.navigate([routeOf('chat'), conversation.id]);
      }
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
          attachmentIds: files.map((file) => file.id),
          webSearch: this.webSearchEnabled(),
        },
      };
      await this.submitPending();
    });
  }

  async retrySubmission() {
    if (this.pending && !this.submitting()) await this.whileSubmitting(() => this.submitPending());
  }

  /** 工作執行時維持 `submitting`；同一帳號的失敗會保留或退回提問。 */
  private async whileSubmitting(work: (current: () => boolean) => Promise<void>) {
    const current = this.auth.guard();
    this.submitting.set(true);
    this.error.set(null);
    try {
      await work(current);
    } catch (error) {
      if (current()) this.submissionError(error);
    } finally {
      if (current()) this.submitting.set(false);
    }
  }

  private async submitPending() {
    const current = this.auth.guard();
    const pending = this.pending!;
    const run = await this.api.createRun(pending.body, pending.key);
    if (!current()) return;
    this.pending = null;
    this.unsent = null;
    this.pendingSubmission.set(false);
    if (pending.mode === 'send' && this.me()) {
      this.drafts.clear(this.me()!.id, pending.draftId);
      this.drafts.clear(this.me()!.id, run.conversationId);
    }
    if (this.selected()?.id === run.conversationId && pending.mode !== 'regenerate') {
      this.composer.clear();
      this.attachments.reset();
      if (pending.mode === 'edit') await this.restoreDraft(run.conversationId);
    }
    if (!current()) return;
    this.liveRun.set(run);
    let snapshot = run;
    let conversation: ConversationDto | null = null;
    try {
      const detail = await this.api.conversation(run.conversationId);
      if (!current()) return;
      if (this.selected()?.id === run.conversationId) this.adopt(detail);
      snapshot = (detail.activeRun as RunDto) ?? run;
      conversation = detail.conversation as ConversationDto;
    } catch (error) {
      if (current()) this.report(error);
    }
    if (!current()) return;
    void this.follow(snapshot);
    await (conversation ? this.showInHistory(conversation) : this.refreshHistory()).catch(
      (error) => {
        if (current()) this.report(error);
      },
    );
  }

  /**
   * 以新的副本更新單一歷史列，而不重新讀取列表。搜尋由伺服器比對，所以仍會重讀。
   */
  private async showInHistory(conversation: ConversationDto) {
    if (this.search()) await this.refreshHistory();
    else this.history.place(conversation);
  }

  /** 網路失敗會保留提問以便重試；其他失敗則退回輸入框。 */
  private submissionError(error: unknown) {
    this.report(error);
    this.pendingSubmission.set(!!this.pending && error instanceof ApiError && error.status === 0);
    if (this.pendingSubmission()) return;
    this.pending = null;
    this.prompt.set(null);
    if (this.unsent) {
      this.draft.set({ text: this.unsent.text });
      this.editing.set(this.unsent.editing);
      this.attachments.reset(this.unsent.files);
      this.unsent = null;
    }
  }

  private showPrompt(text: string, parentId: string | null) {
    this.unsent = { text, editing: this.editing(), files: this.attachments.files() };
    this.prompt.set({
      id: PENDING_MESSAGE_PREFIX + crypto.randomUUID(),
      parentId,
      role: 'user',
      content: text,
      status: 'completed',
      createdAt: new Date().toISOString(),
      runId: null,
      modelId: null,
      attachments: this.unsent.files,
      errorCode: null,
      sources: null,
      feedbackRating: 0,
      charge: null,
      webSources: null,
      webSearchCharge: null,
      timing: null,
      modelDisplayName: null,
      issueCode: null,
    });
    this.composer.clear();
    this.attachments.reset();
  }

  /** 伺服器上開啟對話的版本，會取代排在前面的提問顯示。 */
  private adopt(detail: { conversation: unknown; messages: unknown }) {
    this.selected.set(detail.conversation as ConversationDto);
    this.messages.set(detail.messages as MessageDto[]);
    this.prompt.set(null);
  }

  async regenerate(message: MessageDto) {
    if (this.busy() || !message.parentId || !this.modelId()) return;
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
        webSearch: this.webSearchEnabled(),
      },
    };
    await this.whileSubmitting(() => this.submitPending());
  }

  edit(message: MessageDto) {
    if (!this.busy()) {
      this.persistDraft();
      this.editing.set(message);
      this.draft.set({ text: message.content });
      this.attachments.reset(message.attachments ?? []);
    }
  }
  cancelEdit() {
    this.composer.clear();
    this.attachments.reset();
    void this.restoreDraft(this.selected()?.id ?? null);
  }

  async selectVersion(message: MessageDto, direction: number) {
    if (this.busy()) return;
    const leaf = this.tree().versionLeaf(message, direction);
    if (!leaf) return;
    try {
      await this.api.branch(this.selected()!.id, leaf);
      await this.select(this.selected()!.id, true);
      const conversation = this.selected();
      if (conversation) await this.showInHistory(conversation);
    } catch (error) {
      this.report(error);
    }
  }

  async stop() {
    const current = this.auth.guard();
    const run = this.liveRun();
    if (!run || this.stopping()) return;
    this.stopping.set(true);
    try {
      const final = await this.api.cancel(run.id);
      if (!current()) return;
      this.run.stopped(final);
      await this.finish(final);
    } catch (error) {
      if (current()) this.report(error);
    } finally {
      if (current()) this.stopping.set(false);
    }
  }

  async resume() {
    const current = this.auth.guard();
    const run = this.liveRun();
    if (!run) return;
    try {
      this.error.set(null);
      const latest = await this.api.run(run.id);
      if (current()) await this.follow(latest);
    } catch (error) {
      // 換帳號後才回來的失敗屬於前一個帳號，不能寫到新帳號的畫面上。
      if (current()) this.report(error);
    }
  }

  private async follow(run: RunDto) {
    try {
      await this.run.follow(run, (terminal) => this.finish(terminal));
    } catch (error) {
      if (error instanceof ApiError && [401, 403].includes(error.status)) this.ready.set(false);
      this.report(error);
    }
  }

  private async finish(run: RunDto) {
    const current = this.auth.guard();
    let conversation: ConversationDto | null = null;
    if (this.selected()?.id === run.conversationId) {
      const detail = await this.api.conversation(run.conversationId);
      if (!current()) return;
      if (this.selected()?.id === run.conversationId) this.adopt(detail);
      conversation = detail.conversation as ConversationDto;
    }
    this.run.settle(run);
    // 其他對話的執行在這裡沒有新副本，所以它的列需要伺服器的列表。
    await (conversation ? this.showInHistory(conversation) : this.refreshHistory());
    if (current()) await this.notifications.refresh();
  }

  async savePreferences(value: PreferencesDto) {
    const current = this.auth.guard();
    const version = ++this.preferenceVersion;
    this.themes.apply(value);
    if (!this.ready()) return;
    this.preferenceWrite = this.preferenceWrite.then(async () => {
      if (!current()) return;
      try {
        const saved = await this.api.preferences(value);
        if (!current()) return;
        this.me.update((me) => (me ? { ...me, preferences: saved } : me));
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
    this.error.set(safeMessage(error));
  }

  rateMessage(value: { id: string; rating: number }) {
    this.messages.update((items) =>
      items.map((item) =>
        item.id === value.id ? { ...item, feedbackRating: value.rating } : item,
      ),
    );
  }

  chooseModel(id: string) {
    if (!this.busy() && this.model.choose(id)) this.contextUsage.set(null);
  }

  async previewContext(body: ContextPreviewRequest, signal: AbortSignal) {
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
    } finally {
      if (!signal.aborted) this.contextPending.set(false);
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
    this.draftOwner = null;
    this.model.reset();
    this.run.reset();
    this.composer.reset();
    this.history.reset();
    this.initialized = null;
    this.pending = null;
    this.unsent = null;
    this.prompt.set(null);
    this.pendingSubmission.set(false);
    this.submitting.set(false);
    this.loadingConversation.set(false);
    this.me.set(null);
    this.ready.set(false);
    this.selected.set(null);
    this.messages.set([]);
    this.contextUsage.set(null);
    this.contextPending.set(false);
    this.contextNotice.set(null);
    this.attachments.reset();
    this.error.set(null);
    this.selectionVersion++;
    this.preferenceVersion++;
    this.account = this.auth.guard();
  }
}
