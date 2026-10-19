import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { currentUrl } from './current-url';
import { apiResource } from '../api/api-resource';
import { featureForUrl } from '../api/catalog';
import { WorkspaceSession } from '../auth/workspace-session';
import { Notice } from '@shared/ui/notice';
import { FeatureAccessNotice } from './feature-access-notice';
import { FeaturePage } from './feature-page';
import { WORKSPACE_HOME } from './workspace-home';

/**
 * 各區路由最後的 `**`：`featureGuard` 擋下的頁面，或不存在的網址。網址在目錄中是帳號沒有授權的頁面時，
 * 說明缺少哪個權限；有授權（例如多了一段路徑）或不在目錄中時，說明頁面不存在。
 */
@Component({
  selector: 'nx-unavailable-page',
  imports: [FeaturePage, FeatureAccessNotice, Notice, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<nx-feature-page [title]="title()">
    @if (account.loading()) {
      <p class="form-note" role="status">正在確認權限…</p>
    } @else if (account.error()) {
      <nx-notice [message]="account.error()"
        ><button notice-actions type="button" (click)="account.reload()">重新載入</button>
      </nx-notice>
    } @else if (missing(); as id) {
      <nx-feature-access-notice [feature]="id" />
    } @else {
      <nx-notice tone="warning" icon="search" message="找不到這個頁面，網址可能已變更或輸入錯誤。"
        ><a notice-actions class="secondary-button" [routerLink]="home">回到總覽</a>
      </nx-notice>
    }
  </nx-feature-page>`,
})
export class UnavailablePage {
  private readonly session = inject(WorkspaceSession);
  readonly home = WORKSPACE_HOME;
  readonly account = apiResource({ loader: () => this.session.load() });
  private readonly url = currentUrl();
  /** 網址所屬、但此帳號沒有授權的頁面。 */
  readonly missing = computed(() => {
    this.account.value();
    const feature = featureForUrl(this.url());
    return feature?.kind === 'page' && this.session.me() && !this.session.has(feature.id)
      ? feature.id
      : null;
  });
  readonly title = computed(() => {
    const id = this.missing();
    return id ? this.session.featureName(id) : '找不到頁面';
  });
}
