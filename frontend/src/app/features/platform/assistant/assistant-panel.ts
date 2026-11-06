import {
  afterNextRender,
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  effect,
  ElementRef,
  inject,
  Injector,
  input,
  linkedSignal,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { UserSettingsService } from '@core/preferences/user-settings';
import { Autosize } from '@shared/browser/autosize';
import { CopyFeedback } from '@shared/browser/copy-feedback';
import { isSubmitKey } from '@shared/browser/submit-key';
import { uniqueId } from '@shared/browser/unique-id';
import { MarkdownView } from '@shared/markdown/markdown-view';
import { StreamingAnswer } from '@shared/markdown/streaming-answer';
import { AiMark } from '@shared/ui/ai-mark';
import { FloatingPanel, type FloatingMode } from '@shared/ui/floating-panel';
import { GenerationIndicator } from '@shared/ui/generation-indicator';
import { Icon } from '@shared/ui/icon';
import { Notice } from '@shared/ui/notice';
import { Select } from '@shared/ui/select';
import { StatusBadge } from '@shared/ui/status-badge';
import {
  ASSISTANT_LIMITS,
  type AssistantConversation,
  type AssistantExchange,
} from './assistant-conversation';

/** 島上「剛答完」的預覽停留多久，之後收回「已回答」。 */
const PREVIEW_MS = 2600;

/**
 * 助理面板：常駐在畫面頂端中央的極光圓鈕（`FloatingPanel` 的 `docked`），指到時露出「問『頁面』」，
 * 點一下往下長成面板，可拖動、調整大小、全螢幕，縮小回到頂端；縮著時圓鈕上顯示回答進度與正在查的資料，
 * 答完短暫預覽第一句，沒看的留一顆亮點。
 * 提問、回答與輸入框沿用對話頁的外觀（`styles/conversation.scss`、`StreamingAnswer`）。
 * 回答依所在頁面的畫面資料，需要時由後端的工具再查，數字由系統計算；`[n]` 點下去展開該輪的依據並標出第 n 筆。
 * 全站由 `AssistantHost` 放一個；對話狀態由呼叫端以 `assistantConversation` 建立後傳入。
 */
@Component({
  selector: 'nx-assistant-panel',
  imports: [
    AiMark,
    Autosize,
    FloatingPanel,
    GenerationIndicator,
    Icon,
    MarkdownView,
    Notice,
    RouterLink,
    Select,
    StatusBadge,
    StreamingAnswer,
  ],
  providers: [CopyFeedback],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './assistant-panel.scss',
  templateUrl: './assistant-panel.html',
})
export class AssistantPanel {
  readonly conversation = input.required<AssistantConversation>();
  /** 所在頁面的名稱（標題旁顯示）；沒有時為 null。 */
  readonly page = input<string | null>(null);
  /** 還沒提問時列出的建議問題，點一下直接送出。 */
  readonly suggestions = input<readonly string[]>([]);
  readonly title = '助理';
  readonly limit = ASSISTANT_LIMITS.question;
  readonly id = uniqueId('nx-assistant');
  readonly draft = signal('');
  readonly composing = signal(false);
  /** 展開依據清單的輪次，以及目前標出的那一筆。 */
  readonly openFacts = signal<ReadonlySet<number>>(new Set());
  readonly cited = signal<{ turn: number; fact: number } | null>(null);
  readonly copiedTurn = signal<number | null>(null);
  readonly copy = inject(CopyFeedback);
  /** 依據的處理頁連結（後端給的網址含查詢字串，例：卡號對應以卡號搜尋）。 */
  readonly router = inject(Router);
  private readonly settings = inject(UserSettingsService);
  private readonly floating = viewChild(FloatingPanel);
  /** 第一次展開才建立面板內容：島常駐，但輸入框、模型選單等等到要用才付出成本。 */
  readonly started = linkedSignal<FloatingMode, boolean>({
    source: () => this.floating()?.mode() ?? 'hidden',
    computation: (mode, previous) => !!previous?.value || mode === 'panel' || mode === 'full',
  });
  private readonly input = viewChild<ElementRef<HTMLTextAreaElement>>('question');
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly injector = inject(Injector);

  /** 縮成島時答完、還沒展開來看的那一輪；`preview` 期間島上顯示第一句。 */
  private readonly unread = signal<AssistantExchange | null>(null);
  readonly preview = signal(false);
  readonly latest = computed(() => this.conversation().exchanges().at(-1) ?? null);
  /** 指標或鍵盤停在島上：閒置的圓鈕露出「問『頁面』」，未讀時露出「已回答 · 查看」。 */
  readonly peek = computed(() => !!this.floating()?.peek());
  readonly island = computed(() => {
    if (this.conversation().busy())
      return { state: 'busy', text: (this.latest()?.activity ?? '正在回答') + '…' } as const;
    const unread = this.unread();
    if (!unread) {
      const page = this.page();
      return { state: 'idle', text: page ? `問「${page}」` : '問助理' } as const;
    }
    const failed = unread.status !== 'done';
    if (this.preview() && !failed)
      return { state: 'preview', text: excerpt(unread.answer) } as const;
    return { state: 'unread', text: failed ? '回答未完成 · 查看' : '已回答 · 查看' } as const;
  });
  readonly islandLabel = computed(() => {
    const island = this.island();
    const text =
      island.state === 'idle'
        ? '展開'
        : island.state === 'preview'
          ? `已回答：${island.text}`
          : island.text;
    return `${this.title}，${text}`;
  });

  constructor() {
    // 第一次展開才載入模型清單（失敗後再展開會重試）。
    effect(() => {
      if (this.started()) untracked(() => void this.conversation().prepare());
    });
    // 縮成島時一輪結束：記成未讀並短暫預覽；展開面板就算看過了。
    let previous: AssistantExchange | null = null;
    let timer: ReturnType<typeof setTimeout> | undefined;
    inject(DestroyRef).onDestroy(() => clearTimeout(timer));
    effect(() => {
      const latest = this.latest();
      const minimized = this.floating()?.mode() === 'island';
      untracked(() => {
        if (!minimized) {
          this.unread.set(null);
          this.preview.set(false);
        } else if (
          latest &&
          previous?.id === latest.id &&
          isActive(previous) &&
          !isActive(latest) &&
          latest.status !== 'stopped'
        ) {
          this.unread.set(latest);
          this.preview.set(true);
          clearTimeout(timer);
          timer = setTimeout(() => this.preview.set(false), PREVIEW_MS);
        }
        previous = latest;
      });
    });
  }

  /** 展開面板（範例頁與測試用；使用者點島展開）。 */
  open() {
    this.floating()?.open();
  }

  ask(text = this.draft()) {
    if (!text.trim() || this.conversation().busy()) return;
    this.draft.set('');
    this.cited.set(null);
    void this.conversation().ask(text);
    // 新的提問捲到面板頂端，回答在下方往下長，不必一直追著捲。
    afterNextRender(
      () =>
        this.host.nativeElement
          .querySelector('.assistant-turn:last-of-type')
          ?.scrollIntoView({ block: 'start', behavior: 'smooth' }),
      { injector: this.injector },
    );
  }

  keydown(event: KeyboardEvent) {
    if (!isSubmitKey(event, this.composing(), this.settings.value().enterToSend)) return;
    event.preventDefault();
    this.ask();
  }

  reset() {
    this.conversation().reset();
    this.cited.set(null);
    this.openFacts.set(new Set());
    this.input()?.nativeElement.focus();
  }

  toggleFacts(turn: AssistantExchange) {
    this.openFacts.update((open) => {
      const next = new Set(open);
      if (!next.delete(turn.id)) next.add(turn.id);
      return next;
    });
  }

  cite(turn: AssistantExchange, fact: number) {
    this.cited.set({ turn: turn.id, fact });
    this.openFacts.update((open) => new Set(open).add(turn.id));
    afterNextRender(
      () =>
        this.host.nativeElement
          .querySelector(`#${this.factId(turn, fact)}`)
          ?.scrollIntoView({ block: 'nearest', behavior: 'smooth' }),
      { injector: this.injector },
    );
  }

  copyAnswer(turn: AssistantExchange) {
    this.copiedTurn.set(turn.id);
    void this.copy.copy(turn.answer);
  }

  factId(turn: AssistantExchange, fact: number) {
    return `${this.id}-${turn.id}-${fact}`;
  }

  /** 查詢條件跟上一輪不同時，在兩輪之間標出來：之前的回答依的是舊條件。 */
  contextChanged(index: number) {
    const turns = this.conversation().exchanges();
    return index > 0 && turns[index].context !== turns[index - 1].context;
  }
}

function isActive(turn: AssistantExchange) {
  return turn.status === 'waiting' || turn.status === 'streaming';
}

/** 島上預覽用的第一句：拿掉 Markdown 記號與 `[n]`，取到第一個句號為止。 */
function excerpt(answer: string) {
  const plain = answer
    .replace(/\s*\[\d+\]/g, '')
    .replace(/[*_`#>|~]/g, '')
    .replace(/\s+/g, ' ')
    .trim();
  const end = plain.search(/[。！？!?]/);
  return end > 0 ? plain.slice(0, end + 1) : plain;
}
