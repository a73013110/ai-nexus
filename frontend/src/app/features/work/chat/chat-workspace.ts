import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import type { ConversationDto, ConversationSettingsRequest } from '@core/api/schema';
import { SidebarPanelContent } from '@core/layout/sidebar-panel';
import { WorkspaceCommands } from '@core/layout/workspace-commands';
import { WorkspaceLayout } from '@core/layout/workspace-layout';
import { ConversationDraftTransfer } from '@core/preferences/conversation-draft-transfer';
import { ThemeService } from '@core/preferences/theme-service';
import { downloadFile } from '@shared/browser/download';
import { FileDrop } from '@shared/browser/file-drop';
import { bindHotkeys } from '@shared/browser/hotkeys';
import { ReaderNavigation, type ReaderOrigin } from '@shared/browser/reader-navigation';
import type { Command } from '@shared/ui/command-palette';
import { Icon } from '@shared/ui/icon';
import { Notice } from '@shared/ui/notice';
import { ProductTour } from '@shared/ui/product-tour';
import { ShareDialog } from '@features/work/sharing/share-dialog';
import type { ConversationAction } from '@features/work/conversations/conversation-actions';
import { ConversationSettingsDialog } from '@features/work/conversations/conversation-settings-dialog';
import { PromptLibraryDialog } from '@features/work/prompts/prompt-library-dialog';
import { ChatComposer } from './composer/chat-composer';
import { ChatConversations } from './state/chat-conversations';
import { ChatSidebar } from './sidebar/chat-sidebar';
import { ChatStore } from './chat-store';
import { ChatThread } from './thread/chat-thread';
import { ChatTopbar } from './chat-topbar';
import { chatTour } from './chat-tour';
import { ConversationDialog } from './conversation-dialog';
import { ConversationFind } from './thread/conversation-find';
import { conversationMarkdown } from './conversation-markdown';

/**
 * 聊天頁：開啟路由中的對話，並把指令、快捷鍵與選單動作
 * 送到負責執行它們的頂部列、對話串、輸入框與對話框。
 */
@Component({
  selector: 'nx-chat-workspace',
  styleUrl: './chat-workspace.scss',
  imports: [
    ChatComposer,
    ChatSidebar,
    ChatThread,
    ChatTopbar,
    ConversationDialog,
    ConversationFind,
    ConversationSettingsDialog,
    FileDrop,
    Icon,
    Notice,
    PromptLibraryDialog,
    ShareDialog,
    SidebarPanelContent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './chat-workspace.html',
})
export class ChatWorkspace {
  readonly store = inject(ChatStore);
  readonly layout = inject(WorkspaceLayout);
  private readonly conversations = inject(ChatConversations);
  private readonly themes = inject(ThemeService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly readerNavigation = inject(ReaderNavigation);
  private readonly transfer = inject(ConversationDraftTransfer);
  private readonly tours = inject(ProductTour);
  readonly tour = computed(() => chatTour(this.layout.narrow(), this.store.hasKnowledgeAccess()));
  readonly findOpen = signal(false);
  readonly matches = signal<string[]>([]);
  readonly currentMatch = signal<string | null>(null);
  readonly shareTarget = signal<ConversationDto | null>(null);
  readonly arrival = signal<{ position: ReaderOrigin | null }>({ position: null });
  readonly restoreTo = signal<ReaderOrigin | null>(null);
  readonly thread = viewChild.required(ChatThread);
  readonly composer = viewChild.required(ChatComposer);
  readonly library = viewChild.required(PromptLibraryDialog);
  readonly palette = inject(WorkspaceCommands);
  private readonly settings = viewChild.required(ConversationSettingsDialog);
  private readonly dialog = viewChild.required(ConversationDialog);
  private readonly finder = viewChild(ConversationFind);
  private readonly shareDialog = viewChild(ShareDialog);
  private navigationSequence = 0;
  readonly saveSettings = (conversation: ConversationDto, settings: ConversationSettingsRequest) =>
    this.conversations.organize(conversation, settings);
  readonly assignProject = (conversation: ConversationDto, projectId: string | null) =>
    this.conversations.assignProject(conversation, projectId);
  private readonly actions = computed<Command[]>(() => [
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
  ]);
  private readonly entries = computed<Command[]>(() =>
    this.store.conversations().map((conversation) => ({
      id: 'conversation:' + conversation.id,
      label: conversation.title,
      detail: conversation.isArchived ? '封存對話' : '開啟對話',
      icon: 'chat',
    })),
  );

  constructor() {
    this.palette.provide({
      actions: this.actions,
      entries: this.entries,
      run: (id) => this.command(id),
    });
    bindHotkeys(
      {
        'mod+alt+n': () => void this.router.navigate(['/chat']),
        'mod+shift+l': () => void this.library().open(),
      },
      () =>
        !this.tours.active() && !this.tours.loading() && !document.querySelector('dialog[open]'),
    );
    inject(DestroyRef).onDestroy(() => this.navigationSequence++);
    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      const navigation = ++this.navigationSequence;
      const id = params.get('id');
      const position = this.readerNavigation.takeReturn(this.router.url);
      this.closeFind();
      this.arrival.set({ position });
      this.restoreTo.set(null);
      void this.store.initialize().then(() => {
        if (navigation !== this.navigationSequence || !this.store.ready()) return;
        void this.store.select(id).then(() => {
          if (navigation !== this.navigationSequence || this.store.selected()?.id !== id) return;
          const draft = this.transfer.take(id);
          if (draft) {
            this.store.draft.set({ text: draft.text });
            if (draft.attachments.length) this.store.attachments.reset(draft.attachments);
          }
          this.restoreTo.set(position);
        });
      });
    });
  }

  async reload() {
    await this.store.initialize(true);
    if (this.store.ready()) await this.store.select(this.route.snapshot.paramMap.get('id'), true);
  }
  action(action: ConversationAction) {
    const conversation = this.store.selected();
    if (conversation) this.conversationAction(action, conversation);
  }
  conversationAction(action: ConversationAction, conversation: ConversationDto) {
    switch (action) {
      case 'share':
        this.shareTarget.set(conversation);
        requestAnimationFrame(() => this.shareDialog()?.open());
        break;
      case 'rename':
      case 'delete':
        this.dialog().open(action, conversation);
        break;
      case 'markdown':
        if (!this.store.busy())
          downloadFile(
            conversationMarkdown(conversation.title, this.store.visibleMessages()),
            conversation.title,
            'md',
          );
        break;
      case 'backup':
        void this.conversations.exportBackup();
        break;
      case 'settings':
        this.settings().open(conversation);
        break;
      case 'duplicate':
        void this.conversations.duplicate();
        break;
      case 'favorite':
        void this.conversations.organize(conversation, { isFavorite: !conversation.isFavorite });
        break;
      case 'archive':
        void this.conversations.organize(conversation, { isArchived: !conversation.isArchived });
        break;
      case 'find':
        this.findOpen.set(true);
        requestAnimationFrame(() => this.finder()?.focus());
        break;
    }
  }
  command(id: string) {
    if (id.startsWith('conversation:')) void this.router.navigate(['/chat', id.slice(13)]);
    else if (id === 'new') void this.router.navigate(['/chat']);
    else if (id === 'prompts') void this.library().open();
    else if (id === 'import') void this.conversations.chooseBackup();
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
    if (result.active) this.thread().reveal(result.active);
  }
  closeFind() {
    this.findOpen.set(false);
    this.matches.set([]);
    this.currentMatch.set(null);
  }
}
