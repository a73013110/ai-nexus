import { NgTemplateOutlet } from '@angular/common';
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
  linkedSignal,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { Icon } from './icon';
import { InferenceSignal } from './inference-signal';
import { IslandService, type IslandItem } from './island-service';
import { IssueCode } from './issue-code';

export type IslandMode = 'hidden' | 'compact' | 'split' | 'expanded';

/**
 * `IslandService` 登記的通知所形成的單一變形膠囊。只有一則通知時是精簡膠囊；兩則以上時，
 * 其餘的會分到一個泡泡中。開啟任何一個，都會展開每則通知與其動作。緊急通知會在出現時展開，
 * 直到使用者收合。
 * 膠囊在量測過的尺寸之間做動畫，因此不需要 `interpolate-size` 也能變形。
 */
@Component({
  selector: 'nx-island',
  imports: [Icon, InferenceSignal, IssueCode, NgTemplateOutlet, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './island.scss',
  host: { '[attr.data-mode]': 'mode()', '(keydown.escape)': 'escape($event)' },
  template: `@if (primary(); as first) {
      <div
        class="island"
        animate.enter="island-enter"
        animate.leave="island-leave"
        [attr.data-tone]="first.tone"
        [style.width.px]="size()?.width"
        [style.height.px]="size()?.height"
      >
        <div #content class="island-content">
          @if (mode() === 'expanded') {
            <ul class="island-list" animate.enter="island-swap">
              @for (item of items(); track item.id) {
                <li
                  class="island-item"
                  [attr.data-tone]="item.tone"
                  [attr.role]="item.tone === 'danger' ? 'alert' : 'status'"
                >
                  <ng-container *ngTemplateOutlet="glyph; context: { $implicit: item }" /><span
                    class="island-message"
                    >{{ item.message || item.label }}
                    @if (item.link; as link) {
                      <a [routerLink]="link.commands">{{ link.label }}</a>
                    }
                  </span>
                  @if (item.tone === 'danger') {
                    <nx-issue-code [message]="item.message" />
                  }
                  @if (item.action; as action) {
                    <button
                      type="button"
                      [class]="action.icon ? 'icon-button' : 'island-action'"
                      [attr.aria-label]="action.icon ? action.label : null"
                      [attr.title]="action.icon ? action.label : null"
                      [disabled]="action.disabled"
                      (click)="action.run()"
                    >
                      @if (action.icon) {
                        <nx-icon [name]="action.icon" />
                      } @else {
                        {{ action.label }}
                      }
                    </button>
                  }
                </li>
              }
            </ul>
            <button
              #collapseButton
              type="button"
              class="icon-button island-collapse"
              aria-label="收合提示"
              title="收合提示"
              (click)="collapse()"
            >
              <nx-icon name="chevron-up" />
            </button>
          } @else {
            <button
              #pill
              type="button"
              class="island-pill"
              aria-expanded="false"
              animate.enter="island-swap"
              [attr.aria-label]="first.label + '，展開提示'"
              (click)="open()"
            >
              <ng-container *ngTemplateOutlet="glyph; context: { $implicit: first }" /><span
                role="status"
                class="island-label"
                >{{ first.label }}</span
              >
            </button>
          }
        </div>
      </div>
      @if (mode() === 'split') {
        <button
          type="button"
          class="island-bubble"
          animate.enter="island-bubble-enter"
          [attr.data-tone]="rest()[0].tone"
          [attr.aria-label]="'另有 ' + rest().length + ' 則提示：' + rest()[0].label"
          [title]="rest()[0].label"
          (click)="open()"
        >
          <nx-icon [name]="rest()[0].icon" />
          @if (rest().length > 1) {
            <span class="island-count">+{{ rest().length - 1 }}</span>
          }
        </button>
      }
    }
    <ng-template #glyph let-item>
      @if (item.tone === 'live') {
        <nx-inference-signal class="island-glyph" [active]="true" [subtle]="true" />
      } @else {
        <nx-icon class="island-glyph" [name]="item.icon" />
      }
    </ng-template>`,
})
export class NexusIsland {
  private readonly island = inject(IslandService);
  readonly items = this.island.items;
  readonly primary = computed<IslandItem | null>(() => this.items()[0] ?? null);
  readonly rest = computed(() => this.items().slice(1));
  /** 島清空後自行關閉。 */
  private readonly opened = linkedSignal({
    source: () => this.items().length > 0,
    computation: () => false,
  });
  private readonly ids = computed(() => this.items().map((item) => item.id));
  /** 使用者已收合的通知；緊急的通知要重新出現後才會再次展開。 */
  private readonly seen = linkedSignal<readonly string[], ReadonlySet<string>>({
    source: this.ids,
    computation: (ids, previous) =>
      new Set([...(previous?.value ?? [])].filter((id) => ids.includes(id))),
  });
  readonly mode = computed<IslandMode>(() => {
    const items = this.items();
    // 即使為空也要讀取，讓離開的通知在重新出現前就被遺忘。
    const seen = this.seen();
    if (!items.length) return 'hidden';
    if (this.opened() || items.some((item) => item.urgent && !seen.has(item.id))) return 'expanded';
    return items.length > 1 ? 'split' : 'compact';
  });
  readonly size = signal<{ width: number; height: number } | null>(null);
  private readonly content = viewChild<ElementRef<HTMLElement>>('content');
  private readonly pill = viewChild<ElementRef<HTMLElement>>('pill');
  private readonly collapseButton = viewChild<ElementRef<HTMLElement>>('collapseButton');
  private readonly injector = inject(Injector);

  constructor() {
    if (typeof ResizeObserver === 'undefined') return;
    const observer = new ResizeObserver(([entry]) => {
      const box = entry.borderBoxSize?.[0];
      if (box) this.size.set({ width: box.inlineSize, height: box.blockSize });
    });
    effect((onCleanup) => {
      const element = this.content()?.nativeElement;
      if (!element) return untracked(() => this.size.set(null));
      observer.observe(element);
      onCleanup(() => observer.unobserve(element));
    });
    inject(DestroyRef).onDestroy(() => observer.disconnect());
  }

  open() {
    this.opened.set(true);
    afterNextRender(() => this.collapseButton()?.nativeElement.focus(), {
      injector: this.injector,
    });
  }
  collapse() {
    this.opened.set(false);
    this.seen.set(new Set(this.ids()));
    afterNextRender(() => this.pill()?.nativeElement.focus(), { injector: this.injector });
  }
  escape(event: Event) {
    if (this.mode() !== 'expanded') return;
    event.stopPropagation();
    this.collapse();
  }
}
