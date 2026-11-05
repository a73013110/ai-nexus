import { inject, Injectable, signal } from '@angular/core';
import { WorkspaceApi } from '@core/api/workspace-api';
import { AuthService } from '@core/auth/auth-service';
import { ModelChoice } from '@features/platform/inference/model-choice';

const CHECKING_SEARCH = { available: false, notice: '正在確認網路搜尋設定…' };

/** 對話用的模型選擇（`ModelChoice`），另加請求是否可加入網路搜尋。 */
@Injectable({ providedIn: 'root' })
export class ChatModels extends ModelChoice {
  private readonly api = inject(WorkspaceApi);
  private readonly auth = inject(AuthService);
  readonly webSearchEnabled = signal(false);
  readonly webSearchStatus = signal(CHECKING_SEARCH);

  async loadWebSearch() {
    const current = this.auth.guard();
    try {
      const status = await this.api.webSearchStatus();
      if (current()) this.webSearchStatus.set(status);
    } catch {
      if (current())
        this.webSearchStatus.set({
          available: false,
          notice: '暫時無法確認搜尋服務，請重新連線。',
        });
    }
  }

  reset() {
    this.models.set([]);
    this.webSearchEnabled.set(false);
    this.webSearchStatus.set(CHECKING_SEARCH);
  }
}
