import {
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
  viewChild,
} from '@angular/core';
import type { Message } from '../../core/api/types';
import { ThemeService } from '../../core/preferences/theme-service';
import { Icon } from '../../shared/ui/icon';
import { positionSidePopover } from '../../shared/browser/side-popover-position';

@Component({
  selector: 'nx-conversation-outline',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
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
  readonly messages = input.required<Message[]>();
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
  constructor() {
    effect((onCleanup) => {
      const panel = this.panel()?.nativeElement,
        root = this.viewport(),
        preview = this.preview();
      const expanded = this.expanded();
      if (!panel || !root) return;
      const position = () => {
        const trigger = this.element.querySelector<HTMLElement>(
          expanded ? '.outline-count' : `.outline-tick:nth-child(${(preview ?? 0) + 1})`,
        );
        if (trigger) positionSidePopover(trigger, panel, root.parentElement!);
      };
      panel.showPopover();
      position();
      const outside = (event: PointerEvent) => {
        if (event.target instanceof Node && !this.element.contains(event.target)) this.close();
      };
      const escape = (event: KeyboardEvent) => {
        if (event.key === 'Escape') {
          event.preventDefault();
          this.close();
        }
      };
      window.addEventListener('resize', position);
      document.addEventListener('pointerdown', outside);
      document.addEventListener('keydown', escape);
      onCleanup(() => {
        if (panel.isConnected && panel.matches(':popover-open')) panel.hidePopover();
        window.removeEventListener('resize', position);
        document.removeEventListener('pointerdown', outside);
        document.removeEventListener('keydown', escape);
      });
    });
    effect((onCleanup) => {
      const root = this.viewport(),
        turns = this.turns();
      this.preview.set(null);
      this.expanded.set(false);
      if (!root || !turns.length) return;
      let frame = 0;
      const update = () => {
        cancelAnimationFrame(frame);
        frame = requestAnimationFrame(() => {
          const top = root.getBoundingClientRect().top + 90;
          let index = 0;
          const nodes = root.querySelectorAll<HTMLElement>('.message.user');
          for (let i = 0; i < nodes.length; i++) {
            if (nodes[i].getBoundingClientRect().top <= top) index = i;
            else break;
          }
          this.active.set(index);
        });
      };
      root.addEventListener('scroll', update, { passive: true });
      const observer = new ResizeObserver(update);
      const list = root.querySelector('.message-list');
      observer.observe(list ?? root);
      update();
      onCleanup(() => {
        root.removeEventListener('scroll', update);
        observer.disconnect();
        cancelAnimationFrame(frame);
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
      behavior: this.themes.reducedMotion() ? 'instant' : 'smooth',
    });
  }
}
