import {
  afterRenderEffect,
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
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
import type { ReaderOrigin } from '../../shared/browser/reader-navigation';
import { TextSelection, type SelectedText } from '../../shared/browser/text-selection';
import { Icon } from '../../shared/ui/icon';
import type { ProductTourDefinition } from '../../shared/ui/product-tour';
import { ProductTourButton } from '../../shared/ui/product-tour-button';
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
  imports: [
    ChatMessage,
    ConversationOutline,
    Icon,
    ProductTourButton,
    TextSelection,
    TextSelectionToolbar,
    TextTools,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrls: ['./chat-column.scss', './chat-thread.scss'],
  templateUrl: './chat-thread.html',
})
export class ChatThread {
  readonly store = inject(ChatStore);
  private readonly themes = inject(ThemeService);
  readonly tour = input.required<ProductTourDefinition>();
  readonly matches = input<string[]>([]);
  readonly currentMatch = input<string | null>(null);
  /** Set on each navigation; a saved reader position wins over auto-follow. */
  readonly arrival = input<{ position: ReaderOrigin | null }>({ position: null });
  /** Set once the conversation has loaded, to scroll back to where the reader left. */
  readonly restoreTo = input<ReaderOrigin | null>(null);
  /** A prompt was opened for editing, so the composer should take focus. */
  readonly edited = output<void>();
  readonly following = linkedSignal({
    source: this.arrival,
    computation: ({ position }) => !position && this.store.personal.value().autoFollow,
  });
  readonly paragraph = linkedSignal<unknown, SelectedText | null>({
    source: this.arrival,
    computation: () => null,
  });
  private readonly returnPosition = linkedSignal(() => this.restoreTo());
  private readonly viewport = viewChild.required<ElementRef<HTMLElement>>('viewport');
  private readonly textTools = viewChild.required(TextTools);
  private scrollFrame = 0;
  private lastScrollTop = 0;

  constructor() {
    afterRenderEffect(() => {
      const position = this.returnPosition(),
        view = this.viewport().nativeElement;
      if (!position || this.store.loadingConversation()) return;
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
    effect(() => {
      this.store.streamingText();
      this.store.visibleMessages();
      this.scheduleFollow();
    });
    inject(DestroyRef).onDestroy(() => cancelAnimationFrame(this.scrollFrame));
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
        ?.scrollIntoView({ block: 'center', behavior: this.motion() }),
    );
  }

  scheduleFollow() {
    if (!this.following() || this.scrollFrame) return;
    this.scrollFrame = requestAnimationFrame(() => {
      this.scrollFrame = 0;
      if (this.following()) this.scrollLatest(false);
    });
  }
  onScroll() {
    this.paragraph.set(null);
    const view = this.viewport().nativeElement;
    const near = view.scrollHeight - view.scrollTop - view.clientHeight < 96;
    // A smooth jump upwards begins near the bottom; that first event must not restart auto-follow.
    this.following.set(near && (this.following() || view.scrollTop > this.lastScrollTop));
    this.lastScrollTop = view.scrollTop;
    if (!near) this.cancelFollow();
  }
  pauseFollowing() {
    this.following.set(false);
    this.cancelFollow();
    this.lastScrollTop = this.viewport().nativeElement.scrollTop;
  }
  scrollLatest(smooth = true) {
    const view = this.viewport().nativeElement;
    this.following.set(true);
    view.scrollTo({ top: view.scrollHeight, behavior: smooth ? this.motion() : 'instant' });
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
  private cancelFollow() {
    cancelAnimationFrame(this.scrollFrame);
    this.scrollFrame = 0;
  }
  private motion(): ScrollBehavior {
    return this.themes.reducedMotion() ? 'instant' : 'smooth';
  }
}
