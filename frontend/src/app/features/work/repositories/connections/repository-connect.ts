import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import type { RepositoryStatusDto } from '@core/api/schema';
import { ViewScope } from '@shared/browser/view-scope';
import { Field } from '@shared/ui/field';
import { Icon } from '@shared/ui/icon';
import { Notice } from '@shared/ui/notice';
import { RepositoriesApi } from '../repositories-api';

/** 以三個簡短步驟連接（或重新授權）使用者自己的 Gitea token。 */
@Component({
  selector: 'nx-repository-connect',
  imports: [Field, Icon, Notice],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './repository-connect.scss',
  template: `<section class="connect" aria-labelledby="connect-title">
    <div class="intro">
      <span class="mark" aria-hidden="true"><nx-icon name="git" /></span>
      <h2 id="connect-title">{{ reconnecting() ? '更新 Gitea 授權' : '連接你的 Gitea' }}</h2>
      <p>
        以你自己的 Gitea 權限閱讀程式庫；看得到什麼，就只能讀到什麼。AI Nexus
        管理員不會因此取得你的私人程式庫。
      </p>
      <ul class="facts">
        <li><nx-icon name="lock" />權杖只在伺服器端加密保存，不會再顯示</li>
        <li><nx-icon name="eye" />預設只讀取，不建立 commit、議題或 PR</li>
      </ul>
    </div>
    <ol class="steps">
      <li>
        <span class="step" aria-hidden="true">1</span>
        <div>
          <strong>在 Gitea 建立個人權杖</strong>
          <p>到「設定 → 應用程式」，勾選以下權限：</p>
          <div class="scopes">
            <code>read:user</code><code>read:repository</code><code>read:issue</code>
            <code class="optional" title="只在要把 AI 檢閱結果貼回 Pull Request 時需要"
              >write:issue（選用）</code
            >
          </div>
          <a
            class="quiet-button"
            [href]="status().baseUrl + 'user/settings/applications'"
            target="_blank"
            rel="noopener noreferrer"
            ><nx-icon name="external" />開啟 Gitea 權杖設定</a
          >
        </div>
      </li>
      <li>
        <span class="step" aria-hidden="true">2</span>
        <form (submit)="connect($event)">
          <label for="gitea-token"><strong>貼上權杖</strong></label>
          <input
            nxField
            id="gitea-token"
            type="password"
            minlength="20"
            maxlength="512"
            required
            autocomplete="off"
            spellcheck="false"
            placeholder="40 字元的個人存取權杖"
            [value]="token()"
            (input)="token.set($any($event.target).value)"
          />
          @if (error()) {
            <nx-notice [message]="error()" />
          }
          <div class="actions">
            @if (reconnecting()) {
              <button type="button" class="secondary-button" (click)="cancelled.emit()">
                取消
              </button>
            }
            <button
              type="submit"
              class="primary-button"
              [class.busy]="busy()"
              [disabled]="busy() || !token().trim()"
            >
              @if (busy()) {
                <nx-icon class="verifying-icon" name="loading" />驗證中…
              } @else {
                {{ reconnecting() ? '更新授權' : '連線 Gitea' }}
              }
            </button>
          </div>
        </form>
      </li>
    </ol>
  </section>`,
})
export class RepositoryConnect {
  readonly status = input.required<RepositoryStatusDto>();
  readonly reconnecting = input(false);
  readonly connected = output<RepositoryStatusDto>();
  readonly cancelled = output<void>();
  private readonly api = inject(RepositoriesApi);
  private readonly scope = inject(ViewScope);
  readonly token = signal('');
  readonly busy = signal(false);
  readonly error = signal('');
  constructor() {
    inject(DestroyRef).onDestroy(() => this.token.set(''));
  }
  async connect(event: Event) {
    event.preventDefault();
    const token = this.token().trim();
    if (this.busy() || !token) return;
    const valid = this.scope.guard();
    this.token.set('');
    this.busy.set(true);
    this.error.set('');
    try {
      const status = await this.api.connect(token);
      if (valid()) this.connected.emit(status);
    } catch (error) {
      if (valid()) this.error.set(this.scope.message(error));
    } finally {
      if (valid()) this.busy.set(false);
    }
  }
}
