import { Notice } from './notice';
import { safeMessage } from '../../core/api/safe-errors';
import { ChangeDetectionStrategy, Component, effect, inject, signal } from '@angular/core';
import { AuthService } from '../../core/auth/auth-service';
import { Icon } from './icon';
import { ViewportInset } from '../browser/viewport-inset';

@Component({
  selector: 'nx-identity-banner',
  imports: [Notice, Icon, ViewportInset],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `@if (auth.session()?.testing; as testing) {
    <div
      nxViewportInset="--identity-banner-height"
      class="identity-banner"
      role="region"
      aria-label="管理者測試身分"
    >
      <nx-icon name="shield" />
      <span
        ><strong>測試身分：{{ auth.session()?.displayName || auth.session()?.account }}</strong>
        <small>來源：{{ testing.administratorName }} · 限時 15 分鐘 · 操作會留下稽核</small></span
      >
      <button class="secondary-button" [disabled]="busy()" (click)="restore()">
        {{ busy() ? '正在返回…' : '返回管理者' }}
      </button>
      @if (error()) {
        <nx-notice [message]="error()" />
      }
    </div>
  }`,
})
export class IdentityBanner {
  readonly auth = inject(AuthService);
  readonly busy = signal(false);
  readonly error = signal('');
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
  }
  async restore() {
    if (this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    try {
      await this.auth.endTestIdentity();
    } catch (error) {
      this.error.set(safeMessage(error));
      this.busy.set(false);
    }
  }
}
