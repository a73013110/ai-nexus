import {
  afterRenderEffect,
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  effect,
  inject,
  input,
  type OnInit,
  signal,
  untracked,
} from '@angular/core';

/** 內容區內喚回工具列的操作；`pointerover` 涵蓋指標移進 iframe（之後的移動不會傳回本頁）。 */
const REVEAL_EVENTS = ['pointermove', 'pointerover', 'pointerdown', 'focusin'] as const;

/**
 * 浮在檢視器內容上方的內容工具（模式、頁碼、縮放、搜尋），讓對話框標題列只留視窗層級動作。
 * 放在包住內容、`position: relative` 的容器裡。開啟時、以及內容區內指標移動、觸控點擊或焦點進入時
 * 顯示，閒置 `delay` 毫秒後淡出；焦點或指標在工具列上、其中有展開的控制項（`aria-expanded="true"`，
 * 例如開著的 Select）或設定 `pinned` 時保持顯示。隱藏時控制項仍可聚焦，鍵盤 Tab 進入即喚回。
 */
@Component({
  selector: 'nx-floating-toolbar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './floating-toolbar.scss',
  host: { '[class.is-hidden]': '!shown()' },
  template: `<div
    class="floating-toolbar ui-density-compact"
    role="group"
    [attr.aria-label]="label()"
    (pointerenter)="hovered = true"
    (pointerleave)="hovered = false"
  >
    <ng-content />
  </div>`,
})
export class FloatingToolbar implements OnInit {
  readonly label = input.required<string>();
  /** 工具列浮在其上的內容區；預設為 host 的父元素。 */
  readonly region = input<HTMLElement | null>(null);
  /** 保持顯示，例如搜尋中有輸入時。 */
  readonly pinned = input(false);
  /** 閒置多久後淡出（毫秒）。 */
  readonly delay = input(2000);
  readonly shown = signal(true);
  protected hovered = false;
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private timer?: ReturnType<typeof setTimeout>;
  constructor() {
    afterRenderEffect((cleanup) => {
      const region = this.region() ?? this.host.nativeElement.parentElement;
      if (!region) return;
      const reveal = () => this.reveal();
      for (const type of REVEAL_EVENTS) region.addEventListener(type, reveal, { passive: true });
      cleanup(() => {
        for (const type of REVEAL_EVENTS) region.removeEventListener(type, reveal);
      });
    });
    effect(() => {
      if (this.pinned()) untracked(() => this.reveal());
    });
    inject(DestroyRef).onDestroy(() => clearTimeout(this.timer));
  }
  ngOnInit() {
    this.schedule();
  }
  reveal() {
    this.shown.set(true);
    this.schedule();
  }
  private schedule() {
    clearTimeout(this.timer);
    this.timer = setTimeout(() => {
      if (this.held()) this.schedule();
      else this.shown.set(false);
    }, this.delay());
  }
  private held() {
    const host = this.host.nativeElement;
    return (
      this.pinned() ||
      this.hovered ||
      host.contains(document.activeElement) ||
      !!host.querySelector('[aria-expanded="true"]')
    );
  }
}
