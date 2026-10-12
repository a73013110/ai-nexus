import {
  afterRenderEffect,
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import type { MessageDto } from '../../core/api/schema';
import { ThemeService } from '../../core/preferences/theme-service';
import { Icon } from '../../shared/ui/icon';
import { anchoredPopover } from '../../shared/browser/anchored-popover';
import { positionSidePopover } from '../../shared/browser/side-popover-position';

/** How far below the viewport's top a prompt counts as the current turn. */
const currentLine = 90;
/** Reaches above any conversation, so every prompt scrolled past stays reported. */
const reach = 10_000_000;

@Component({
  selector: 'nx-conversation-outline',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './conversation-outline.scss',
  template: `@if (turns().length) {
    <!-- Hover preview only; every turn is reachable through its own button. -->
    <!-- eslint-disable-next-line @angular-eslint/template/interactive-supports-focus -->
    <nav
      class="conversation-outline"
      aria-label="對話定位"
      (pointerleave)="leave()"
      (pointerenter)="cancelLeave()"
      (focusout)="focusLeave($event)"
      (keydown.escape)="close()"
    >
      <button
        type="button"
        class="outline-count"
        [attr.aria-expanded]="expanded()"
        aria-label="開啟對話目錄"
        (click)="expanded.update(toggle); preview.set(null)"
      >
        {{ active() + 1 }}<span>/{{ turns().length }}</span>
      </button>
      <div class="outline-ticks">
        @for (turn of turns(); track turn.id; let i = $index) {
          <button
            type="button"
            class="outline-tick"
            [class.is-current]="active() === i"
            [attr.aria-current]="active() === i ? 'location' : null"
            [attr.aria-label]="'跳至第 ' + (i + 1) + ' 輪：' + turn.prompt.slice(0, 64)"
            (pointerenter)="hover($event, i)"
            (focus)="preview.set(i)"
            (click)="jump(i)"
          >
            <span></span>
          </button>
        }
      </div>
      @if (expanded()) {
        <div
          #panel
          popover="manual"
          class="outline-popover outline-directory"
          aria-label="所有對話輪次"
        >
          <div class="outline-heading">
            <strong>{{ turns().length }} 輪對話</strong
            ><button type="button" class="icon-button" aria-label="關閉對話目錄" (click)="close()">
              <nx-icon name="close" />
            </button>
          </div>
          @for (turn of turns(); track turn.id; let i = $index) {
            <button
              type="button"
              class="outline-entry"
              [class.is-current]="active() === i"
              (click)="jump(i); close()"
            >
              <span>{{ i + 1 }}</span
              ><strong>{{ turn.prompt }}</strong>
            </button>
          }
        </div>
      } @else if (preview() !== null) {
        @let turn = turns()[preview()!];
        @if (turn) {
          <div class="outline-popover outline-preview" #panel popover="manual">
            <span class="panel-eyebrow">第 {{ preview()! + 1 }} 輪</span
            ><strong>{{ turn.prompt }}</strong>
            <p>{{ excerpt(turn.answer) || '尚未有 AI 回覆' }}</p>
            <button type="button" class="quiet-button" (click)="jump(preview()!); close()">
              跳到這段對話 <nx-icon name="arrow" />
            </button>
          </div>
        }
      }
    </nav>
  }`,
})
export class ConversationOutline {
  readonly messages = input.required<MessageDto[]>();
  readonly viewport = input<HTMLElement | null>(null);
  readonly navigated = output<void>();
  readonly expanded = signal(false);
  readonly preview = signal<number | null>(null);
  readonly active = signal(0);
  readonly toggle = (value: boolean) => !value;
  readonly turns = computed(() => {
    const rows = this.messages();
    return rows.flatMap((message, i) =>
      message.role === 'user'
        ? [
            {
              id: message.id,
              prompt: message.content,
              answer: rows[i + 1]?.role === 'assistant' ? rows[i + 1].content : '',
            },
          ]
        : [],
    );
  });
  private readonly themes = inject(ThemeService);
  private readonly element = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  private readonly panel = viewChild<ElementRef<HTMLElement>>('panel');
  private leaveTimer?: ReturnType<typeof setTimeout>;
  private readonly popover = anchoredPopover({
    panel: () => this.panel()?.nativeElement,
    place: (panel) => {
      const root = this.viewport();
      const trigger = this.element.querySelector<HTMLElement>(
        this.expanded()
          ? '.outline-count'
          : `.outline-tick:nth-child(${(this.preview() ?? 0) + 1})`,
      );
      if (root && trigger) positionSidePopover(trigger, panel, root.parentElement!);
    },
    dismiss: () => this.close(),
    within: () => this.element,
  });
  constructor() {
    // Placed again against the hovered tick, or the count once the directory opens.
    effect((onCleanup) => {
      const panel = this.panel()?.nativeElement,
        root = this.viewport();
      this.preview();
      this.expanded();
      if (!panel || !root) return;
      untracked(() => this.popover.show());
      onCleanup(() => this.popover.hide());
    });
    effect(() => {
      this.turns();
      this.preview.set(null);
      this.expanded.set(false);
    });
    // The current turn is the last prompt whose top has passed a line 90px below the viewport's top.
    // An observer whose root reaches far above the viewport and ends at that line reports exactly
    // those prompts, so scrolling reads no layout. Its margin follows the viewport's height.
    afterRenderEffect((onCleanup) => {
      const root = this.viewport();
      this.turns();
      const prompts = root
        ? Array.from(root.querySelectorAll<HTMLElement>('nx-chat-message[data-role="user"]'))
        : [];
      if (!root || !prompts.length) return;
      const order = new Map<Element, number>(prompts.map((node, i) => [node, i]));
      const passed = new Set<number>();
      let observer: IntersectionObserver | undefined;
      let height = -1;
      const observe = () => {
        if (root.clientHeight === height) return;
        height = root.clientHeight;
        observer?.disconnect();
        passed.clear();
        observer = new IntersectionObserver(
          (entries) => {
            for (const entry of entries) {
              const index = order.get(entry.target)!;
              if (entry.isIntersecting) passed.add(index);
              else passed.delete(index);
            }
            this.active.set(passed.size ? Math.max(...passed) : 0);
          },
          { root, rootMargin: `${reach}px 0px ${currentLine - height}px 0px` },
        );
        prompts.forEach((node) => observer!.observe(node));
      };
      const resize = new ResizeObserver(observe);
      resize.observe(root);
      observe();
      onCleanup(() => {
        resize.disconnect();
        observer?.disconnect();
      });
    });
    inject(DestroyRef).onDestroy(() => this.cancelLeave());
  }
  excerpt(value: string) {
    return value
      .replace(/```[\s\S]*?```/g, '程式碼區塊')
      .replace(/[*_#>`]/g, '')
      .replace(/\s+/g, ' ')
      .trim()
      .slice(0, 240);
  }
  hover(event: PointerEvent, index: number) {
    if (event.pointerType === 'mouse') {
      this.cancelLeave();
      this.preview.set(index);
    }
  }
  leave() {
    if (!this.expanded()) this.leaveTimer = setTimeout(() => this.preview.set(null), 180);
  }
  cancelLeave() {
    clearTimeout(this.leaveTimer);
  }
  focusLeave(event: FocusEvent) {
    if (!this.element.contains(event.relatedTarget as Node | null)) this.close();
  }
  close() {
    this.cancelLeave();
    this.preview.set(null);
    this.expanded.set(false);
  }
  jump(index: number) {
    const turn = this.turns()[index];
    const target =
      turn &&
      this.viewport()?.querySelector<HTMLElement>(`[data-message-id="${CSS.escape(turn.id)}"]`);
    if (!target) return;
    this.navigated.emit();
    this.active.set(index);
    target.scrollIntoView({
      block: 'start',
      behavior: this.themes.scrollBehavior(),
    });
  }
}
