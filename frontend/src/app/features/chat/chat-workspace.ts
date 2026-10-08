import { CompactDialog } from '../../shared/ui/compact-dialog';
import { Field } from '../../shared/ui/field';
import { Card } from '../../shared/ui/card';
import { IssueCode } from '../../shared/ui/issue-code';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  afterRenderEffect,
  DestroyRef,
  effect,
  ElementRef,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  FormField,
  form,
  maxLength,
  required,
  readonly as readonlyField,
} from '@angular/forms/signals';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { WorkspaceLayout } from '../../core/preferences/workspace-layout';
import { ThemeService } from '../../core/preferences/theme-service';
import type { Conversation, ConversationSettings } from '../../core/api/types';
import { Icon } from '../../shared/ui/icon';
import { InferenceSignal } from '../../shared/ui/inference-signal';
import { generationStatus } from '../../core/api/generation-status';
import { Autosize } from '../../shared/browser/autosize';
import { FileDrop } from '../../shared/browser/file-drop';
import { downloadFile } from '../../shared/browser/download';
import { Command, CommandPalette } from '../../shared/ui/command-palette';
import { AttachmentList } from '../attachments/attachment-list';
import { ConversationAction, ConversationActions } from '../workspace/conversation-actions';
import { ConversationSettingsDialog } from '../workspace/conversation-settings-dialog';
import { PromptLibraryDialog } from '../workspace/prompt-library-dialog';
import { ChatMessage } from './chat-message';
import { ChatSidebar } from './chat-sidebar';
import { ChatStore } from './chat-store';
import { ComposerControls } from './composer-controls';
import { ConversationFind } from './conversation-find';
import { InlineTitle } from '../../shared/ui/inline-title';
import { ConversationOutline } from './conversation-outline';
import { KnowledgePicker } from '../knowledge/knowledge-picker';
import { TextSelection, type SelectedText } from '../../shared/browser/text-selection';
import { TextTools } from '../artifacts/text-tools';
import { ShareDialog } from '../sharing/share-dialog';
import { ConversationDraftTransfer } from '../../core/preferences/conversation-draft-transfer';
import { FocusComposer } from './focus-composer';
import { isSubmitKey } from '../../shared/browser/submit-key';
import { ConversationSpendView } from '../billing/conversation-spend';
import { ReaderNavigation, type ReaderOrigin } from '../../shared/browser/reader-navigation';
import { LibraryPicker } from '../files/library-picker';

@Component({
  selector: 'nx-chat-workspace',
  imports: [
    Card,
    CompactDialog,
    Field,
    IssueCode,
    FormField,
    RouterLink,
    Icon,
    ChatMessage,
    ChatSidebar,
    ComposerControls,
    InferenceSignal,
    Autosize,
    FileDrop,
    AttachmentList,
    ConversationActions,
    ConversationSettingsDialog,
    PromptLibraryDialog,
    CommandPalette,
    ConversationFind,
    InlineTitle,
    ConversationOutline,
    KnowledgePicker,
    TextSelection,
    TextTools,
    ShareDialog,
    FocusComposer,
    ConversationSpendView,
    LibraryPicker,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './chat-workspace.html',
})
export class ChatWorkspace {
  readonly store = inject(ChatStore);
  readonly starting = computed(
    () => !this.store.visibleMessages().length && !this.store.loadingConversation(),
  );
  readonly themes = inject(ThemeService);
  private readonly destroy = inject(DestroyRef);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly readerNavigation = inject(ReaderNavigation);
  private readonly returnPosition = signal<ReaderOrigin | null>(null);
  private readonly transfer = inject(ConversationDraftTransfer);
  private navigationSequence = 0;
  readonly layout = inject(WorkspaceLayout);
  readonly sidebarOpen = computed(() => !this.layout.compact());
  readonly narrow = this.layout.narrow;
  readonly following = signal(true);
  readonly composing = signal(false);
  readonly findOpen = signal(false);
  readonly matches = signal<string[]>([]);
  readonly currentMatch = signal<string | null>(null);
  readonly paragraph = signal<SelectedText | null>(null);
  readonly textTools = viewChild(TextTools);
  readonly focusComposer = viewChild.required(FocusComposer);
  readonly shareDialog = viewChild(ShareDialog);
  readonly hasSharing = computed(
    () => this.store.me()?.access.features.some((x) => x.id === 'shared') ?? false,
  );
  paragraphAction(action: string) {
    const selected = this.paragraph();
    if (!selected) return;
    this.textTools()?.open(selected.text, action, selected.sourceId);
    this.paragraph.set(null);
    window.getSelection()?.removeAllRanges();
  }
  readonly shareTarget = signal<Conversation | null>(null);
  readonly modal = signal<'rename' | 'delete' | null>(null);
  readonly modalTarget = signal<Conversation | null>(null);
  readonly modalBusy = signal(false);
  readonly composerForm = form(this.store.draft, (schema) => {
    readonlyField(schema.text, {
      when: () =>
        this.store.loading() ||
        this.store.loadingConversation() ||
        !!this.store.selected()?.isArchived ||
        this.store.submitting() ||
        this.store.pendingSubmission(),
    });
    required(schema.text, { message: '請輸入訊息。' });
    maxLength(schema.text, () => this.store.policy().maxInputCharacters, {
      message: '訊息超過系統允許的字數，請縮短內容。',
    });
  });
  readonly renameModel = signal({ title: '' });
  readonly renameForm = form(this.renameModel, (schema) => {
    required(schema.title);
    maxLength(schema.title, 120);
  });
  readonly textarea = viewChild<ElementRef<HTMLTextAreaElement>>('composer');
  readonly viewport = viewChild<ElementRef<HTMLElement>>('viewport');
  readonly dialog = viewChild<ElementRef<HTMLDialogElement>>('dialog');
  readonly library = viewChild(PromptLibraryDialog);
  readonly settings = viewChild(ConversationSettingsDialog);
  readonly palette = viewChild(CommandPalette);
  readonly finder = viewChild(ConversationFind);
  readonly importInput = viewChild<ElementRef<HTMLInputElement>>('importInput');
  readonly saveSettings = (conversation: Conversation, settings: ConversationSettings) =>
    this.store.organize(conversation, settings);
  readonly renameCurrent = (title: string) => {
    const conversation = this.store.selected();
    return conversation ? this.store.rename(conversation.id, title) : Promise.resolve(false);
  };
  readonly statusText = computed(() =>
    this.store.stopping()
      ? '正在停止…'
      : this.store.connection() === 'reconnecting'
        ? '連線中斷，正在恢復…'
        : (generationStatus(this.store.liveRun()?.status ?? '', !!this.store.streamingText())
            ?.label ??
          (this.store.submitting()
            ? this.store.webSearchEnabled()
              ? '正在搜尋網路並準備回答…'
              : '正在提交訊息…'
            : '')),
  );
  readonly suggestions = [
    {
      icon: 'lines',
      title: '整理思緒',
      detail: '讓重點與下一步更清楚',
      text: '幫我整理以下筆記，歸納重點與待辦事項：\n',
    },
    {
      icon: 'document',
      title: '寫得更精準',
      detail: '打磨文字，保留你的觀點',
      text: '幫我修改以下文字，讓語氣清楚、自然且專業：\n',
    },
    {
      icon: 'idea',
      title: '拆解問題',
      detail: '從複雜問題找到可行方向',
      text: '和我一起分析以下問題，列出可行方向與需要確認的資訊：\n',
    },
  ];
  readonly commands = computed<Command[]>(() => [
    {
      id: 'new',
      label: '開始新對話',
      detail: '建立新的工作思路',
      icon: 'plus',
      shortcut: 'Ctrl Alt N',
    },
    {
      id: 'prompts',
      label: '開啟常用提示詞',
      detail: '套用或整理個人範本',
      icon: 'library',
      shortcut: 'Ctrl Shift L',
    },
    {
      id: 'import',
      label: '匯入對話文字備份',
      detail: '從 AI Nexus JSON 備份還原',
      icon: 'upload',
    },
    {
      id: 'theme',
      label: this.themes.preferences().theme === 'dark' ? '切換淺色外觀' : '切換深色外觀',
      detail: '依目前工作環境調整',
      icon: 'sliders',
    },
    ...(this.store.selected()
      ? [
          { id: 'find', label: '搜尋目前對話訊息', detail: '定位分支中的文字', icon: 'search' },
          {
            id: 'settings',
            label: '編輯對話指令與標籤',
            detail: '設定回答方式並分類',
            icon: 'tag',
          },
          { id: 'duplicate', label: '建立對話副本', detail: '保留所有分支與附件', icon: 'copy' },
        ]
      : []),
    ...this.store.conversations().map((conversation) => ({
      id: 'conversation:' + conversation.id,
      label: conversation.title,
      detail: conversation.isArchived ? '封存對話' : '開啟對話',
      icon: 'document',
    })),
  ]);
  private scrollFrame = 0;
  private lastScrollTop = 0;

  constructor() {
    const keyboard = (event: KeyboardEvent) => {
      if (event.isComposing || event.repeat) return;
      if (
        event.key === 'Escape' &&
        this.narrow() &&
        this.sidebarOpen() &&
        !document.querySelector('dialog[open]')
      )
        this.closeSidebar();
      if (!(event.ctrlKey || event.metaKey) || document.querySelector('dialog[open]')) return;
      if (event.key.toLowerCase() === 'k') {
        event.preventDefault();
        this.palette()?.open();
      } else if (event.altKey && event.key.toLowerCase() === 'n') {
        event.preventDefault();
        void this.router.navigate(['/chat']);
      } else if (event.shiftKey && event.key.toLowerCase() === 'l') {
        event.preventDefault();
        void this.library()?.open();
      }
    };
    document.addEventListener('keydown', keyboard);
    this.destroy.onDestroy(() => this.navigationSequence++);
    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      const navigation = ++this.navigationSequence;
      const position = this.readerNavigation.takeReturn(this.router.url);
      this.following.set(position ? false : this.store.personal.value().autoFollow);
      void this.store.initialize().then(() => {
        if (navigation === this.navigationSequence && this.store.ready())
          void this.store.select(params.get('id')).then(() => {
            if (
              navigation !== this.navigationSequence ||
              this.store.selected()?.id !== params.get('id')
            )
              return;
            const prompt = this.transfer.take(params.get('id'));
            if (prompt) {
              this.store.draft.set({ text: prompt });
            }
            this.returnPosition.set(position);
          });
      });
      this.closeFind();
    });
    afterRenderEffect(() => {
      const position = this.returnPosition(),
        view = this.viewport()?.nativeElement;
      if (!position || !view || this.store.loadingConversation()) return;
      view.scrollTop = position.scrollTop;
      this.lastScrollTop = view.scrollTop;
      const link = Array.from(view.querySelectorAll<HTMLAnchorElement>('a[data-reader-id]')).find(
        (element) =>
          element.dataset['readerId'] === position.documentId &&
          (!position.messageId ||
            element.closest<HTMLElement>('[data-message-id]')?.dataset['messageId'] ===
              position.messageId),
      );
      link?.focus({ preventScroll: true });
      this.returnPosition.set(null);
    });
    effect((onCleanup) => {
      const ready = this.store.ready(),
        modelId = this.store.modelId(),
        conversation = this.store.selected(),
        edit = this.store.editing(),
        prompt = this.store.draft().text;
      const attachmentIds = this.store.attachments.files().map((file) => file.id);
      const webSearch = this.store.webSearchEnabled();
      this.store.messages();
      this.store.knowledge.revision();
      this.store.contextUsage.set(null);
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
              attachmentIds,
              webSearch,
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
      this.scheduleFollow();
    });
    this.destroy.onDestroy(() => {
      document.removeEventListener('keydown', keyboard);
      cancelAnimationFrame(this.scrollFrame);
    });
  }
  pickSuggestion(text: string) {
    this.store.draft.set({ text });
    this.textarea()?.nativeElement.focus();
  }
  scheduleFollow() {
    if (!this.following() || this.scrollFrame) return;
    this.scrollFrame = requestAnimationFrame(() => {
      this.scrollFrame = 0;
      if (this.following()) this.scrollLatest(false);
    });
  }
  keydown(event: KeyboardEvent) {
    if (!isSubmitKey(event, this.composing(), this.store.personal.value().enterToSend)) return;
    event.preventDefault();
    this.submitDraft();
  }
  send(event: Event) {
    event.preventDefault();
    this.submitDraft();
  }
  submitDraft() {
    if (this.store.canSend() && !this.composerForm.text().invalid()) {
      this.following.set(this.store.personal.value().autoFollow);
      void this.store.send();
    }
  }
  onScroll() {
    this.paragraph.set(null);
    const view = this.viewport()?.nativeElement;
    if (view) {
      const near = view.scrollHeight - view.scrollTop - view.clientHeight < 96;
      // A smooth jump upwards begins near the bottom; that first event must not restart auto-follow.
      this.following.set(near && (this.following() || view.scrollTop > this.lastScrollTop));
      this.lastScrollTop = view.scrollTop;
      if (!near) {
        cancelAnimationFrame(this.scrollFrame);
        this.scrollFrame = 0;
      }
    }
  }
  pauseFollowing() {
    this.following.set(false);
    cancelAnimationFrame(this.scrollFrame);
    this.scrollFrame = 0;
    this.lastScrollTop = this.viewport()?.nativeElement.scrollTop ?? 0;
  }
  scrollLatest(smooth = true) {
    const view = this.viewport()?.nativeElement;
    if (!view) return;
    this.following.set(true);
    view.scrollTo({
      top: view.scrollHeight,
      behavior: smooth && !this.themes.reducedMotion() ? 'smooth' : 'instant',
    });
  }
  closeMobileSidebar() {
    this.layout.closeMobile();
  }
  closeSidebar() {
    const previous = document.activeElement;
    this.layout.compact.set(true);
    if (this.narrow())
      requestAnimationFrame(() => {
        const active = document.activeElement;
        if (active === previous || active === document.body || active?.closest('.sidebar'))
          document.querySelector<HTMLButtonElement>('.sidebar-toggle')?.focus();
      });
  }
  newChat() {
    this.closeMobileSidebar();
  }
  editMessageFocus() {
    requestAnimationFrame(() => this.textarea()?.nativeElement.focus());
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
  selectModel(id: string) {
    this.store.chooseModel(id);
    void this.store.savePreferences({
      ...this.themes.preferences(),
      defaultModelId: this.store.modelId() || null,
    });
  }
  exportConversation() {
    const conversation = this.store.selected();
    if (!conversation || this.store.busy()) return;
    const content = [
      '# ' + conversation.title,
      ...this.store
        .visibleMessages()
        .map(
          (message) =>
            `## ${message.role === 'user' ? '你' : 'AI Nexus'}\n\n${message.content}${message.attachments?.length ? '\n\n附件：' + message.attachments.map((file) => file.fileName).join('、') : ''}`,
        ),
    ].join('\n\n');
    downloadFile(content, conversation.title, 'md');
  }
  async reload() {
    await this.store.initialize(true);
    if (this.store.ready()) await this.store.select(this.route.snapshot.paramMap.get('id'), true);
  }
  action(action: ConversationAction) {
    const conversation = this.store.selected();
    if (conversation) this.conversationAction(action, conversation);
  }
  conversationAction(action: ConversationAction, conversation: Conversation) {
    switch (action) {
      case 'share':
        this.shareTarget.set(conversation);
        requestAnimationFrame(() => this.shareDialog()?.open());
        break;
      case 'rename':
      case 'delete':
        this.openDialog(action, conversation);
        break;
      case 'markdown':
        this.exportConversation();
        break;
      case 'backup':
        void this.store.exportBackup();
        break;
      case 'settings':
        this.settings()?.open(conversation);
        break;
      case 'duplicate':
        void this.store.duplicate();
        break;
      case 'favorite':
        void this.store.organize(conversation, { isFavorite: !conversation.isFavorite });
        break;
      case 'archive':
        void this.store.organize(conversation, { isArchived: !conversation.isArchived });
        break;
      case 'find':
        this.findOpen.set(true);
        requestAnimationFrame(() => this.finder()?.focus());
        break;
    }
  }
  command(id: string) {
    this.closeMobileSidebar();
    if (id.startsWith('conversation:')) {
      void this.router.navigate(['/chat', id.slice(13)]);
      return;
    }
    if (id === 'new') void this.router.navigate(['/chat']);
    else if (id === 'prompts') void this.library()?.open();
    else if (id === 'import') this.importInput()?.nativeElement.click();
    else if (id === 'theme')
      void this.store.savePreferences({
        ...this.themes.preferences(),
        theme: this.themes.preferences().theme === 'dark' ? 'light' : 'dark',
      });
    else this.action(id as ConversationAction);
  }
  find(result: { ids: string[]; active: string | null }) {
    this.matches.set(result.ids);
    this.currentMatch.set(result.active);
    if (result.active) {
      this.following.set(false);
      requestAnimationFrame(() =>
        this.viewport()
          ?.nativeElement.querySelector<HTMLElement>(
            `[data-message-id="${CSS.escape(result.active!)}"]`,
          )
          ?.scrollIntoView({
            block: 'center',
            behavior: this.themes.reducedMotion() ? 'instant' : 'smooth',
          }),
      );
    }
  }
  closeFind() {
    this.paragraph.set(null);
    this.findOpen.set(false);
    this.matches.set([]);
    this.currentMatch.set(null);
  }
  uploadChanged(event: Event) {
    const input = event.target as HTMLInputElement;
    if (input.files) void this.store.attachments.upload(input.files);
    input.value = '';
  }
  importChanged(event: Event) {
    const input = event.target as HTMLInputElement;
    if (input.files?.[0]) void this.store.importBackup(input.files[0]);
    input.value = '';
  }
}
