import { DestroyRef, Injectable, computed, inject, linkedSignal, signal } from '@angular/core';
import { apiResource } from '@core/api/api-resource';
import type { UserSettingsDto } from '@core/api/schema';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { safeMessage } from '@core/errors/safe-errors';
import { UserSettingsService, defaultSettings } from '@core/preferences/user-settings';
import { ViewScope } from '@shared/browser/view-scope';

/**
 * 個人設定的草稿，由 `SettingsPage` 提供、各分頁注入同一份。修改立即預覽在整個工作區
 * （`UserSettingsService.preview`），按儲存才寫回帳號；對話框關閉（元件銷毀）時還原為已儲存的值，
 * 未儲存的預覽不會留在畫面上。
 */
@Injectable()
export class SettingsStore {
  readonly session = inject(WorkspaceSession);
  private readonly service = inject(UserSettingsService);
  private readonly scope = inject(ViewScope);
  /** 設定與帳號一起重新讀取，絕不沿用其他裝置的快取。 */
  private readonly read = apiResource({
    freshAccount: true,
    loader: () => this.service.load(this.session.me()!.id, true),
  });
  /**
   * 按「重新載入」的次數。分頁自己的讀取（模型、網路搜尋、用量）以它當 `params`，
   * 所以重新載入設定時一併重讀，不必知道目前開著哪個分頁。
   */
  readonly version = signal(0);
  /** 伺服器上保存的值；草稿從它開始，並與它比較。 */
  readonly saved = linkedSignal<UserSettingsDto>(() =>
    structuredClone(this.read.value() ?? defaultSettings()),
  );
  readonly draft = linkedSignal<UserSettingsDto>(() => structuredClone(this.saved()));
  readonly loading = this.read.loading;
  readonly saving = signal(false);
  private readonly saveError = signal('');
  readonly error = computed(() => this.saveError() || this.read.error());
  /** 上一次儲存的結果，顯示在儲存列；再次修改時清掉。 */
  readonly notice = signal('');
  readonly changed = computed(() => JSON.stringify(this.draft()) !== JSON.stringify(this.saved()));

  constructor() {
    inject(DestroyRef).onDestroy(() => this.service.restore());
  }

  reload() {
    this.saveError.set('');
    this.read.reload();
    this.version.update((value) => value + 1);
  }

  update<K extends keyof UserSettingsDto>(key: K, value: UserSettingsDto[K]) {
    this.edit((current) => ({ ...current, [key]: value }));
  }

  appearance<K extends keyof UserSettingsDto['appearance']>(
    key: K,
    value: UserSettingsDto['appearance'][K],
  ) {
    this.edit((current) => ({ ...current, appearance: { ...current.appearance, [key]: value } }));
  }

  revert() {
    this.draft.set(structuredClone(this.saved()));
    this.service.restore();
    this.notice.set('');
  }

  async save() {
    const current = this.scope.guard();
    this.saving.set(true);
    this.saveError.set('');
    try {
      const value = await this.service.save(this.draft());
      if (!current()) return;
      this.saved.set(structuredClone(value));
      this.notice.set('已儲存，此帳號的其他裝置也會套用。');
    } catch (error) {
      if (current()) this.saveError.set(safeMessage(error));
    } finally {
      if (current()) this.saving.set(false);
    }
  }

  private edit(change: (current: UserSettingsDto) => UserSettingsDto) {
    this.draft.update(change);
    this.service.preview(this.draft());
    this.notice.set('');
  }
}
