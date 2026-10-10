import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import type {
  AdminFeatureDto,
  AdminGroupDto,
  AdminRoleDto,
  AdminUserDto,
} from '../../../core/api/schema';
import { safeMessage } from '../../../core/errors/safe-errors';
import { groupFeatures } from '../../../core/layout/feature-groups';
import { parseStorageLimitGb, storageLimitGb } from '../../../shared/browser/storage-limit';
import { Checkbox } from '../../../shared/ui/checkbox';
import { CompactDialog } from '../../../shared/ui/compact-dialog';
import { Field } from '../../../shared/ui/field';
import { Icon } from '../../../shared/ui/icon';
import { Notice } from '../../../shared/ui/notice';
import { DialogMotion, ViewMotion } from '../../../shared/ui/view-motion';
import { ViewSwitch } from '../../../shared/ui/view-switch';
import { AdminApi } from '../admin-api';
import { AdminStore } from '../admin-store';
import { FeatureSummary } from '../feature-summary';
import {
  ModelPolicyEditor,
  modelPolicyDraft,
  modelPolicyRequest,
  type ModelPolicyDraft,
} from '../model-policy-editor';

export type AccessKind = 'user' | 'role' | 'group' | 'feature';
export type AccessItem = AdminUserDto | AdminRoleDto | AdminGroupDto | AdminFeatureDto;
interface Editor {
  kind: AccessKind;
  id: string;
  name: string;
  subtitle: string;
  enabled: boolean;
  ids: string[];
  modelPolicy: ModelPolicyDraft;
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
const profileKeys = [
  'name',
  'enabled',
  'adEnabled',
  'localEnabled',
  'adAccount',
  'localAccount',
  'password',
];

/** Creates or edits a user, role, feature group or feature in one dialog. */
@Component({
  selector: 'nx-access-editor-dialog',
  imports: [
    Checkbox,
    CompactDialog,
    DialogMotion,
    Field,
    FeatureSummary,
    Icon,
    ModelPolicyEditor,
    Notice,
    ViewMotion,
    ViewSwitch,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './access-editor-dialog.scss',
  templateUrl: './access-editor-dialog.html',
})
export class AccessEditorDialog {
  readonly store = inject(AdminStore);
  private readonly api = inject(AdminApi);
  readonly editor = signal<Editor | null>(null);
  readonly saving = signal(false);
  readonly editorError = signal('');
  readonly groupSection = signal('general');
  readonly groupViewOptions = [
    { value: 'general', label: '基本資料' },
    { value: 'features', label: '功能授權' },
    { value: 'models', label: 'AI 模型' },
    { value: 'storage', label: '附件容量' },
  ];
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('editorDialog');
  readonly choices = computed(() => {
    const e = this.editor(),
      c = this.store.catalog();
    if (!e || !c) return [];
    return e.kind === 'user' ? c.roles : e.kind === 'role' ? c.groups : c.features;
  });
  readonly groupedFeatures = computed(() => groupFeatures(this.store.catalog()?.features || []));
  readonly proposedFeatures = computed(() => {
    const e = this.editor(),
      c = this.store.catalog();
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
  open(kind: AccessKind, item?: AccessItem) {
    this.editorError.set('');
    const user = kind === 'user' ? (item as AdminUserDto) : null,
      role = kind === 'role' ? (item as AdminRoleDto) : null,
      group = kind === 'group' ? (item as AdminGroupDto) : null,
      feature = kind === 'feature' ? (item as AdminFeatureDto) : null;
    this.editor.set({
      kind,
      id: item?.id || '',
      name: user?.displayName || role?.name || group?.name || feature?.name || '',
      subtitle: user?.account || feature?.route || '',
      enabled: user?.enabled ?? role?.enabled ?? group?.enabled ?? feature?.enabled ?? true,
      ids:
        user?.roleIds || role?.groupIds || group?.featureIds || (kind === 'user' ? ['member'] : []),
      modelPolicy: modelPolicyDraft(group?.policy),
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
    this.groupSection.set('general');
    this.dialog().nativeElement.showModal();
  }
  update<K extends keyof Editor>(key: K, value: Editor[K]) {
    this.editor.update((old) =>
      old
        ? {
            ...old,
            [key]: value,
            profileChanged:
              old.profileChanged || (old.kind === 'user' && profileKeys.includes(key)),
          }
        : null,
    );
  }
  check(id: string, checked: boolean) {
    const e = this.editor();
    if (e) this.update('ids', checked ? [...e.ids, id] : e.ids.filter((x) => x !== id));
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
        if (e.profileChanged) {
          const account = {
            displayName: e.name,
            enabled: e.enabled,
            adEnabled: e.adEnabled,
            localEnabled: e.localEnabled,
            adAccount: e.adEnabled ? e.adAccount : null,
            localAccount: e.localEnabled ? e.localAccount : null,
            password: e.password || null,
            roleIds: e.ids,
          };
          await (e.isNew ? this.api.createUser(account) : this.api.updateUser(e.id, account));
        } else await this.api.roles(e.id, e.ids);
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
            ...modelPolicyRequest(e.modelPolicy),
            storedAttachmentLimitBytes: parseStorageLimitGb(e.storage),
          },
        });
      if (e.kind === 'feature')
        await this.api.feature(e.id, {
          name: e.name,
          enabled: e.enabled,
          sortOrder: Number(e.order),
        });
      this.dialog().nativeElement.close();
      this.store.saved('已儲存。授權變更會在下一次操作生效。');
    } catch (error) {
      this.editorError.set(safeMessage(error));
    } finally {
      this.saving.set(false);
    }
  }
  label(kind: string) {
    return (
      { user: '使用者', role: '角色', group: '功能群組', feature: '功能' } as Record<string, string>
    )[kind];
  }
}
