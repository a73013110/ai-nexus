import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  effect,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { AuthService } from '../../core/auth/auth-service';
import { Icon } from './icon';

@Component({
  selector: 'nx-identity-banner',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `@if (auth.session()?.testing; as testing) {
    <div #banner class="identity-banner" role="region" aria-label="管理者測試身分">
      <nx-icon name="shield" />
      <span
        ><strong>測試身分：{{ auth.session()?.displayName || auth.session()?.account }}</strong>
        <small>來源：{{ testing.administratorName }} · 限時 15 分鐘 · 操作會留下稽核</small></span
      >
      <button class="secondary-button" [disabled]="busy()" (click)="restore()">
        {{ busy() ? '正在返回…' : '返回管理者' }}
      </button>
      @if (error()) {
        <p role="alert">{{ error() }}</p>
      }
    </div>
  }`,
})
export class IdentityBanner {
  readonly auth = inject(AuthService);
  readonly busy = signal(false);
  readonly error = signal('');
  private readonly banner = viewChild<ElementRef<HTMLElement>>('banner');
  constructor() {
    effect((onCleanup) => {
      const test = this.auth.session()?.testing;
      if (!test) {
        document.documentElement.style.removeProperty('--identity-banner-height');
        return;
      }
      const timer = setTimeout(
        () => void this.restore(),
        Math.max(0, new Date(test.expiresAt).getTime() - Date.now()),
      );
      onCleanup(() => clearTimeout(timer));
    });
    effect((onCleanup) => {
      const element = this.banner()?.nativeElement;
      if (!element) return;
      const observer = new ResizeObserver((entries) =>
        document.documentElement.style.setProperty(
          '--identity-banner-height',
          entries[0].borderBoxSize[0].blockSize + 'px',
        ),
      );
      observer.observe(element);
      onCleanup(() => observer.disconnect());
    });
    inject(DestroyRef).onDestroy(() =>
      document.documentElement.style.removeProperty('--identity-banner-height'),
    );
  }
  async restore() {
    if (this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    try {
      await this.auth.endTestIdentity();
    } catch (error) {
      this.error.set(error instanceof Error ? error.message : '返回未完成，請重新連線後再試。');
      this.busy.set(false);
    }
  }
}
