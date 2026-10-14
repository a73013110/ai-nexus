import { Injectable, computed, inject, linkedSignal, signal } from '@angular/core';
import { apiResource } from '@core/api/api-resource';
import type { AdminUserDto } from '@core/api/schema';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { safeMessage } from '@core/errors/safe-errors';
import { ViewScope } from '@shared/browser/view-scope';
import { parseStorageLimitGb, storageLimitGb } from '@shared/browser/storage-limit';
import { ToastService } from '@shared/ui/toast-service';
import { AdministrationApi } from '../administration-api';
import { AdministrationStore } from '../administration-store';
import {
  modelPolicyDraft,
  modelPolicyRequest,
  sameModelPolicy,
  type ModelPolicyDraft,
} from '../model-policies/model-policy-editor';
import {
  sameUserAccount,
  userAccountDraft,
  userAccountRequest,
  type UserAccountDraft,
} from './user-account-fields';
import { sameIds } from './user-role-fields';

/** A draft follows its saved value within one opening (`key`) of the inspector. */
type Keyed<T> = readonly [string | undefined, T];

export type UserTab = 'account' | 'roles' | 'models' | 'storage' | 'conversations';
/** The tabs that edit something; each saves on its own. */
export type UserSettingsTab = Exclude<UserTab, 'conversations'>;

/**
 * One user's management state for the inspector: the saved account, a draft per tab and the
 * reads behind them. Every opening is a new `key`, so drafts start from the saved values and
 * responses that arrive after the dialog moved on are dropped.
 */
@Injectable()
export class AdminUserStore {
  private readonly api = inject(AdministrationApi);
  private readonly admin = inject(AdministrationStore);
  private readonly scope = inject(ViewScope);
  private readonly toast = inject(ToastService);
  private readonly session = inject(WorkspaceSession);
  /** The user opened from the list; kept until the dialog has left so it does not empty. */
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
  /** Fields are read-only while a tab saves or while testing another identity. */
  readonly locked = computed(() => !!this.saving() || this.session.testing());

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
  /** The saved user: fresh from the detail read, the list row until it arrives. */
  readonly current = computed(() => this.overview()?.user ?? this.user());

  // Saved baselines follow the server and are set at once after a save, so a draft is never
  // reported as unsaved while the fresh read is on its way.
  private readonly savedAccount = linkedSignal<UserAccountDraft, UserAccountDraft>({
    source: () => userAccountDraft(this.current()),
    computation: (saved) => saved,
    equal: sameUserAccount,
  });
  readonly account = linkedSignal<Keyed<UserAccountDraft>, UserAccountDraft>({
    source: () => [this.key(), this.savedAccount()],
    computation: ([, saved]) => saved,
  });
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

  /** The model policy is read the first time its tab opens. */
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
    roles: !sameIds(this.roles(), this.savedRoles()),
    models: !!this.modelPolicy() && !sameModelPolicy(this.models(), this.savedModels()),
    storage: this.storage().trim() !== this.savedStorage(),
  }));

  open(user: AdminUserDto) {
    this.scope.cancel('inspector-search');
    this.user.set(user);
    this.visit.update((visit) => visit + 1);
  }
  /** Closed: drop the user, every draft and every read with them. */
  release() {
    this.scope.cancel('inspector-search');
    this.user.set(null);
  }
  select(tab: UserTab) {
    this.tab.set(tab);
    if (tab === 'models') this.modelsWanted.set(true);
  }
  /** Return a tab's draft to the saved values. */
  revert(tab: UserSettingsTab) {
    if (tab === 'account') this.account.set(this.savedAccount());
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
  /** Account and roles change grants: the catalog, list and own session are read again. */
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
