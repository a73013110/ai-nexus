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
import type { MessageDto } from '../../core/api/schema';
import { ThemeService } from '../../core/preferences/theme-service';
import { followScroll } from '../../shared/browser/follow-scroll';
import type { ReaderOrigin } from '../../shared/browser/reader-navigation';
import { TextSelection, type SelectedText } from '../../shared/browser/text-selection';
import { Icon } from '../../shared/ui/icon';
import { TextSelectionToolbar } from '../artifacts/text-selection-toolbar';
import { TextTools } from '../artifacts/text-tools';
import { ChatMessage } from './chat-message';
import { ChatStore } from './chat-store';
import { ConversationOutline } from './conversation-outline';

/**
 * The scrolling message list. It follows new output while the reader stays at the bottom, pauses
 * when they scroll up, and returns to a saved position after visiting the document reader.
 */
@Component({
  selector: 'div[nxChatThread]',
  imports: [ChatMessage, ConversationOutline, Icon, TextSelection, TextSelectionToolbar, TextTools],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrls: ['./chat-column.scss', './chat-thread.scss'],
  templateUrl: './chat-thread.html',
})
export class ChatThread {
  readonly store = inject(ChatStore);
  private readonly themes = inject(ThemeService);
  readonly matches = input<string[]>([]);
  readonly currentMatch = input<string | null>(null);
  /** Set on each navigation; a saved reader position wins over auto-follow. */
  readonly arrival = input<{ position: ReaderOrigin | null }>({ position: null });
  /** Set once the conversation has loaded, to scroll back to where the reader left. */
  readonly restoreTo = input<ReaderOrigin | null>(null);
  /** A prompt was opened for editing, so the composer should take focus. */
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
  /** The answer being generated; only its row reads the streamed text. */
  readonly activeId = computed(() => this.store.liveRun()?.assistantMessageId ?? null);
  readonly runStatus = computed(() => this.store.liveRun()?.status ?? '');
  readonly matched = computed(() => new Set(this.matches()));
  /**
   * Messages that arrived after the conversation was shown (a sent prompt, a new answer), so they
   * animate in. Opening a conversation or another version renders everything at once, and the
   * server's copy of a sent prompt replaces the one already shown without arriving again.
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
      // Read on every change so each arrival is compared with what was on screen before it.
      this.entering();
      this.scroll.schedule();
    });
  }

  /** A new prompt was sent; follow its answer when the person prefers that. */
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
