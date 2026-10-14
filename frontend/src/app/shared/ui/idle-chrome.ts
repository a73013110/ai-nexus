import {
  DestroyRef,
  Directive,
  ElementRef,
  booleanAttribute,
  computed,
  effect,
  inject,
  input,
  signal,
  untracked,
  type Signal,
} from '@angular/core';

/** 指標離 chrome 多近（px）算進入喚回區；區外的移動是閱讀，不喚回。 */
const REACH = 24;
/** 觸控點擊與按下位置相差超過這個距離（px）就是拖曳或捲動，不切換。 */
const TAP_SLOP = 10;
/** 觸控點到這些元素時是在操作內容，不切換 chrome。 */
const INTERACTIVE = 'a, button, input, select, textarea, summary, label, [role="button"]';

type ChromeState = 'shown' | 'hidden';

interface Part {
  readonly element: HTMLElement;
  readonly held: () => boolean;
  hovered: boolean;
}

/**
 * 檢視器的沉浸式 chrome：範圍內登記的工具列與（放大時的）標題列一起出現、一起淡出。
 * 開啟、換內容時短暫出現；滑鼠只有移近 chrome（`REACH` 內）或停在上面才喚回，在內容上閱讀時的移動、
 * 點擊不喚回；觸控點一下內容切換；Tab 讓鍵盤焦點出現時喚回；Esc 或閒置 `delay` 毫秒後淡出。
 * 指標停在 chrome 上、其中有鍵盤焦點（`:focus-visible`）或展開的控制項（`aria-expanded="true"`）、
 * 或 part 自己要求保留時不淡出。隱藏只是透明，控制項仍在無障礙樹且可聚焦，不影響版面。
 */
@Directive({ selector: '[nxIdleChrome]', exportAs: 'nxIdleChrome' })
export class IdleChrome {
  /** 閒置多久後淡出（毫秒）。 */
  readonly delay = input(2000, { alias: 'nxIdleChromeDelay' });
  readonly shown = signal(true);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  private readonly parts = new Set<Part>();
  private timer?: ReturnType<typeof setTimeout>;
  private press?: { type: string; x: number; y: number };
  constructor() {
    const listeners: [string, (event: never) => void][] = [
      ['pointermove', (event: PointerEvent) => this.move(event)],
      ['pointerdown', (event: PointerEvent) => this.down(event)],
      ['click', (event: MouseEvent) => this.tap(event)],
      ['focusin', (event: FocusEvent) => this.focus(event)],
      ['keydown', (event: KeyboardEvent) => this.key(event)],
      // 範圍是對話框時，每次開啟都短暫出現。
      ['toggle', (event: ToggleEvent) => event.newState === 'open' && this.reveal()],
    ];
    for (const [type, listener] of listeners)
      this.host.addEventListener(type, listener as EventListener, { passive: true });
    inject(DestroyRef).onDestroy(() => {
      clearTimeout(this.timer);
      for (const [type, listener] of listeners)
        this.host.removeEventListener(type, listener as EventListener);
    });
  }
  /** 顯示並重新計時；換文件或頁面時呼叫，讓使用者知道工具在哪裡。 */
  reveal() {
    this.shown.set(true);
    this.schedule();
  }
  hide() {
    clearTimeout(this.timer);
    if (!this.held()) this.shown.set(false);
  }
  /** @internal */
  add(part: Part) {
    this.parts.add(part);
    this.reveal();
    return () => {
      this.parts.delete(part);
      this.schedule();
    };
  }
  private schedule() {
    clearTimeout(this.timer);
    if (!this.parts.size) return;
    this.timer = setTimeout(() => {
      if (this.held()) this.schedule();
      else this.shown.set(false);
    }, this.delay());
  }
  private held() {
    for (const part of this.parts) {
      const element = part.element;
      if (
        part.hovered ||
        part.held() ||
        element.querySelector(':focus-visible, [aria-expanded="true"]')
      )
        return true;
    }
    return false;
  }
  private part(target: EventTarget | null) {
    if (!(target instanceof Node)) return undefined;
    for (const part of this.parts) if (part.element.contains(target)) return part;
    return undefined;
  }
  private move(event: PointerEvent) {
    if (event.pointerType === 'touch') return;
    for (const { element } of this.parts) {
      const box = element.getBoundingClientRect();
      if (
        event.clientX >= box.left - REACH &&
        event.clientX <= box.right + REACH &&
        event.clientY >= box.top - REACH &&
        event.clientY <= box.bottom + REACH
      )
        return this.reveal();
    }
  }
  private down(event: PointerEvent) {
    this.press = { type: event.pointerType, x: event.clientX, y: event.clientY };
    if (this.part(event.target)) this.reveal();
  }
  private tap(event: MouseEvent) {
    const press = this.press;
    this.press = undefined;
    if (press?.type !== 'touch' || this.part(event.target)) return;
    if (Math.hypot(event.clientX - press.x, event.clientY - press.y) > TAP_SLOP) return;
    const target = event.target as Element | null;
    if (target?.closest?.(INTERACTIVE)) return;
    if (this.shown()) this.hide();
    else this.reveal();
  }
  private focus(event: FocusEvent) {
    const target = event.target as Element;
    if (this.part(target) || target.matches?.(':focus-visible')) this.reveal();
  }
  private key(event: KeyboardEvent) {
    if (event.key === 'Escape' && this.shown()) this.hide();
  }
}

/**
 * 把 `element` 登記成最近 `IdleChrome` 的一部分，`active()` 為 false 時不參與（例如未放大的標題列）。
 * 回傳 `data-chrome` 的值：沒有範圍或未啟用為 null。必須在 injection context 呼叫。
 */
export function idleChromePart(
  element: HTMLElement,
  active: () => boolean,
  held: () => boolean = () => false,
): Signal<ChromeState | null> {
  const scope = inject(IdleChrome, { optional: true });
  if (!scope) return signal(null);
  const part: Part = { element, held, hovered: false };
  const enter = (event: PointerEvent) => {
    if (event.pointerType === 'touch') return;
    part.hovered = true;
    scope.reveal();
  };
  const leave = () => (part.hovered = false);
  element.addEventListener('pointerenter', enter, { passive: true });
  element.addEventListener('pointerleave', leave, { passive: true });
  inject(DestroyRef).onDestroy(() => {
    element.removeEventListener('pointerenter', enter);
    element.removeEventListener('pointerleave', leave);
  });
  const enabled = computed(active);
  effect((cleanup) => {
    if (enabled()) cleanup(untracked(() => scope.add(part)));
  });
  // 外部要求保留（例如搜尋中）時立即出現。
  effect(() => {
    if (enabled() && held()) untracked(() => scope.reveal());
  });
  return computed(() => (enabled() ? (scope.shown() ? 'shown' : 'hidden') : null));
}

/** 範本裡的 chrome：`<header nxIdleChromePart [nxIdleChromePart]="maximized()">`。 */
@Directive({
  selector: '[nxIdleChromePart]',
  host: { '[attr.data-chrome]': 'state()' },
})
export class IdleChromePart {
  readonly active = input(true, { alias: 'nxIdleChromePart', transform: booleanAttribute });
  protected readonly state = idleChromePart(
    inject<ElementRef<HTMLElement>>(ElementRef).nativeElement,
    () => this.active(),
  );
}
