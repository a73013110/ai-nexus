import { effect, inject, Injectable, untracked } from '@angular/core';
import { ApiClient } from '@core/api/api-client';
import { ToastService } from '@shared/ui/toast-service';

/** 本機開發與測試的版號（angular.json 的預設）；這時前後端各自建置，版號不必相同。 */
export const DEVELOPMENT_VERSION = '0.0.0-dev';

/** 這個分頁載入的前端是否已不是伺服器正在跑的版本。 */
export function outdated(client: string, server: string | null): server is string {
  return !!server && client !== DEVELOPMENT_VERSION && server !== client;
}

/**
 * 網站更新後，還開著的分頁仍是舊的前端：登入後的 API 回應帶 `X-App-Version`，和 `APP_VERSION` 不同時
 * 以提示加「重新整理」告知（ADR 0028）。不自動重新整理，避免吃掉還沒送出的輸入；同一分頁同一版只提示一次。
 */
@Injectable({ providedIn: 'root' })
export class AppUpdateNotice {
  private readonly api = inject(ApiClient);
  private readonly toasts = inject(ToastService);
  private notified = '';
  constructor() {
    effect(() => {
      const server = this.api.serverVersion();
      if (!outdated(APP_VERSION, server) || server === this.notified) return;
      this.notified = server;
      untracked(() =>
        this.toasts.show({
          id: 'app-update',
          tone: 'info',
          title: `已更新到 v${server}`,
          description: '重新整理後使用新版；尚未送出的內容請先儲存。',
          action: { label: '重新整理', run: () => window.location.reload() },
          duration: 0,
        }),
      );
    });
  }
}
