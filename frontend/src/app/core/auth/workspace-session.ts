import { Injectable, computed, effect, inject, signal, untracked } from '@angular/core';
import { AuthService } from './auth-service';
import { WorkspaceApi } from '../api/workspace-api';
import type { MeDto } from '../api/schema';
import { UserSettingsService } from '../preferences/user-settings';
import { FEATURE_NAMES } from './feature-names';

/** 管理寫入因帳號正在測試其他身分而被拒絕時顯示。 */
export const TESTING_IDENTITY_NOTE = '測試身分期間不能修改管理設定';

/** 功能頁共用的帳號脈絡；它永遠不載入對話歷史。 */
@Injectable({ providedIn: 'root' })
export class WorkspaceSession {
  readonly auth = inject(AuthService);
  readonly me = signal<MeDto | null>(null);
  private readonly api = inject(WorkspaceApi);
  readonly settings = inject(UserSettingsService);
  private generation = -1;
  /** 管理員正以其他身分操作；期間管理寫入會被拒絕。 */
  readonly testing = computed(() => !!this.auth.session()?.testing);
  /** 測試期間為 {@link TESTING_IDENTITY_NOTE}，否則為空（供儲存列的 `note` 使用）。 */
  readonly testingNote = computed(() => (this.testing() ? TESTING_IDENTITY_NOTE : ''));
  constructor() {
    effect(() => {
      const generation = this.auth.generation();
      if (generation !== this.generation) untracked(() => this.me.set(null));
    });
  }
  adopt(me: MeDto) {
    this.generation = this.auth.generation();
    this.me.set(me);
  }
  async load(refresh = false) {
    if (!refresh && this.me() && this.generation === this.auth.generation()) return this.me();
    const current = this.auth.guard();
    if (!(await this.auth.requireLogin())) return null;
    const request = this.api.me();
    const [me] = await Promise.all([
      request,
      this.settings.load(
        request.then((value) => value.id),
        refresh,
      ),
    ]);
    if (!current()) return null;
    this.adopt(me);
    return me;
  }
  has(id: string) {
    return this.me()?.access.features?.some((item) => item.id === id) ?? false;
  }
  featureName(id: string) {
    return (
      this.me()?.access.features?.find((item) => item.id === id)?.name || FEATURE_NAMES[id] || id
    );
  }
}
