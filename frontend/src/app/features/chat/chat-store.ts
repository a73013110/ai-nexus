import { computed, inject, Injectable, signal } from '@angular/core';
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

@Injectable({ providedIn: 'root' })
export class ChatStore {
  private readonly api = inject(NexusApi);
  private readonly stream = inject(RunStream);
  private readonly router = inject(Router);
  private readonly themes = inject(ThemeService);
  readonly auth = inject(AuthService);
  readonly me = signal<Me | null>(null);
  readonly conversations = signal<Conversation[]>([]);
  readonly selected = signal<Conversation | null>(null);
  readonly messages = signal<Message[]>([]);
  readonly models = signal<Model[]>([]);
  readonly modelId = signal('');
  readonly policy = signal<ModelPolicy>({
    allowModelSelection: true,
    showModelNames: true,
    defaultModelId: null,
  });
  readonly reasoningEffort = signal('auto');
  readonly contextUsage = signal<ContextUsage | null>(null);
  readonly contextNotice = signal<string | null>(null);
  readonly hasChatAccess = computed(
    () => this.me()?.access?.features?.some((x) => x.id === 'chat') ?? false,
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
  readonly editing = signal<Message | null>(null);
  readonly draft = signal({ text: '' });
  readonly busy = computed(
    () => this.submitting() || this.liveRun() !== null || this.pendingSubmission(),
  );
  readonly canSend = computed(
    () =>
      this.ready() &&
      this.hasChatAccess() &&
      !!this.modelId() &&
      !this.busy() &&
      !this.contextUsage()?.budgetExceeded,
  );
  readonly visibleMessages = computed(() => {
    const all = new Map(this.messages().map((message) => [message.id, message]));
    const result: Message[] = [];
    const seen = new Set<string>();
    let leaf = this.selected()?.activeLeafId;
    while (leaf && !seen.has(leaf)) {
      seen.add(leaf);
      const message = all.get(leaf);
      if (!message) break;
      result.push(message);
      leaf = message.parentId;
    }
    return result.reverse();
  });
  private initialized: Promise<void> | null = null;
  private subscription: AbortController | null = null;
  private selectionVersion = 0;
  private searchVersion = 0;
  private pending: { body: CreateRun; key: string } | null = null;
  private preferenceWrite: Promise<void> = Promise.resolve();
  private preferenceVersion = 0;

  initialize(retry = false): Promise<void> {
    if (this.initialized && !retry) return this.initialized;
    this.initialized = this.bootstrap();
    return this.initialized;
  }

  private async bootstrap() {
    this.loading.set(true);
    this.error.set(null);
    try {
      if (!(await this.auth.requireLogin())) return;
      const me = await this.api.me();
      this.me.set(me);
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
        this.models().find((x) => x.id === this.modelId())?.defaultReasoningEffort ?? 'auto',
      );
      await this.refreshHistory();
      this.ready.set(true);
      if (me.activeRunId && !this.liveRun()) {
        const active = await this.api.run(me.activeRunId);
        if (isActive(active.status)) void this.follow(active);
      }
    } catch (error) {
      this.ready.set(false);
      this.report(error);
    } finally {
      this.loading.set(false);
    }
  }

  async refreshHistory(more = false) {
    const version = ++this.searchVersion;
    const rows = await this.api.conversations(
      this.search(),
      more ? this.conversations().length : 0,
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
    if (!id && !this.selected() && this.messages().length === 0) {
      this.loadingConversation.set(false);
      return;
    }
    const version = ++this.selectionVersion;
    this.editing.set(null);
    this.draft.set({ text: '' });
    if (!id) {
      this.selected.set(null);
      this.messages.set([]);
      this.loadingConversation.set(false);
      return;
    }
    this.loadingConversation.set(true);
    try {
      const detail = await this.api.conversation(id);
      if (version !== this.selectionVersion) return;
      this.selected.set(detail.conversation as Conversation);
      this.messages.set(detail.messages as Message[]);
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
    const text = this.draft().text.trim();
    if (!text || !this.canSend()) return;
    this.submitting.set(true);
    this.error.set(null);
    try {
      let conversation = this.selected();
      if (!conversation) {
        conversation = await this.api.createConversation();
        this.selected.set(conversation);
        await this.router.navigate(['/chat', conversation.id]);
      }
      const edit = this.editing();
      this.pending = {
        key: crypto.randomUUID(),
        body: {
          conversationId: conversation.id,
          modelId: this.modelId(),
          prompt: text,
          parentMessageId: edit ? edit.parentId : conversation.activeLeafId,
          regenerateUserMessageId: null,
          reasoningEffort: this.reasoningEffort(),
        },
      };
      await this.submitPending();
    } catch (error) {
      this.submissionError(error, text);
    } finally {
      this.submitting.set(false);
    }
  }

  async retrySubmission() {
    if (!this.pending || this.submitting()) return;
    this.submitting.set(true);
    this.error.set(null);
    try {
      await this.submitPending();
    } catch (error) {
      this.submissionError(error, this.pending?.body.prompt ?? '');
    } finally {
      this.submitting.set(false);
    }
  }

  private async submitPending() {
    const run = await this.api.createRun(this.pending!.body, this.pending!.key);
    this.pending = null;
    this.pendingSubmission.set(false);
    this.draft.set({ text: '' });
    this.editing.set(null);
    this.liveRun.set(run);
    let snapshot = run;
    try {
      const detail = await this.api.conversation(run.conversationId);
      if (this.selected()?.id === run.conversationId) {
        this.selected.set(detail.conversation as Conversation);
        this.messages.set(detail.messages as Message[]);
      }
      snapshot = (detail.activeRun as Run) ?? run;
    } catch (error) {
      this.report(error);
    }
    void this.follow(snapshot);
    await this.refreshHistory().catch((error) => this.report(error));
  }

  private submissionError(error: unknown, text: string) {
    this.report(error);
    this.pendingSubmission.set(!!this.pending && error instanceof ApiError && error.status === 0);
    if (!this.pendingSubmission()) this.pending = null;
    if (text) this.draft.set({ text });
  }

  async regenerate(message: Message) {
    if (this.busy() || !message.parentId || !this.modelId()) return;
    this.submitting.set(true);
    this.error.set(null);
    this.pending = {
      key: crypto.randomUUID(),
      body: {
        conversationId: this.selected()!.id,
        modelId: this.modelId(),
        prompt: null,
        parentMessageId: null,
        regenerateUserMessageId: message.parentId,
        reasoningEffort: this.reasoningEffort(),
      },
    };
    try {
      await this.submitPending();
    } catch (error) {
      this.submissionError(error, '');
    } finally {
      this.submitting.set(false);
    }
  }

  edit(message: Message) {
    if (!this.busy()) {
      this.editing.set(message);
      this.draft.set({ text: message.content });
    }
  }
  cancelEdit() {
    this.editing.set(null);
    this.draft.set({ text: '' });
  }

  async selectVersion(message: Message, direction: number) {
    if (this.busy()) return;
    const all = this.messages();
    const versions = all.filter((x) => x.parentId === message.parentId && x.role === message.role);
    let leaf = versions[versions.findIndex((x) => x.id === message.id) + direction];
    if (!leaf) return;
    const seen = new Set<string>();
    while (!seen.has(leaf.id)) {
      seen.add(leaf.id);
      const children = all.filter((x) => x.parentId === leaf!.id);
      if (!children.length) break;
      leaf = children[children.length - 1];
    }
    try {
      await this.api.branch(this.selected()!.id, leaf.id);
      await this.select(this.selected()!.id, true);
      await this.refreshHistory();
    } catch (error) {
      this.report(error);
    }
  }

  async stop() {
    const run = this.liveRun();
    if (!run || this.stopping()) return;
    this.stopping.set(true);
    try {
      const final = await this.api.cancel(run.id);
      this.subscription?.abort();
      this.streamingText.set(final.content);
      await this.finish(final);
    } catch (error) {
      this.report(error);
    } finally {
      this.stopping.set(false);
    }
  }

  async resume() {
    const run = this.liveRun();
    if (!run) return;
    try {
      this.error.set(null);
      await this.follow(await this.api.run(run.id));
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
        this.report(error);
      }
    }
  }

  private async finish(run: Run) {
    if (this.selected()?.id === run.conversationId) {
      const detail = await this.api.conversation(run.conversationId);
      if (this.selected()?.id === run.conversationId) {
        this.selected.set(detail.conversation as Conversation);
        this.messages.set(detail.messages as Message[]);
      }
    }
    if (this.liveRun()?.id === run.id) this.liveRun.set(null);
    this.connection.set('connected');
    await this.refreshHistory();
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
    const version = ++this.preferenceVersion;
    this.themes.apply(value);
    if (!this.ready()) return;
    this.preferenceWrite = this.preferenceWrite.then(async () => {
      try {
        const saved = await this.api.preferences(value);
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

  async logout() {
    try {
      await this.auth.logout();
      this.subscription?.abort();
      this.initialized = null;
      this.pending = null;
      this.pendingSubmission.set(false);
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
    } catch (error) {
      this.report(error);
    }
  }
}
