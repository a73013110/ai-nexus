import { Injectable, computed, inject, linkedSignal, signal } from '@angular/core';
import { apiResource } from '@core/api/api-resource';
import type { AdminUserDto } from '@core/api/schema';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { safeMessage } from '@core/errors/safe-errors';
import { ViewScope } from '@shared/browser/view-scope';
import { parseStorageLimitGb, storageLimitGb } from '@shared/browser/storage-limit';
import { ToastService } from '@shared/ui/toast-service';
import { IdentityApi } from '../identity-api';
import { AdminUsersStore } from '../admin-users-store';
import {
  modelPolicyDraft,
  modelPolicyRequest,
  sameModelPolicy,
  type ModelPolicyDraft,
} from '@features/platform/inference/model-policies/model-policy-editor';
import {
  sameUserAccount,
  userAccountDraft,
  userAccountRequest,
  type UserAccountDraft,
} from './user-account-fields';
import { sameIds } from '@features/admin/access-control/roles/same-ids';

/** 草稿在檢視器的同一次開啟（`key`）內跟隨已儲存的值。 */
type Keyed<T> = readonly [string | undefined, T];

export type UserTab = 'account' | 'email' | 'roles' | 'models' | 'storage' | 'conversations';
/** 會編輯某些內容的分頁；各自獨立儲存。 */
export type UserSettingsTab = Exclude<UserTab, 'conversations'>;

/**
 * 單一使用者在檢視器中的管理狀態：已儲存的帳號、每個分頁的草稿，以及它們背後的讀取。
 * 每次開啟都是新的 `key`，讓草稿從已儲存的值開始；對話框已切換後才抵達的回應會被丟棄。
 */
@Injectable()
export class AdminUserStore {
  private readonly api = inject(IdentityApi);
  private readonly admin = inject(AdminUsersStore);
  private readonly scope = inject(ViewScope);
  private readonly toast = inject(ToastService);
  private readonly session = inject(WorkspaceSession);
  /** 從列表開啟的使用者；對話框退場前保留，避免內容被清空。 */
  readonly user = signal<AdminUserDto | null>(null);
  private readonly visit = signal(0);
  readonly key = computed(() => {
    const id = this.user()?.id;
    return id ? `${id}#${this.visit()}` : undefined;
  });
  readonly tab = linkedSignal<string | undefined, UserTab>({
    source: this.key,
    computation: () => 'account',
  });
  readonly saving = linkedSignal<string | undefined, UserSettingsTab | null>({
    source: this.key,
    computation: () => null,
  });
  readonly errors = linkedSignal<string | undefined, Partial<Record<UserSettingsTab, string>>>({
    source: this.key,
    computation: () => ({}),
  });
  /** 改綁或解除 AD 綁定送出中；不經帳號表單的儲存，錯誤顯示在帳號分頁。 */
  readonly binding = linkedSignal<string | undefined, 'rebind' | 'unbind' | null>({
    source: this.key,
    computation: () => null,
  });
  /** 分頁儲存中、AD 綁定送出中或測試其他身分時，欄位唯讀。 */
  readonly locked = computed(() => !!this.saving() || !!this.binding() || this.session.testing());

  private readonly request = computed(() => {
    const key = this.key(),
      id = this.user()?.id;
    return key && id ? { key, id } : undefined;
  });
  private readonly overviewRead = apiResource({
    params: () => this.request(),
    loader: async ({ key, id }) => ({ key, data: await this.api.insights(id) }),
  });
  readonly overview = computed(() => {
    const loaded = this.overviewRead.value();
    return loaded && loaded.key === this.key() ? loaded.data : null;
  });
  readonly overviewError = this.overviewRead.error;
  /** 詳情讀取中（含寫入後重讀）；還沒有詳情時摘要的數字畫骨架。 */
  readonly overviewRefreshing = this.overviewRead.refreshing;
  /** 已儲存的使用者：詳情讀取回來前用列表的資料。 */
  readonly current = computed(() => this.overview()?.user ?? this.user());

  // 已儲存的基準會跟隨伺服器，儲存後立即設定；如此在新讀取還在途中時，
  // 草稿不會被誤判為未儲存。
  private readonly savedAccount = linkedSignal<UserAccountDraft, UserAccountDraft>({
    source: () => userAccountDraft(this.current()),
    computation: (saved) => saved,
    equal: sameUserAccount,
  });
  readonly account = linkedSignal<Keyed<UserAccountDraft>, UserAccountDraft>({
    source: () => [this.key(), this.savedAccount()],
    computation: ([, saved]) => saved,
  });
  private readonly savedEmail = linkedSignal(() => this.current()?.email ?? '');
  readonly email = linkedSignal<Keyed<string>, string>({
    source: () => [this.key(), this.savedEmail()],
    computation: ([, saved]) => saved,
  });
  /** AD 提供的信箱由目錄同步，下次登入會覆寫，所以管理員只能檢視。 */
  readonly emailEditable = computed(() => this.current()?.emailSource !== 'directory');
  private readonly savedRoles = linkedSignal<readonly string[], readonly string[]>({
    source: () => this.current()?.roleIds ?? [],
    computation: (saved) => saved,
    equal: sameIds,
  });
  readonly roles = linkedSignal<Keyed<readonly string[]>, string[]>({
    source: () => [this.key(), this.savedRoles()],
    computation: ([, saved]) => [...saved],
  });
  private readonly savedStorage = linkedSignal(() =>
    storageLimitGb(this.current()?.storage?.personalLimitBytes),
  );
  readonly storage = linkedSignal<Keyed<string>, string>({
    source: () => [this.key(), this.savedStorage()],
    computation: ([, saved]) => saved,
  });

  /** 模型政策在其分頁第一次開啟時讀取。 */
  private readonly modelsWanted = linkedSignal(() => (this.key(), false));
  private readonly modelsRead = apiResource({
    params: () => (this.modelsWanted() ? this.request() : undefined),
    loader: async ({ key, id }) => ({ key, data: await this.api.modelPolicy(id) }),
  });
  readonly modelPolicy = computed(() => {
    const loaded = this.modelsRead.value();
    return loaded && loaded.key === this.key() ? loaded.data : null;
  });
  readonly loadingModels = this.modelsRead.loading;
  /** 重新載入模型政策（失敗後重試）中。 */
  readonly refreshingModels = this.modelsRead.refreshing;
  readonly modelsError = this.modelsRead.error;
  private readonly savedModels = linkedSignal<ModelPolicyDraft, ModelPolicyDraft>({
    source: () => modelPolicyDraft(this.modelPolicy()?.personal),
    computation: (saved) => saved,
    equal: sameModelPolicy,
  });
  readonly models = linkedSignal<Keyed<ModelPolicyDraft>, ModelPolicyDraft>({
    source: () => [this.key(), this.savedModels()],
    computation: ([, saved]) => saved,
  });

  readonly dirty = computed<Record<UserSettingsTab, boolean>>(() => ({
    account: !sameUserAccount(this.account(), this.savedAccount()),
    email: this.email().trim() !== this.savedEmail(),
    roles: !sameIds(this.roles(), this.savedRoles()),
    models: !!this.modelPolicy() && !sameModelPolicy(this.models(), this.savedModels()),
    storage: this.storage().trim() !== this.savedStorage(),
  }));

  open(user: AdminUserDto) {
    this.scope.cancel('inspector-search');
    this.user.set(user);
    this.visit.update((visit) => visit + 1);
  }
  /** 關閉時丟棄使用者、所有草稿與所有相關讀取。 */
  release() {
    this.scope.cancel('inspector-search');
    this.user.set(null);
  }
  select(tab: UserTab) {
    this.tab.set(tab);
    if (tab === 'models') this.modelsWanted.set(true);
  }
  /** 把分頁的草稿還原為已儲存的值。 */
  revert(tab: UserSettingsTab) {
    if (tab === 'account') this.account.set(this.savedAccount());
    if (tab === 'email') this.email.set(this.savedEmail());
    if (tab === 'roles') this.roles.set([...this.savedRoles()]);
    if (tab === 'models') this.models.set(this.savedModels());
    if (tab === 'storage') this.storage.set(this.savedStorage());
    this.errors.update((errors) => ({ ...errors, [tab]: '' }));
  }
  reloadModels() {
    this.errors.update((errors) => ({ ...errors, models: '' }));
    this.modelsRead.reload();
  }

  saveAccount() {
    return this.save(
      'account',
      (id) => this.api.updateUser(id, userAccountRequest(this.account(), [...this.savedRoles()])),
      () => {
        const draft = this.account();
        this.savedAccount.set({
          ...draft,
          password: '',
          hasLocalPassword: draft.localEnabled && (draft.hasLocalPassword || !!draft.password),
        });
        this.changed('帳號已儲存', '停用、登入方式或密碼變更會讓舊的工作階段失效。');
      },
    );
  }
  saveEmail() {
    const email = this.email().trim();
    return this.save(
      'email',
      (id) => this.api.email(id, email || null),
      () => {
        this.savedEmail.set(email);
        this.overviewRead.reload();
        this.toast.success(
          email ? '通知信箱已設定' : '通知信箱已清除',
          email ? '本人驗證前不會用來寄驗證碼。' : undefined,
        );
        this.admin.readUsers();
      },
    );
  }
  saveRoles() {
    return this.save(
      'roles',
      (id) => this.api.roles(id, this.roles()),
      () => {
        this.savedRoles.set(this.roles());
        this.changed('角色已儲存', '授權變更會在下一次操作生效。');
      },
    );
  }
  saveModels() {
    return this.save(
      'models',
      (id) => this.api.saveModelPolicy(id, modelPolicyRequest(this.models())),
      () => {
        this.savedModels.set(this.models());
        this.modelsRead.reload();
        this.toast.success('個人模型政策已儲存', '下次生成生效。');
        this.admin.readUsers();
      },
    );
  }
  saveStorage() {
    return this.save(
      'storage',
      async (id) => {
        const bytes = parseStorageLimitGb(this.storage());
        await this.api.storage(id, bytes);
        return bytes;
      },
      (bytes) => {
        this.savedStorage.set(storageLimitGb(bytes as number | null));
        this.overviewRead.reload();
        this.toast.success('個人容量上限已更新');
        this.admin.readUsers();
      },
    );
  }
  rebind(adAccount: string) {
    return this.bind(
      'rebind',
      (id) => this.api.rebindDirectory(id, adAccount),
      'AD 帳號已改綁',
      '角色與歷史資料沿用；舊的工作階段已失效。',
    );
  }
  unbind() {
    return this.bind(
      'unbind',
      (id) => this.api.unbindDirectory(id),
      'AD 綁定已解除',
      '下次以 AD 帳號驗證或從 AD 匯入時重新綁定。',
    );
  }
  /** 成功回傳 true，呼叫端據此清空輸入；失敗訊息放在帳號分頁。 */
  private async bind(
    action: 'rebind' | 'unbind',
    work: (id: string) => Promise<unknown>,
    title: string,
    description: string,
  ) {
    const key = this.key(),
      id = this.user()?.id,
      alive = this.scope.guard(),
      valid = () => alive() && this.key() === key;
    if (!id || this.locked()) return false;
    this.binding.set(action);
    this.errors.update((errors) => ({ ...errors, account: '' }));
    try {
      await work(id);
      if (valid()) this.changed(title, description);
      return valid();
    } catch (error) {
      if (valid()) this.errors.update((errors) => ({ ...errors, account: safeMessage(error) }));
      return false;
    } finally {
      if (valid()) this.binding.set(null);
    }
  }
  /** 帳號與角色的授權變更：目錄、列表與自己的工作階段會重新讀取。 */
  private changed(title: string, description: string) {
    this.overviewRead.reload();
    this.admin.saved(title, description);
  }
  private async save(
    tab: UserSettingsTab,
    work: (id: string) => Promise<unknown>,
    done: (result: unknown) => void,
  ) {
    const key = this.key(),
      id = this.user()?.id,
      alive = this.scope.guard(),
      valid = () => alive() && this.key() === key;
    if (!id || this.locked()) return;
    this.saving.set(tab);
    this.errors.update((errors) => ({ ...errors, [tab]: '' }));
    try {
      const result = await work(id);
      if (valid()) done(result);
    } catch (error) {
      if (valid()) this.errors.update((errors) => ({ ...errors, [tab]: safeMessage(error) }));
    } finally {
      if (valid()) this.saving.set(null);
    }
  }
}
