import {
  ChangeDetectionStrategy,
  Component,
  effect,
  ElementRef,
  inject,
  input,
  signal,
  viewChild,
} from '@angular/core';
import { ThemeService } from '@core/preferences/theme-service';
import { BrandWordmark } from '@shared/ui/brand-wordmark';
import { LoginWelcome } from './login-welcome';
import { LoginTrace } from './login-trace';
import { markPlayed, SignalMark } from './signal-mark';

export type LoginStage = 'credentials' | 'identity' | 'welcome';

/**
 * 登入頁的品牌區。文案會立即讀取；字標等待其 N 到位，
 * 兩條訊號先在文案上方的空白處繪出，之後才落到正確位置。登入後，側邊欄展開為歡迎畫面，字標移到中央，
 * 接著字標與頭像飛進工作區的側邊欄。
 */
@Component({
  selector: 'section[nxLoginIntro]',
  styleUrl: './login-intro.scss',
  imports: [SignalMark, LoginTrace, BrandWordmark, LoginWelcome],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'login-intro',
    '[class.intro-ready]': 'ready()',
    '[attr.data-stage]': 'stage()',
    '(pointermove)': 'follow($event)',
    '(pointerleave)': 'spot.set(null)',
    '[style.--spot-x]': 'spot()?.x',
    '[style.--spot-y]': 'spot()?.y',
    '[class.has-spot]': '!!spot()',
  },
  template: `<div class="login-intro-layout">
      <div class="login-brand" [attr.aria-hidden]="!ready()">
        <nx-brand-wordmark #brand [markVisible]="ready()" />
      </div>
      <div class="login-signal-slot" aria-hidden="true">
        <svg nxLoginTrace></svg>
      </div>
      @if (stage() === 'welcome') {
        <nx-login-welcome [name]="name()" [identity]="identity()" />
      } @else {
        <div class="login-intro-copy">
          <p class="intro-eyebrow"><span lang="en">nex·us</span><span>連結各方的交會點</span></p>
          <h1>思考與流程，<br />在此<em>交會</em>。</h1>
          <p class="intro-lede">和 AI 一起整理資訊、推進專案，日常的工作流程也從這裡接續。</p>
        </div>
      }
    </div>
    <nx-signal-mark
      [anchor]="brand.markElement()"
      [origin]="trace().nativeElement"
      (finishedChange)="ready.set($event)"
    />`,
})
export class LoginIntro {
  private readonly themes = inject(ThemeService);
  readonly stage = input<LoginStage>('credentials');
  readonly name = input('');
  /** 帳號需要選擇時，上一步所選的身分。 */
  readonly identity = input<string | null>(null);
  /** 標記已播放過時（例如剛登出後）一開始就就緒。 */
  readonly ready = signal(markPlayed() || this.themes.reducedMotion());
  readonly spot = signal<{ x: string; y: string } | null>(null);
  private readonly mark = viewChild.required(SignalMark);
  readonly trace = viewChild.required(LoginTrace, { read: ElementRef });
  private frame = 0;
  constructor() {
    // N 還在繪製時登入，會讓它定格，字標才能完整移動。
    effect(() => {
      if (this.stage() === 'credentials') return;
      if (!this.mark().complete()) this.mark().finish();
      this.spot.set(null);
    });
  }
  /** 柔和的光會跟著精細指標在點陣上移動；觸控與減少動態效果會略過。 */
  follow(event: PointerEvent) {
    if (event.pointerType !== 'mouse' || this.themes.reducedMotion() || this.stage() === 'welcome')
      return;
    const host = event.currentTarget as HTMLElement;
    cancelAnimationFrame(this.frame);
    this.frame = requestAnimationFrame(() => {
      const box = host.getBoundingClientRect();
      this.spot.set({ x: `${event.clientX - box.left}px`, y: `${event.clientY - box.top}px` });
    });
  }
}
