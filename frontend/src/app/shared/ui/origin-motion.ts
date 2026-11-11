import {
  afterNextRender,
  DestroyRef,
  Directive,
  ElementRef,
  inject,
  Injector,
} from '@angular/core';
import { ThemeService } from '@core/preferences/theme-service';
import { recentActivation } from '@shared/browser/activation';
import { motionTiming } from '@shared/browser/motion-tokens';

/** 陰影畫在外框之外：靜止時的裁切讓出這段，動畫結束拿掉裁切時陰影才不會跳出來。 */
const SHADOW_ROOM = 96;

/** 靜止的對話框：不位移、不縮放，裁切在陰影之外。 */
const REST: Keyframe = {
  transformOrigin: '0 0',
  transform: 'none',
  clipPath: `inset(-${SHADOW_ROOM}px round ${SHADOW_ROOM}px)`,
};

type Box = Pick<DOMRectReadOnly, 'left' | 'top' | 'width' | 'height'>;

/**
 * `frame`（對話框）收進 `origin`（觸發它的控制項）時的樣子：等比縮到蓋住 `origin`、中心對齊，再裁成它的
 * 形狀。等比所以內容不變形；裁切寫在對話框自己的座標（縮放之前），所以要除回比例。比對話框還大的來源
 * 不放大對話框，只對齊中心。
 */
export function originKeyframe(frame: Box, origin: Box, radius: number): Keyframe {
  const scale = Math.min(1, Math.max(origin.width / frame.width, origin.height / frame.height));
  const shiftX = origin.left + origin.width / 2 - (frame.left + (frame.width * scale) / 2);
  const shiftY = origin.top + origin.height / 2 - (frame.top + (frame.height * scale) / 2);
  const insetX = Math.max(0, (frame.width - origin.width / scale) / 2);
  const insetY = Math.max(0, (frame.height - origin.height / scale) / 2);
  return {
    transformOrigin: '0 0',
    transform: `translate(${shiftX}px, ${shiftY}px) scale(${scale})`,
    clipPath: `inset(${insetY}px ${insetX}px round ${radius / scale}px)`,
  };
}

/**
 * 對話框從開啟它的控制項長出來，關閉時縮回去（共享元素的語彙：交代東西從哪來、回哪去）。
 * 來源是使用者剛啟動的控制項（`recentActivation`），開啟點不必傳；沒有來源（快捷鍵、計時器）、來源已不在
 * 畫面上、手機底部 sheet 或減少動態時不介入，由 dialogs.scss 的預設進出場處理。
 * 介入時在對話框標 `data-origin`，讓 CSS 只留淡入淡出；位移、縮放與裁切由這裡的動畫負責，
 * 都是合成器屬性、不重排內容，動畫中對話框照常可操作。
 */
@Directive({
  selector: 'dialog[nxOriginMotion]',
  host: { '(beforetoggle)': 'toggling($event)' },
})
export class OriginMotion {
  private readonly element = inject<ElementRef<HTMLDialogElement>>(ElementRef).nativeElement;
  private readonly injector = inject(Injector);
  private origin: Element | null = null;
  private animation?: Animation;
  constructor() {
    inject(DestroyRef).onDestroy(() => this.animation?.cancel());
  }
  /** `beforetoggle` 在 `open` 屬性改變之前同步送出：這時標上 `data-origin`，CSS 的進出場就已經是淡入淡出。 */
  toggling(event: Event) {
    const opening = (event as ToggleEvent).newState === 'open';
    this.animation?.cancel();
    if (opening) this.origin = recentActivation();
    const active = !!this.originBox();
    this.element.toggleAttribute('data-origin', active);
    if (!active) return;
    // 開啟：等這一輪渲染把內容放進來再量外框；關閉：對話框此刻還開著，直接量。
    if (opening) afterNextRender({ read: () => this.play(true) }, { injector: this.injector });
    else this.play(false);
  }
  /** 來源此刻在畫面上的方框與圓角；不該介入時為 null。 */
  private originBox() {
    const origin = this.origin;
    if (!origin?.isConnected || this.element.contains(origin) || !this.element.animate) return null;
    if (this.injector.get(ThemeService).reducedMotion()) return null;
    if (getComputedStyle(this.element).getPropertyValue('--dialog-sheet').trim() === '1')
      return null;
    const box = origin.getBoundingClientRect();
    if (!box.width || !box.height) return null;
    // 標準屬性的計算值已換算成 px（百分比除外）；膠囊與圓形以短邊的一半為上限。
    const radius = Number.parseFloat(getComputedStyle(origin).borderTopLeftRadius) || 0;
    return { box, radius: Math.min(radius, box.width / 2, box.height / 2) };
  }
  private play(opening: boolean) {
    const origin = this.originBox();
    const frame = this.element.getBoundingClientRect();
    if (!origin || !this.element.open || !frame.width || !frame.height) return;
    const collapsed = originKeyframe(frame, origin.box, origin.radius);
    this.animation = opening
      ? this.element.animate(
          [collapsed, REST],
          motionTiming(this.element, '--motion-morph', '--ease-morph'),
        )
      : // 退場與 CSS 的淡出、離開 top layer 同長（--motion）；停在終點直到下次開啟，不閃回原位。
        this.element.animate([REST, collapsed], {
          ...motionTiming(this.element, '--motion', '--ease-exit'),
          fill: 'forwards',
        });
  }
}
