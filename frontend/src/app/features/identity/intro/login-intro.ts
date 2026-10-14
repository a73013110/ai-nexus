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
 * The brand side of the login page. The copy reads at once; the wordmark waits for its N, which
 * two signals draw in the free space above the copy before it lands in place. After sign-in the side widens into the welcome screen, the wordmark moves to its centre, and
 * from there it and the avatar fly into the sidebar of the workspace.
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
  /** The identity chosen on the previous step, when the account had a choice to make. */
  readonly identity = input<string | null>(null);
  /** Ready from the start when the mark has already played, e.g. right after a sign-out. */
  readonly ready = signal(markPlayed() || this.themes.reducedMotion());
  readonly spot = signal<{ x: string; y: string } | null>(null);
  private readonly mark = viewChild.required(SignalMark);
  readonly trace = viewChild.required(LoginTrace, { read: ElementRef });
  private frame = 0;
  constructor() {
    // Signing in while the N is still being drawn settles it, so the wordmark can travel whole.
    effect(() => {
      if (this.stage() === 'credentials') return;
      if (!this.mark().complete()) this.mark().finish();
      this.spot.set(null);
    });
  }
  /** A soft light follows a fine pointer across the dot field; touch and reduced motion skip it. */
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
