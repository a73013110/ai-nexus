import { Injectable, computed, effect, inject, signal, untracked } from '@angular/core';
import { AuthService } from './auth-service';
import { WorkspaceApi } from '../api/workspace-api';
import type { MeDto } from '../api/schema';
import { UserSettingsService } from '../preferences/user-settings';
import { catalogFeature, type FeatureId } from '../api/catalog';
import type { DataScopeId } from '../api/feature-catalog';

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
  has(id: FeatureId) {
    return this.me()?.access.features?.some((item) => item.id === id) ?? false;
  }
  /** 功能的有效資料範圍（含主管範圍，由窄到寬）；未授權或功能沒有範圍時為空。只拿來決定版面，資料一律由伺服器依範圍篩選。 */
  scopes(id: FeatureId): readonly DataScopeId[] {
    return (this.me()?.access.dataScopes[id] ?? []) as DataScopeId[];
  }
  /** 看得到本人以外的資料：決定要不要顯示選人、選單位等篩選，讓只看得到自己的人一進頁面就是最終版面。 */
  seesOthers(id: FeatureId) {
    return this.scopes(id).some((scope) => scope !== 'own');
  }
  /**
   * 管理員在 `/me` 覆寫的名稱優先，其次是目錄的預設名稱（載入前、未授予的功能、閱讀器）。
   * `id` 可以是 API 給的任意字串（例如監控區域）；不認得時原樣顯示。
   */
  featureName(id: string) {
    return (
      this.me()?.access.features?.find((item) => item.id === id)?.name ||
      catalogFeature(id)?.name ||
      id
    );
  }
}
