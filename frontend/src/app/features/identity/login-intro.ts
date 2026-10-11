import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { BrandWordmark } from '../../shared/ui/brand-wordmark';
import { FourierMark } from './fourier-mark';

/** The brand side of the login page; the copy reads at once, the wordmark waits for the drawn N. */
@Component({
  selector: 'section[nxLoginIntro]',
  styleUrl: './login-intro.scss',
  imports: [FourierMark, BrandWordmark],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'login-intro', '[class.intro-ready]': 'ready()' },
  template: `<div class="login-intro-layout">
      <div class="login-brand" [attr.aria-hidden]="!ready()">
        <nx-brand-wordmark #brand [markVisible]="ready()" />
        <small class="login-reveal">INTERNAL WORKSPACE</small>
      </div>
      <div class="login-intro-copy">
        <span class="intro-eyebrow">你的工作，從這裡接續</span>
        <h1>讓想法有個<br />專注的空間。</h1>
        <p>整理資訊、打磨文字，和 AI 一起推進手上的工作。</p>
      </div>
      <span class="login-footnote">公司帳號 · 個人工作區</span>
    </div>
    <nx-fourier-mark [anchor]="brand.markElement()" (finishedChange)="ready.set($event)" />`,
})
export class LoginIntro {
  readonly ready = signal(false);
}
