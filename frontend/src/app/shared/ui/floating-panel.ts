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
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { below } from '../browser/breakpoints';
import { mediaQuery } from '../browser/media-query';
import { swallowClick } from '../browser/swallow-click';
import { bringToFront, ownsEscape } from '../browser/top-layer';
import { uniqueId } from '../browser/unique-id';
import {
  anchorOf,
  centeredPlacement,
  collapseTo,
  defaultPlacement,
  FLOATING_LIMITS,
  expandFrom,
  moved,
  place,
  placementOf,
  rectOf,
  resized,
  snapped,
  type Anchor,
  type FloatingPlacement,
  type Rect,
  type ResizeEdge,
  type Size,
} from './floating-geometry';
import { Icon } from './icon';
import { morphSize } from './morph-size';

/** `hidden` 還沒開過；開過之後只在面板、全螢幕與動態島之間切換，沒有關閉。 */
export type FloatingMode = 'hidden' | 'panel' | 'full' | 'island';

/** 指標移動多少才算拖動，讓標題列與島上的點擊仍然有效（與滑動關閉相同）。 */
const SLOP = 6;
/** 島還沒量到尺寸時（第一次縮小的那一幀、單元測試）的大小。 */
const ISLAND_FALLBACK: Size = { width: 160, height: 40 };
const EDGES: readonly ResizeEdge[] = ['start', 'end', 'bottom', 'bottom-start', 'bottom-end'];

interface Gesture {
  kind: 'move' | ResizeEdge;
  pointer: number;
  x: number;
  y: number;
  start: Rect;
  moved: boolean;
}

interface Stored {
  placement: FloatingPlacement;
  island: Anchor | null;
}

/**
 * 浮在頁面上、不擋操作的面板：拖標題列移動（放開時靠近邊緣會吸附）、拉左右下緣調整大小、
 * 放大成全螢幕，或縮成動態島（以彈簧變形收進角落的膠囊，點一下長回原處）。沒有關閉，
 * 開過之後一直留在頁面上，離開頁面才消失。手機上面板佔滿畫面，島在底部置中，不能拖動。
 * `docked` 時一開始就是島、常駐在畫面頂端中央（全站入口），縮小一律回到那裡，島不能拖；
 * 面板從島往下長出來，仍可拖動與調整大小。
 * 標題、按鈕收在標題列中間一顆與島同形的膠囊裡，展開時看起來就是島長大。
 * 位置與大小依 `storageKey` 記在瀏覽器；內容由呼叫端投影：標題前的標記 `[floating-mark]`、標題旁 `[floating-meta]`、
 * 按鈕 `[floating-actions]`、底部 `[floating-footer]`、島 `[floating-island]` 與 `[floating-island-actions]`。
 */
@Component({
  selector: 'nx-floating-panel',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './floating-panel.scss',
  template: `<div
    #layer
    popover="manual"
    class="ui-floating"
    [attr.data-mode]="view()"
    [attr.data-side]="side()"
    [attr.data-dock]="home() ? 'top' : null"
    [attr.data-gesture]="gesture() ? '' : null"
    [attr.data-busy]="busy() ? '' : null"
    [style.transition-property]="settling() ? 'opacity, scale' : null"
    [style.--floating-x]="frame().x + 'px'"
    [style.--floating-y]="frame().y + 'px'"
    [style.--floating-width]="frame().width + 'px'"
    [style.--floating-height]="frame().height + 'px'"
    [style.--panel-width]="content().width + 'px'"
    [style.--panel-height]="content().height + 'px'"
    (pointermove)="track($event)"
    (pointerup)="finish($event)"
    (pointercancel)="finish($event)"
  >
    <section
      class="ui-floating-panel"
      role="dialog"
      [attr.aria-labelledby]="id + '-title'"
      [inert]="mode() === 'island'"
    >
      <header class="ui-floating-header" (pointerdown)="begin($event, 'move')">
        <div class="ui-floating-heading">
          <ng-content select="[floating-mark]" />
          <h2 class="ui-floating-title" [id]="id + '-title'">{{ title() }}</h2>
          <ng-content select="[floating-meta]" />
          <span class="ui-floating-actions">
            <ng-content select="[floating-actions]" />
            <button
              type="button"
              class="icon-button"
              aria-label="縮小成動態島"
              title="縮小（Esc）"
              (click)="minimize()"
            >
              <nx-icon name="minus" />
            </button>
            @if (!phone()) {
              <button
                type="button"
                class="icon-button"
                [attr.aria-label]="mode() === 'full' ? '還原大小' : '全螢幕'"
                [title]="mode() === 'full' ? '還原大小' : '全螢幕'"
                [attr.aria-pressed]="mode() === 'full'"
                (click)="toggleFull()"
              >
                <nx-icon [name]="mode() === 'full' ? 'minimize' : 'maximize'" />
              </button>
            }
          </span>
        </div>
      </header>
      <div class="ui-floating-body"><ng-content /></div>
      <ng-content select="[floating-footer]" />
      @if (resizable()) {
        @for (edge of edges; track edge) {
          <span
            class="ui-floating-edge"
            [attr.data-edge]="edge"
            aria-hidden="true"
            (pointerdown)="begin($event, edge)"
          ></span>
        }
      }
    </section>
    <div
      #island
      class="ui-floating-island"
      [inert]="mode() !== 'island'"
      (pointerdown)="begin($event, 'move')"
      (pointerenter)="hovered.set(true)"
      (pointerleave)="hovered.set(false)"
    >
      <button
        #islandButton
        type="button"
        class="ui-floating-island-open"
        [attr.aria-label]="islandLabel()"
        (click)="expand()"
        (focus)="focused.set($any($event.target).matches(':focus-visible'))"
        (blur)="focused.set(false)"
      >
        <ng-content select="[floating-island]" />
      </button>
      <ng-content select="[floating-island-actions]" />
    </div>
    <span class="sr-only" role="status">{{ mode() === 'island' ? islandLabel() : '' }}</span>
  </div>`,
})
export class FloatingPanel {
  readonly title = input.required<string>();
  /** 動態島的可存取名稱與狀態播報，例：「助理，正在回答」。 */
  readonly islandLabel = input.required<string>();
  /** 記住位置與大小的鍵；不給就每次從預設位置（右上）開。 */
  readonly storageKey = input<string | null>(null);
  /** 常駐在頂端中央的島（見類別說明）。 */
  readonly docked = input(false);
  /** 內容正在工作（例：助理回答中）：面板內緣泛起柔光。 */
  readonly busy = input(false);
  readonly id = uniqueId('nx-floating');
  readonly hovered = signal(false);
  readonly focused = signal(false);
  /** 指標停在島上或鍵盤移到島上：呼叫端可藉此在島上多露出說明文字。 */
  readonly peek = computed(() => this.mode() === 'island' && (this.hovered() || this.focused()));
  readonly edges = EDGES;
  private readonly state = signal<FloatingMode>('hidden');
  readonly mode = this.state.asReadonly();
  /** 手機：面板佔滿畫面、島在底部置中，不拖動也不調整大小。 */
  readonly phone = mediaQuery(below('phone'));
  readonly resizable = computed(() => this.mode() === 'panel' && !this.phone());
  /** 島固定在頂端中央：常駐且不在手機（手機的島在底部的拇指區）。 */
  readonly home = computed(() => this.docked() && !this.phone());
  private readonly viewport = signal<Size>(viewportSize());
  private readonly placement = signal<FloatingPlacement>(defaultPlacement(viewportSize()));
  private readonly anchor = signal<Anchor | null>(null);
  /** 拖動或調整大小中，指標下的矩形；放開後回到依錨計算的位置。 */
  private readonly live = signal<Rect | null>(null);
  readonly gesture = computed(() => !!this.live());
  /**
   * 常駐的島剛出現：島的寬度要顯示後才量得到，量到之前只做進場淡入，
   * 位置與大小直接到位，不從預設寬度變形過去。
   */
  readonly settling = signal(false);
  private readonly layer = viewChild.required<ElementRef<HTMLElement>>('layer');
  private readonly islandButton = viewChild.required<ElementRef<HTMLElement>>('islandButton');
  private readonly islandSize = morphSize(() => this.island()?.nativeElement);
  private readonly island = viewChild<ElementRef<HTMLElement>>('island');
  private readonly injector = inject(Injector);
  /** 縮小前是面板還是全螢幕；點島時回到那裡。 */
  private restoreTo: 'panel' | 'full' = 'panel';
  private located = false;
  private pointer: Gesture | null = null;

  /** 面板（非全螢幕）在目前視窗裡的位置；縮成島時內容維持這個大小，只被外框裁切。 */
  readonly panel = computed(() => rectOf(this.placement(), this.viewport()));
  /** 面板內容的大小：拖動或調整大小中跟著指標，內容即時重排，不等放開。 */
  readonly content = computed<Size>(() => {
    const live = this.live();
    return live && this.mode() !== 'island' ? live : this.panel();
  });
  readonly islandRect = computed(() => {
    const size = this.islandSize() ?? ISLAND_FALLBACK,
      viewport = this.viewport();
    // 手機的島固定在底部置中（拇指區），不記位置。
    if (this.phone() || this.docked())
      return place(
        {
          side: 'start',
          inline: (viewport.width - size.width) / 2,
          edge: this.phone() ? 'bottom' : 'top',
          block: FLOATING_LIMITS.margin,
        },
        size,
        viewport,
      );
    return place(this.anchor() ?? collapseTo(this.panel(), viewport), size, viewport);
  });
  /** 畫面上的樣子：手機的面板就是全螢幕。 */
  readonly view = computed<FloatingMode>(() =>
    this.phone() && this.mode() === 'panel' ? 'full' : this.mode(),
  );
  /** 外框現在的矩形，由 CSS 以彈簧過渡到這裡。 */
  readonly frame = computed<Rect>(() => {
    const live = this.live();
    if (live) return live;
    const view = this.view();
    if (view === 'island' || (view === 'hidden' && this.docked())) return this.islandRect();
    if (view === 'full') return { x: 0, y: 0, ...this.viewport() };
    return this.panel();
  });
  /** 島與面板內容收向哪一側（常駐的島收向中間，見 `data-dock`）。 */
  readonly side = computed(() =>
    this.frame().x + this.frame().width / 2 > this.viewport().width / 2 ? 'end' : 'start',
  );

  constructor() {
    const destroyRef = inject(DestroyRef);
    let frame = 0,
      settle = 0;
    const resize = () => {
      cancelAnimationFrame(frame);
      frame = requestAnimationFrame(() => this.viewport.set(viewportSize()));
    };
    // 非模態，沒有原生的 Esc：全螢幕先還原，面板再縮成島（輪到誰見 `ownsEscape`）。
    const escape = (event: KeyboardEvent) => {
      const mode = this.mode();
      if (event.key !== 'Escape' || mode === 'hidden' || mode === 'island') return;
      if (!ownsEscape(this.layer().nativeElement, event)) return;
      event.preventDefault();
      if (mode === 'full' && !this.phone()) this.state.set('panel');
      else this.minimize();
    };
    window.addEventListener('resize', resize);
    document.addEventListener('keydown', escape);
    // 常駐的島一建立就出現；面板第一次展開時才決定位置（見 `locate`）。
    afterNextRender(
      () => {
        if (!this.docked()) return;
        this.settling.set(true);
        this.state.set('island');
        this.layer().nativeElement.showPopover?.();
      },
      { injector: this.injector },
    );
    // 量到島的寬度並寫進樣式後，隔兩個畫格（確定已套用）才恢復變形過渡。
    effect(() => {
      if (!this.settling() || !this.islandSize()?.width) return;
      settle = requestAnimationFrame(() => {
        settle = requestAnimationFrame(() => this.settling.set(false));
      });
    });
    destroyRef.onDestroy(() => {
      cancelAnimationFrame(frame);
      cancelAnimationFrame(settle);
      window.removeEventListener('resize', resize);
      document.removeEventListener('keydown', escape);
      const layer = this.layer().nativeElement;
      if (layer.matches(':popover-open')) layer.hidePopover();
    });
  }

  /** 開啟或展開；已開著就移到最上面（可能被之後開的對話框蓋住）。 */
  open() {
    const layer = this.layer().nativeElement;
    if (this.mode() === 'hidden') {
      this.locate();
      this.state.set('panel');
      layer.showPopover?.();
      this.focusPanel();
    } else if (this.mode() === 'island') this.expand();
    else {
      bringToFront(layer);
      this.focusPanel();
    }
  }

  minimize() {
    const mode = this.mode();
    if (mode !== 'panel' && mode !== 'full') return;
    // 焦點在面板裡（或按下的按鈕已被移除、掉到 body）就交給島，鍵盤使用者不會掉回頁首；
    // 要在面板變 inert 之前判斷，之後焦點會掉到 body。
    const focused = document.activeElement;
    if (!focused || focused === document.body || this.layer().nativeElement.contains(focused))
      this.focusAfterRender(() => this.islandButton().nativeElement);
    this.restoreTo = mode === 'full' && !this.phone() ? 'full' : 'panel';
    if (!this.docked()) this.anchor.set(collapseTo(this.panel(), this.viewport()));
    this.state.set('island');
    this.save();
  }

  expand() {
    if (this.mode() !== 'island') return;
    // 常駐的島第一次展開才讀記住的位置；之後回到上次放的地方，由島變形過去。
    if (this.docked()) {
      if (!this.located) this.locate();
    } else this.placement.set(expandFrom(this.islandRect(), this.placement(), this.viewport()));
    // 島展開後變 inert，收不到 pointerleave：先清掉，下次縮小不會一出現就露出說明。
    this.hovered.set(false);
    this.state.set(this.restoreTo);
    this.save();
    this.focusPanel();
  }

  toggleFull() {
    const mode = this.mode();
    if (mode === 'panel' || mode === 'full') this.state.set(mode === 'full' ? 'panel' : 'full');
  }

  /** 標題列與島拖動、邊緣調整大小；按鈕與欄位上不開始（島上的開啟鈕例外，沒拖動就是點擊）。 */
  begin(event: PointerEvent, kind: Gesture['kind']) {
    const mode = this.mode();
    if (event.button !== 0 || this.phone() || mode === 'full' || mode === 'hidden') return;
    if (mode === 'island' && this.docked()) return;
    const target = event.target as Element;
    if (kind === 'move' && target.closest('button, a, input, textarea, select, [role="combobox"]'))
      if (target.closest('.ui-floating-island-open') !== this.islandButton().nativeElement) return;
    event.stopPropagation();
    this.layer().nativeElement.setPointerCapture?.(event.pointerId);
    this.pointer = {
      kind,
      pointer: event.pointerId,
      x: event.clientX,
      y: event.clientY,
      start: this.frame(),
      moved: kind !== 'move',
    };
    // 調整大小一按下就算開始；拖動要先移動一段距離，否則是點擊。
    if (kind !== 'move') {
      event.preventDefault();
      this.live.set(this.frame());
    }
  }

  track(event: PointerEvent) {
    const gesture = this.pointer;
    if (!gesture || event.pointerId !== gesture.pointer) return;
    const dx = event.clientX - gesture.x,
      dy = event.clientY - gesture.y;
    if (!gesture.moved && Math.hypot(dx, dy) < SLOP) return;
    gesture.moved = true;
    const viewport = this.viewport();
    this.live.set(
      gesture.kind === 'move'
        ? moved(gesture.start, dx, dy, viewport)
        : resized(gesture.start, gesture.kind, dx, dy, viewport),
    );
  }

  finish(event: PointerEvent) {
    const gesture = this.pointer;
    if (!gesture || event.pointerId !== gesture.pointer) return;
    this.pointer = null;
    const live = this.live();
    if (!gesture.moved || !live) return;
    if (gesture.kind === 'move') swallowClick();
    const viewport = this.viewport();
    untracked(() => {
      if (this.mode() === 'island') this.anchor.set(anchorOf(snapped(live, viewport), viewport));
      else
        this.placement.set(
          placementOf(gesture.kind === 'move' ? snapped(live, viewport) : live, viewport),
        );
      this.live.set(null);
    });
    this.save();
  }

  /** 第一次開啟才決定位置：元件建立時的視窗大小可能已經變了。 */
  private locate() {
    const viewport = viewportSize();
    const stored = this.restore();
    this.located = true;
    this.viewport.set(viewport);
    this.placement.set(
      stored?.placement ??
        (this.docked() ? centeredPlacement(viewport) : defaultPlacement(viewport)),
    );
    this.anchor.set(this.docked() ? null : (stored?.island ?? null));
  }

  private focusPanel() {
    this.focusAfterRender(() => {
      const panel = this.layer().nativeElement.querySelector<HTMLElement>('.ui-floating-panel');
      return panel?.querySelector<HTMLElement>('[autofocus]') ?? panel;
    });
  }

  // 面板與島的 inert 在下一次渲染才切換，同步呼叫 focus() 會落在仍是 inert 的元素上而無效。
  private focusAfterRender(target: () => HTMLElement | null | undefined) {
    afterNextRender(() => target()?.focus({ preventScroll: true }), { injector: this.injector });
  }

  private storageId() {
    const key = this.storageKey();
    return key ? `ai-nexus.floating.${key}` : null;
  }

  private restore(): Stored | null {
    const id = this.storageId();
    if (!id) return null;
    try {
      const stored = JSON.parse(localStorage.getItem(id) ?? 'null') as Partial<Stored> | null;
      return isPlacement(stored?.placement)
        ? { placement: stored.placement, island: isAnchor(stored.island) ? stored.island : null }
        : null;
    } catch {
      return null;
    }
  }

  private save() {
    const id = this.storageId();
    if (!id) return;
    try {
      const stored: Stored = { placement: this.placement(), island: this.anchor() };
      localStorage.setItem(id, JSON.stringify(stored));
    } catch {
      /* 選用偏好：存不了就每次從預設位置開。 */
    }
  }
}

function viewportSize(): Size {
  return { width: window.innerWidth, height: window.innerHeight };
}

function isAnchor(value: unknown): value is Anchor {
  const anchor = value as Anchor | null | undefined;
  return (
    !!anchor &&
    (anchor.side === 'start' || anchor.side === 'end') &&
    (anchor.edge === 'top' || anchor.edge === 'bottom') &&
    Number.isFinite(anchor.inline) &&
    Number.isFinite(anchor.block)
  );
}

function isPlacement(value: unknown): value is FloatingPlacement {
  const placement = value as FloatingPlacement | null | undefined;
  return (
    isAnchor(placement) && Number.isFinite(placement.width) && Number.isFinite(placement.height)
  );
}
