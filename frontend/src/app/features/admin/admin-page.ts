import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { FeaturePage } from '../../shared/ui/feature-page';
import { Icon } from '../../shared/ui/icon';
import { RouterLink } from '@angular/router';
import { WorkspaceSession } from '../../core/auth/workspace-session';
import type {
  AdminCatalog,
  AdminUser,
  AdminUsers,
  AdminRole,
  AdminGroup,
  AdminFeature,
  AdminUsage,
} from '../../core/api/types';
import { AdminApi } from './admin-api';
import { Checkbox } from '../../shared/ui/checkbox';
import { SearchField } from '../../shared/ui/search-field';
import { AdminUserInspector } from './admin-user-inspector';
import { AdminAudit } from './admin-audit';
import {
  formatDate,
  formatNumber,
  formatBytes,
  formatDuration,
  formatModelName,
} from '../../shared/browser/format';
import { PriceBook } from '../billing/price-book';
import { FeatureSummary } from '../../shared/ui/feature-summary';
import { groupFeatures } from '../../core/feature-groups';
import { ActionMenu, type MenuAction } from '../../shared/ui/action-menu';
import { ConfirmDialog } from '../../shared/ui/confirm-dialog';

import { parseStorageLimitGb, storageLimitGb } from '../../shared/browser/storage-limit';

interface Editor {
  kind: 'user' | 'role' | 'group' | 'feature';
  id: string;
  name: string;
  subtitle: string;
  enabled: boolean;
  ids: string[];
  restricted: boolean;
  modelIds: string[];
  daily: string;
  storage: string;
  order: string;
  isNew: boolean;
  adEnabled: boolean;
  localEnabled: boolean;
  adAccount: string;
  localAccount: string;
  password: string;
  hasLocalPassword: boolean;
  profileChanged: boolean;
}
@Component({
  selector: 'nx-admin-page',
  imports: [
    FeaturePage,
    Icon,
    RouterLink,
    Checkbox,
    SearchField,
    AdminUserInspector,
    AdminAudit,
    PriceBook,
    FeatureSummary,
    ActionMenu,
    ConfirmDialog,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './admin-page.html',
})
export class AdminPage {
  readonly session = inject(WorkspaceSession);
  private readonly api = inject(AdminApi);
  readonly catalog = signal<AdminCatalog | null>(null);
  readonly users = signal<AdminUsers | null>(null);
  readonly inspected = signal<AdminUser | null>(null);
  readonly usage = signal<AdminUsage | null>(null);
  readonly loading = signal(true);
  readonly error = signal('');
  readonly notice = signal('');
  readonly tab = signal('users');
  readonly search = signal('');
  readonly editor = signal<Editor | null>(null);
  readonly saving = signal(false);
  readonly testing = signal<AdminUser | null>(null);
  readonly testReason = signal('驗證角色權限與功能操作');
  readonly testError = signal('');
  readonly testDialog = viewChild.required<ElementRef<HTMLDialogElement>>('testDialog');
  readonly confirmation = viewChild.required(ConfirmDialog);
  readonly editorError = signal('');
  readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('editorDialog');
  readonly tabs = [
    { id: 'users', name: '使用者' },
    { id: 'roles', name: '角色' },
    { id: 'groups', name: '功能群組與模型' },
    { id: 'features', name: '功能' },
    { id: 'audit', name: '異動稽核' },
    { id: 'usage', name: '平台用量' },
  ];
  readonly choices = computed(() => {
    const e = this.editor(),
      c = this.catalog();
    if (!e || !c) return [];
    return e.kind === 'user' ? c.roles : e.kind === 'role' ? c.groups : c.features;
  });
  readonly groupedFeatures = computed(() => groupFeatures(this.catalog()?.features || []));
  readonly groupSummaries = computed(() =>
    Object.fromEntries(
      (this.catalog()?.groups || []).map((group) => [
        group.id,
        (this.catalog()?.features || []).filter((feature) => group.featureIds.includes(feature.id)),
      ]),
    ),
  );
  readonly proposedFeatures = computed(() => {
    const e = this.editor(),
      c = this.catalog();
    if (!e || !e.enabled || !['user', 'role'].includes(e.kind) || !c) return [];
    const groups = new Set(
      e.kind === 'role'
        ? e.ids
        : c.roles.filter((x) => x.enabled && e.ids.includes(x.id)).flatMap((x) => x.groupIds),
    );
    const features = new Set(
      c.groups.filter((x) => x.enabled && groups.has(x.id)).flatMap((x) => x.featureIds),
    );
    return c.features.filter((x) => x.enabled && features.has(x.id));
  });
  private version = 0;
  private alive = true;
  private timer?: ReturnType<typeof setTimeout>;
  constructor() {
    void this.load();
    inject(DestroyRef).onDestroy(() => {
      this.alive = false;
      this.version++;
      clearTimeout(this.timer);
    });
  }
  async load() {
    this.loading.set(true);
    this.error.set('');
    try {
      await this.session.load(true);
      if (!this.alive || !this.session.me()) return;
      if (!this.session.has('admin')) {
        this.error.set('你的帳號目前沒有平台管理權限。');
        return;
      }
      const catalog = await this.api.catalog();
      if (!this.alive) return;
      this.catalog.set(catalog);
      await this.loadUsers(0);
    } catch (error) {
      if (this.alive) this.error.set(this.message(error));
    } finally {
      if (this.alive) this.loading.set(false);
    }
  }
  async selectTab(id: string) {
    this.tab.set(id);
    this.error.set('');
    try {
      if (id === 'usage') {
        const value = await this.api.usage();
        if (this.alive) this.usage.set(value);
      }
    } catch (error) {
      if (this.alive) this.error.set(this.message(error));
    }
  }
  async loadUsers(offset = 0) {
    const version = ++this.version;
    try {
      const users = await this.api.users(this.search(), offset);
      if (this.alive && version === this.version) this.users.set(users);
    } catch (error) {
      if (this.alive && version === this.version) this.error.set(this.message(error));
    }
  }
  searchChanged(value: string) {
    this.search.set(value);
    clearTimeout(this.timer);
    this.timer = setTimeout(() => void this.loadUsers(), 250);
  }
  roleNames(ids: string[]) {
    return ids.map((id) => this.catalog()?.roles.find((x) => x.id === id)?.name || id);
  }
  groupNames(ids: string[]) {
    return ids.map((id) => this.catalog()?.groups.find((x) => x.id === id)?.name || id);
  }
  open(kind: Editor['kind'], item?: AdminUser | AdminRole | AdminGroup | AdminFeature) {
    this.editorError.set('');
    const user = kind === 'user' ? (item as AdminUser) : null,
      role = kind === 'role' ? (item as AdminRole) : null,
      group = kind === 'group' ? (item as AdminGroup) : null,
      feature = kind === 'feature' ? (item as AdminFeature) : null;
    this.editor.set({
      kind,
      id: item?.id || '',
      name: user?.displayName || role?.name || group?.name || feature?.name || '',
      subtitle: user?.account || feature?.route || '',
      enabled: role?.enabled ?? group?.enabled ?? feature?.enabled ?? true,
      ids:
        user?.roleIds || role?.groupIds || group?.featureIds || (kind === 'user' ? ['member'] : []),
      restricted: group?.policy?.allowedModelIds != null,
      modelIds: group?.policy?.allowedModelIds || [],
      daily: group?.policy?.dailyRequestLimit == null ? '' : String(group.policy.dailyRequestLimit),
      storage:
        group?.policy?.storedAttachmentLimitBytes == null
          ? ''
          : storageLimitGb(group.policy.storedAttachmentLimitBytes),
      order: String(feature?.sortOrder || 10),
      isNew: !item,
      adEnabled: user?.authentication?.adEnabled ?? !!user,
      localEnabled: user?.authentication?.localEnabled ?? !user,
      adAccount:
        user?.authentication?.adAccount ?? user?.account.split('\\').at(-1)?.split('@')[0] ?? '',
      localAccount: user?.authentication?.localAccount ?? '',
      password: '',
      hasLocalPassword: user?.authentication?.hasLocalPassword ?? false,
      profileChanged: !item,
    });
    if (user) this.update('enabled', user.enabled ?? true);
    if (user) this.editor.update((old) => (old ? { ...old, profileChanged: false } : null));
    this.dialog().nativeElement.showModal();
  }
  update<K extends keyof Editor>(key: K, value: Editor[K]) {
    this.editor.update((old) =>
      old
        ? {
            ...old,
            [key]: value,
            profileChanged:
              old.profileChanged ||
              (old.kind === 'user' &&
                [
                  'name',
                  'enabled',
                  'adEnabled',
                  'localEnabled',
                  'adAccount',
                  'localAccount',
                  'password',
                ].includes(key)),
          }
        : null,
    );
  }
  check(id: string, checked: boolean, models = false) {
    const key = models ? 'modelIds' : 'ids';
    const e = this.editor();
    if (e) this.update(key, checked ? [...e[key], id] : e[key].filter((x) => x !== id));
  }
  close() {
    if (!this.saving()) this.dialog().nativeElement.close();
  }
  cancel(event: Event) {
    if (this.saving()) event.preventDefault();
  }
  async save(event: Event) {
    event.preventDefault();
    const e = this.editor();
    if (!e) return;
    if ((!e.id && e.kind !== 'user') || !e.name.trim()) {
      this.editorError.set('請輸入識別碼與名稱。');
      return;
    }
    this.saving.set(true);
    this.editorError.set('');
    try {
      if (e.kind === 'user') {
        if (e.profileChanged)
          await this.api.saveUser(e.isNew ? null : e.id, {
            displayName: e.name,
            enabled: e.enabled,
            adEnabled: e.adEnabled,
            localEnabled: e.localEnabled,
            adAccount: e.adEnabled ? e.adAccount : null,
            localAccount: e.localEnabled ? e.localAccount : null,
            password: e.password || null,
            roleIds: e.ids,
          });
        else await this.api.roles(e.id, e.ids);
        this.update('password', '');
      }
      if (e.kind === 'role')
        await this.api.role(e.id, { name: e.name, enabled: e.enabled, groupIds: e.ids });
      if (e.kind === 'group')
        await this.api.group(e.id, {
          name: e.name,
          enabled: e.enabled,
          featureIds: e.ids,
          policy: {
            allowedModelIds: e.restricted ? e.modelIds : null,
            dailyRequestLimit: e.daily === '' ? null : Number(e.daily),
            storedAttachmentLimitBytes: parseStorageLimitGb(e.storage),
          },
        });
      if (e.kind === 'feature')
        await this.api.feature(e.id, {
          name: e.name,
          enabled: e.enabled,
          sortOrder: Number(e.order),
        });
      if (!this.alive) return;
      this.dialog().nativeElement.close();
      this.notice.set('已儲存。授權變更會在下一次操作生效。');
      this.catalog.set(await this.api.catalog());
      await this.loadUsers(this.users()?.offset || 0);
      await this.session.load(true);
    } catch (error) {
      if (this.alive) {
        if (this.dialog().nativeElement.open) this.editorError.set(this.message(error));
        else this.error.set('異動已完成，但列表重新載入失敗：' + this.message(error));
      }
    } finally {
      if (this.alive) this.saving.set(false);
    }
  }
  label(kind: string) {
    return (
      { user: '使用者', role: '角色', group: '功能群組', feature: '功能' } as Record<string, string>
    )[kind];
  }
  readonly date = formatDate;
  readonly bytes = formatBytes;
  readonly duration = formatDuration;
  readonly modelName = formatModelName;
  readonly format = formatNumber;
  actions(user: AdminUser): MenuAction[] {
    const testing = !!this.session.auth.session()?.testing;
    return [
      { id: 'edit', label: '編輯使用者與登入方式', icon: 'edit', disabled: testing },
      {
        id: 'test',
        label: '以此身分測試',
        icon: 'shield',
        disabled: testing || user.id === this.session.me()?.id || user.enabled === false,
      },
      {
        id: 'delete',
        label: '刪除使用者',
        icon: 'trash',
        danger: true,
        disabled: testing || user.id === this.session.me()?.id,
      },
    ];
  }
  async userAction(action: string, user: AdminUser) {
    if (action === 'edit') this.open('user', user);
    if (action === 'test') {
      this.testing.set(user);
      this.testReason.set('驗證角色權限與功能操作');
      this.testError.set('');
      this.testDialog().nativeElement.showModal();
    }
    if (action === 'delete') {
      if (
        !(await this.confirmation().ask({
          title: '刪除使用者：' + user.displayName,
          message:
            '此使用者將無法登入，既有工作階段會失效。對話、附件、用量與稽核仍會保留，登入帳號也會保留，避免他人接管。',
          confirm: '刪除使用者',
          danger: true,
        }))
      )
        return;
      try {
        await this.api.deleteUser(user.id);
        await this.loadUsers();
        this.notice.set('已刪除登入身分，歷史資料已保留。');
      } catch (error) {
        this.error.set(this.message(error));
      }
    }
  }
  async startTest(event: Event) {
    event.preventDefault();
    const user = this.testing();
    if (!user || this.saving()) return;
    this.saving.set(true);
    this.testError.set('');
    try {
      await this.session.auth.testIdentity(user.id, this.testReason());
    } catch (error) {
      this.testError.set(this.message(error));
      this.saving.set(false);
    }
  }
  private message(error: unknown) {
    return error instanceof Error ? error.message : '服務暫時無法使用，請重試。';
  }
}
