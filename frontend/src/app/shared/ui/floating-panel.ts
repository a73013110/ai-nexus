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
  centered,
  collapseTo,
  defaultPlacement,
  expandFrom,
  gripOf,
  moved,
  place,
  placementOf,
  rectOf,
  resized,
  snapped,
  type Anchor,
  type FloatingPlacement,
  type Grip,
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
  /** 按住的是外框的哪一點；拖動中這一點留在指標下（見 `moved`）。 */
  grip: Grip;
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
 * `docked` 時一開始就是島（全站入口），預設在頂端中央；島的位置屬於使用者，可拖到別處並記住，
 * 縮小一律回到島那裡（不跟著面板走）。不論是否 `docked`，展開時面板都從島所在的那一側長出來。
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
    [attr.data-edge]="edge()"
    [attr.data-gesture]="gesture()"
    [attr.data-busy]="busy() ? '' : null"
    [style.transition-property]="settling() ? 'opacity, scale' : null"
    [style.--floating-x]="frame().x + 'px'"
    [style.--floating-y]="frame().y + 'px'"
    [style.--floating-width]="frame().width + 'px'"
    [style.--floating-height]="frame().height + 'px'"
    [style.--panel-width]="content().width + 'px'"
    [style.--panel-height]="content().height + 'px'"
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
      (pointerenter)="hover(true)"
      (pointerleave)="hover(false)"
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
  /** 一開始就是島、縮小回到島自己的位置（見類別說明）。 */
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
  private readonly viewport = signal<Size>(viewportSize());
  private readonly placement = signal<FloatingPlacement>(defaultPlacement(viewportSize()));
  private readonly anchor = signal<Anchor | null>(null);
  /** 開始拖動或調整大小後的手勢與指標位置；放開後清掉，回到依錨計算的位置。 */
  private readonly drag = signal<{ gesture: Gesture; x: number; y: number } | null>(null);
  /** 手勢中指標下的矩形。拖島時以島目前量到的大小計算：拖動中島上的文字變長變短，按住的點仍在指標下。 */
  private readonly live = computed<Rect | null>(() => {
    const drag = this.drag();
    if (!drag) return null;
    const { gesture, x, y } = drag,
      viewport = this.viewport();
    if (gesture.kind !== 'move')
      return resized(gesture.start, gesture.kind, x - gesture.x, y - gesture.y, viewport);
    const size = this.mode() === 'island' ? (this.islandSize() ?? ISLAND_FALLBACK) : gesture.start;
    return moved(size, gesture.grip, x, y, viewport);
  });
  /** 進行中的手勢（`data-gesture`）：外框以對應的游標顯示，期間不做變形過渡。 */
  readonly gesture = computed(() => this.drag()?.gesture.kind ?? null);
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
  private pointer: Gesture | null = null;
  /** 手勢進行中掛在 window 上的監聽；放開或元件銷毀時移除。 */
  private unlisten: (() => void) | null = null;

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
    // 手機的島固定在底部置中（拇指區），不套用桌面記住的位置。
    if (this.phone()) return place(centered('bottom'), size, viewport);
    const fallback = this.docked() ? centered('top') : collapseTo(this.panel(), viewport);
    return place(this.anchor() ?? fallback, size, viewport);
  });
  /** 島錨定的側與緣：外框內的面板與島對齊這裡，展開、縮小時看起來是從島長出、收回島。 */
  private readonly islandAnchor = computed(() => anchorOf(this.islandRect(), this.viewport()));
  readonly side = computed(() => this.islandAnchor().side);
  readonly edge = computed(() => this.islandAnchor().edge);
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
    // 常駐的島一建立就出現，先讀回記住的島與面板大小。
    afterNextRender(
      () => {
        if (!this.docked()) return;
        this.locate();
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
      this.unlisten?.();
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
    this.placement.set(expandFrom(this.islandRect(), this.placement(), this.viewport()));
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

  /**
   * 指標進出島。手勢中不更新：拖的是按下時看到的那個島（露出說明的就維持露出），
   * 不因指標暫時落在島外而收起、讓按住的點跑到指標外；放開時再依指標實際位置校正（`finish`）。
   */
  hover(over: boolean) {
    if (!this.pointer) this.hovered.set(over);
  }

  /** 標題列與島拖動、邊緣調整大小；按鈕與欄位上不開始（島上的開啟鈕例外，沒拖動就是點擊）。 */
  begin(event: PointerEvent, kind: Gesture['kind']) {
    const mode = this.mode();
    if (event.button !== 0 || this.phone() || mode === 'full' || mode === 'hidden') return;
    const target = event.target as Element;
    if (kind === 'move' && target.closest('button, a, input, textarea, select, [role="combobox"]'))
      if (target.closest('.ui-floating-island-open') !== this.islandButton().nativeElement) return;
    event.stopPropagation();
    this.listen();
    const start = this.frame();
    this.pointer = {
      kind,
      pointer: event.pointerId,
      x: event.clientX,
      y: event.clientY,
      start,
      grip: gripOf(start, event.clientX, event.clientY),
      moved: false,
    };
    // 調整大小一按下就算開始；拖動要先移動一段距離，否則是點擊。
    if (kind !== 'move') {
      event.preventDefault();
      this.engage(event);
    }
  }

  // 手勢期間聽 window：還沒超過 SLOP 前不擷取指標（擷取中放開，click 會改派給外框，
  // 島上的開啟鈕就收不到點擊），而這段期間指標一下子移出外框也跟得上。
  private listen() {
    this.unlisten?.();
    const track = (event: PointerEvent) => this.track(event);
    const finish = (event: PointerEvent) => this.finish(event);
    window.addEventListener('pointermove', track);
    window.addEventListener('pointerup', finish);
    window.addEventListener('pointercancel', finish);
    this.unlisten = () => {
      window.removeEventListener('pointermove', track);
      window.removeEventListener('pointerup', finish);
      window.removeEventListener('pointercancel', finish);
      this.unlisten = null;
    };
  }

  /**
   * 手勢正式開始：擷取指標到外框，之後游標一律是外框的（`data-gesture`），
   * 經過頁面上的連結、文字也不會換游標或觸發它們的 hover。
   */
  private engage(event: PointerEvent) {
    const gesture = this.pointer!;
    gesture.moved = true;
    this.layer().nativeElement.setPointerCapture?.(event.pointerId);
    this.drag.set({ gesture, x: event.clientX, y: event.clientY });
  }

  private track(event: PointerEvent) {
    const gesture = this.pointer;
    if (!gesture || event.pointerId !== gesture.pointer) return;
    if (gesture.moved) this.drag.set({ gesture, x: event.clientX, y: event.clientY });
    else if (Math.hypot(event.clientX - gesture.x, event.clientY - gesture.y) >= SLOP)
      this.engage(event);
  }

  private finish(event: PointerEvent) {
    const gesture = this.pointer;
    if (!gesture || event.pointerId !== gesture.pointer) return;
    this.pointer = null;
    this.unlisten?.();
    // 手勢中沒有更新 hover：依指標現在是否在島上校正一次。
    if (this.mode() === 'island')
      this.hovered.set(!!this.island()?.nativeElement.matches(':hover'));
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
      this.drag.set(null);
    });
    this.save();
  }

  /** 第一次出現（常駐的島）或開啟才決定位置：元件建立時的視窗大小可能已經變了。 */
  private locate() {
    const viewport = viewportSize();
    const stored = this.restore();
    this.viewport.set(viewport);
    this.placement.set(stored?.placement ?? defaultPlacement(viewport));
    this.anchor.set(stored?.island ?? null);
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
    (anchor.side === 'start' || anchor.side === 'center' || anchor.side === 'end') &&
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
