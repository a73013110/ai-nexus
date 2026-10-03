import {
  afterNextRender,
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  effect,
  ElementRef,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormField, form, maxLength, required } from '@angular/forms/signals';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ThemeService } from '../../core/preferences/theme-service';
import type { Conversation } from '../../core/api/types';
import { Icon } from '../../shared/ui/icon';
import { ChatMessage } from './chat-message';
import { ChatStore } from './chat-store';
import { ComposerControls } from './composer-controls';
import { InferenceSignal } from '../../shared/ui/inference-signal';

@Component({
  selector: 'nx-chat-workspace',
  imports: [FormField, RouterLink, Icon, ChatMessage, ComposerControls, InferenceSignal],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './chat-workspace.html',
})
export class ChatWorkspace {
  readonly store = inject(ChatStore);
  readonly themes = inject(ThemeService);
  private readonly destroy = inject(DestroyRef);
  private readonly route = inject(ActivatedRoute);
  readonly sidebarOpen = signal(window.innerWidth >= 860);
  readonly narrow = signal(window.innerWidth < 860);
  readonly following = signal(true);
  readonly composing = signal(false);
  readonly modal = signal<'rename' | 'delete' | null>(null);
  readonly modalTarget = signal<Conversation | null>(null);
  readonly modalBusy = signal(false);
  readonly searchModel = signal({ query: '' });
  readonly searchForm = form(this.searchModel, (schema) => maxLength(schema.query, 120));
  readonly composerForm = form(this.store.draft, (schema) => {
    required(schema.text, { message: '請輸入訊息。' });
    maxLength(schema.text, 12000, { message: '訊息最多 12,000 個字元。' });
  });
  readonly renameModel = signal({ title: '' });
  readonly renameForm = form(this.renameModel, (schema) => {
    required(schema.title);
    maxLength(schema.title, 120);
  });
  readonly preferencesForm = form(this.themes.preferences);
  readonly textarea = viewChild<ElementRef<HTMLTextAreaElement>>('composer');
  readonly viewport = viewChild<ElementRef<HTMLElement>>('viewport');
  readonly dialog = viewChild<ElementRef<HTMLDialogElement>>('dialog');
  readonly groups = computed(() => {
    const now = new Date();
    const day = new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Taipei' });
    const today = day.format(now),
      yesterday = day.format(new Date(now.getTime() - 86400000));
    const groups = new Map<string, Conversation[]>();
    for (const conversation of this.store.conversations()) {
      const date = day.format(new Date(conversation.updatedAt));
      const label = date === today ? '今天' : date === yesterday ? '昨天' : '先前的對話';
      groups.set(label, [...(groups.get(label) ?? []), conversation]);
    }
    return [...groups].map(([label, conversations]) => ({ label, conversations }));
  });
  readonly statusText = computed(() =>
    this.store.stopping()
      ? '正在停止…'
      : this.store.connection() === 'reconnecting'
        ? '連線中斷，正在恢復…'
        : this.store.liveRun()?.status === 'queued'
          ? '等待模型 · 已加入佇列'
          : this.store.liveRun()?.status === 'running'
            ? '模型正在回答'
            : this.store.submitting()
              ? '正在提交訊息…'
              : '',
  );
  readonly suggestions = [
    { icon: 'lines', title: '整理思緒', text: '幫我整理以下筆記，歸納重點與待辦事項：\n' },
    { icon: 'document', title: '寫得更精準', text: '幫我修改以下文字，讓語氣清楚、自然且專業：\n' },
    {
      icon: 'idea',
      title: '拆解問題',
      text: '和我一起分析以下問題，列出可行方向與需要確認的資訊：\n',
    },
  ];
  private searchTimer: ReturnType<typeof setTimeout> | null = null;
  private scrollFrame = 0;

  constructor() {
    const resize = () => this.narrow.set(window.innerWidth < 860);
    const escape = (event: KeyboardEvent) => {
      if (event.key === 'Escape' && this.narrow() && this.sidebarOpen()) this.closeSidebar();
    };
    window.addEventListener('resize', resize);
    document.addEventListener('keydown', escape);
    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      void this.store.initialize().then(() => {
        if (this.store.ready()) void this.store.select(params.get('id'));
      });
      this.following.set(true);
    });
    effect(() => {
      this.store.draft();
      requestAnimationFrame(() => this.resizeComposer());
    });
    effect((onCleanup) => {
      const ready = this.store.ready();
      const modelId = this.store.modelId();
      const conversation = this.store.selected();
      const edit = this.store.editing();
      const prompt = this.store.draft().text;
      this.store.messages();
      if (!ready || !modelId) return;
      const controller = new AbortController();
      const timer = setTimeout(
        () =>
          void this.store.previewContext(
            {
              conversationId: conversation?.id ?? null,
              parentMessageId: edit ? edit.parentId : (conversation?.activeLeafId ?? null),
              prompt,
              modelId,
            },
            controller.signal,
          ),
        350,
      );
      onCleanup(() => {
        clearTimeout(timer);
        controller.abort();
      });
    });
    effect(() => {
      this.store.streamingText();
      this.store.visibleMessages();
      if (this.following()) {
        cancelAnimationFrame(this.scrollFrame);
        this.scrollFrame = requestAnimationFrame(() => {
          if (this.following()) this.scrollLatest(false);
        });
      }
    });
    this.destroy.onDestroy(() => {
      window.removeEventListener('resize', resize);
      document.removeEventListener('keydown', escape);
      if (this.searchTimer) clearTimeout(this.searchTimer);
      cancelAnimationFrame(this.scrollFrame);
    });
    afterNextRender(() => this.resizeComposer());
  }
  searchChanged() {
    if (this.searchTimer) clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => {
      void this.store.searchHistory(this.searchModel().query);
    }, 250);
  }
  pickSuggestion(text: string) {
    this.store.draft.set({ text });
    this.textarea()?.nativeElement.focus();
  }
  keydown(event: KeyboardEvent) {
    if (
      event.key !== 'Enter' ||
      event.shiftKey ||
      event.isComposing ||
      this.composing() ||
      event.keyCode === 229
    )
      return;
    event.preventDefault();
    if (
      this.store.canSend() &&
      this.store.draft().text.trim() &&
      !this.composerForm.text().invalid()
    ) {
      this.following.set(true);
      void this.store.send();
    }
  }
  send(event: Event) {
    event.preventDefault();
    if (this.store.canSend() && !this.composerForm.text().invalid()) {
      this.following.set(true);
      void this.store.send();
    }
  }
  resizeComposer() {
    const field = this.textarea()?.nativeElement;
    if (!field) return;
    field.style.height = 'auto';
    const limit = Math.min(192, window.innerHeight * 0.25);
    field.style.height = `${Math.min(field.scrollHeight, limit)}px`;
  }
  onScroll() {
    const view = this.viewport()?.nativeElement;
    if (view) {
      const near = view.scrollHeight - view.scrollTop - view.clientHeight < 96;
      this.following.set(near);
      if (!near) cancelAnimationFrame(this.scrollFrame);
    }
  }
  scrollLatest(smooth = true) {
    const view = this.viewport()?.nativeElement;
    if (!view) return;
    this.following.set(true);
    const reduce =
      this.themes.preferences().reducedMotion ||
      window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    view.scrollTo({ top: view.scrollHeight, behavior: smooth && !reduce ? 'smooth' : 'instant' });
  }
  closeMobileSidebar() {
    if (this.narrow()) this.sidebarOpen.set(false);
  }
  closeSidebar() {
    const previousFocus = document.activeElement;
    this.sidebarOpen.set(false);
    if (this.narrow())
      requestAnimationFrame(() => {
        const active = document.activeElement;
        if (active === previousFocus || active === document.body || active?.closest('.sidebar')) {
          document.querySelector<HTMLButtonElement>('.sidebar-toggle')?.focus();
        }
      });
  }
  newChat() {
    this.store.cancelEdit();
    this.closeMobileSidebar();
  }
  editMessageFocus() {
    setTimeout(() => this.textarea()?.nativeElement.focus(), 0);
  }
  openDialog(type: 'rename' | 'delete', target: Conversation) {
    this.modalTarget.set(target);
    this.modal.set(type);
    this.renameModel.set({ title: target.title });
    this.dialog()?.nativeElement.showModal();
  }
  closeDialog() {
    this.dialog()?.nativeElement.close();
    this.modal.set(null);
  }
  async confirmDialog(event: Event) {
    event.preventDefault();
    const target = this.modalTarget();
    if (!target || this.modalBusy()) return;
    if (
      this.modal() === 'rename' &&
      (!this.renameModel().title.trim() || this.renameForm.title().invalid())
    )
      return;
    this.modalBusy.set(true);
    const success =
      this.modal() === 'rename'
        ? await this.store.rename(target.id, this.renameModel().title)
        : await this.store.remove(target.id);
    this.modalBusy.set(false);
    if (success) this.closeDialog();
  }
  savePreferences() {
    queueMicrotask(
      () =>
        void this.store.savePreferences({
          ...this.themes.preferences(),
          defaultModelId: this.store.policy().allowModelSelection
            ? this.store.modelId() || null
            : null,
        }),
    );
  }
  selectModel(id: string) {
    this.store.chooseModel(id);
    this.savePreferences();
  }
  exportConversation() {
    const conversation = this.store.selected();
    if (!conversation || this.store.busy()) return;
    const content = [
      '# ' + conversation.title,
      ...this.store
        .visibleMessages()
        .map(
          (message) => `## ${message.role === 'user' ? '你' : 'AI Nexus'}\n\n${message.content}`,
        ),
    ].join('\n\n');
    const url = URL.createObjectURL(new Blob([content], { type: 'text/markdown;charset=utf-8' }));
    const link = document.createElement('a');
    link.href = url;
    link.download =
      (conversation.title.replace(/[\\/:*?"<>|\u0000-\u001f]/g, '_').slice(0, 80) || 'AI-Nexus') +
      '.md';
    link.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  }
  async loadMore() {
    try {
      await this.store.refreshHistory(true);
    } catch (error) {
      this.store.report(error);
    }
  }
  toggleSidebar() {
    this.sidebarOpen.update((value) => !value);
    if (this.narrow() && this.sidebarOpen())
      requestAnimationFrame(() =>
        document.querySelector<HTMLAnchorElement>('.sidebar .brand')?.focus(),
      );
  }
  async reload() {
    await this.store.initialize(true);
    if (this.store.ready()) await this.store.select(this.route.snapshot.paramMap.get('id'), true);
  }
}
