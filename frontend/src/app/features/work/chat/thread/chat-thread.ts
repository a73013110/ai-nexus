import {
  afterRenderEffect,
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  ElementRef,
  inject,
  input,
  linkedSignal,
  output,
  viewChild,
} from '@angular/core';
import type { MessageDto } from '@core/api/schema';
import { ThemeService } from '@core/preferences/theme-service';
import { followScroll } from '@shared/browser/follow-scroll';
import type { ReaderOrigin } from '@shared/browser/reader-navigation';
import { TextSelection, type SelectedText } from '@shared/browser/text-selection';
import { Icon } from '@shared/ui/icon';
import { TextSelectionToolbar } from '@features/work/artifacts/text-selection-toolbar';
import { TextTools } from '@features/work/artifacts/text-tools';
import { ChatMessage } from './chat-message';
import { ChatStore } from '../chat-store';
import { ConversationOutline } from './conversation-outline';

/**
 * 可捲動的訊息列表。讀者停在底部時會跟隨新輸出；往上捲時暫停，
 * 造訪文件閱讀器回來後，會回到先前儲存的位置。
 */
@Component({
  selector: 'div[nxChatThread]',
  imports: [ChatMessage, ConversationOutline, Icon, TextSelection, TextSelectionToolbar, TextTools],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrls: ['../chat-column.scss', './chat-thread.scss'],
  templateUrl: './chat-thread.html',
})
export class ChatThread {
  readonly store = inject(ChatStore);
  private readonly themes = inject(ThemeService);
  readonly matches = input<string[]>([]);
  readonly currentMatch = input<string | null>(null);
  /** 每次導覽都會設定；已儲存的閱讀位置優先於自動跟隨。 */
  readonly arrival = input<{ position: ReaderOrigin | null }>({ position: null });
  /** 對話載入後設定一次，用來捲回讀者離開的位置。 */
  readonly restoreTo = input<ReaderOrigin | null>(null);
  /** 提問已打開進行編輯，因此輸入框應取得焦點。 */
  readonly edited = output<void>();
  private readonly viewport = viewChild.required<ElementRef<HTMLElement>>('viewport');
  readonly scroll = followScroll({
    viewport: () => this.viewport().nativeElement,
    initial: () => !this.arrival().position && this.store.personal.value().autoFollow,
  });
  readonly following = this.scroll.following;
  readonly paragraph = linkedSignal<unknown, SelectedText | null>({
    source: this.arrival,
    computation: () => null,
  });
  /** 正在產生的回答；只有它的列會讀取串流文字。 */
  readonly activeId = computed(() => this.store.liveRun()?.assistantMessageId ?? null);
  readonly runStatus = computed(() => this.store.liveRun()?.status ?? '');
  readonly matched = computed(() => new Set(this.matches()));
  /**
   * 對話顯示之後才抵達的訊息（已送出的提問、新回答）會有進場動畫。開啟對話或其他版本時
   * 會一次渲染全部；已送出提問在伺服器上的副本，會取代已顯示的那則，而且不會再次進場。
   */
  readonly entering = linkedSignal<EnteringSource, ReadonlySet<string>>({
    source: () => ({
      messages: this.store.visibleMessages(),
      loading: this.store.loadingConversation(),
      conversation: this.store.selected()?.id ?? null,
    }),
    computation: (current, previous) => {
      const before = previous?.source;
      if (
        !before ||
        current.loading ||
        before.loading ||
        before.conversation !== current.conversation
      )
        return none;
      const shown = new Set(before.messages.map((message) => message.id));
      const replacing = before.messages.some((message) => message.id.startsWith(pendingPrefix));
      return new Set(
        current.messages
          .filter((message) => !shown.has(message.id) && !(replacing && message.role === 'user'))
          .map((message) => message.id),
      );
    },
  });
  private readonly returnPosition = linkedSignal(() => this.restoreTo());
  private readonly textTools = viewChild.required(TextTools);

  constructor() {
    afterRenderEffect(() => {
      const position = this.returnPosition(),
        view = this.viewport().nativeElement;
      if (!position || this.store.loadingConversation()) return;
      view.scrollTop = position.scrollTop;
      this.scroll.mark();
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
    effect(() => {
      this.store.streamingText();
      this.store.visibleMessages();
      // 每次變更都要讀取，讓每則新到的訊息都能與它出現前畫面上的內容比較。
      this.entering();
      this.scroll.schedule();
    });
  }

  /** 剛送出新提問；若使用者偏好如此，就跟隨其回答。 */
  follow() {
    this.following.set(this.store.personal.value().autoFollow);
  }
  reveal(messageId: string) {
    this.following.set(false);
    requestAnimationFrame(() =>
      this.viewport()
        .nativeElement.querySelector<HTMLElement>(`[data-message-id="${CSS.escape(messageId)}"]`)
        ?.scrollIntoView({ block: 'center', behavior: this.themes.scrollBehavior() }),
    );
  }
  onScroll() {
    this.paragraph.set(null);
    this.scroll.scrolled();
  }
  edit(message: MessageDto) {
    this.store.edit(message);
    this.edited.emit();
  }
  saveArtifact(message: MessageDto) {
    this.textTools().open(message.content, 'save', message.id);
  }
  paragraphAction(action: string) {
    const selected = this.paragraph();
    if (!selected) return;
    this.textTools().open(selected.text, action, selected.sourceId);
    this.paragraph.set(null);
    window.getSelection()?.removeAllRanges();
  }
}

interface EnteringSource {
  messages: MessageDto[];
  loading: boolean;
  conversation: string | null;
}
const none: ReadonlySet<string> = new Set();
const pendingPrefix = 'pending:';
