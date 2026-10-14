import { NgTemplateOutlet } from '@angular/common';
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
  GroupPolicyRequest,
} from '../../../core/api/schema';
import { safeMessage } from '../../../core/errors/safe-errors';
import { groupFeatures } from '../../../core/layout/feature-groups';
import { parseStorageLimitGb, storageLimitGb } from '../../../shared/browser/storage-limit';
import { Checkbox } from '../../../shared/ui/checkbox';
import { ChoiceGroups, type ChoiceGroup } from '../../../shared/ui/choice-groups';
import { CompactDialog } from '../../../shared/ui/compact-dialog';
import { Field } from '../../../shared/ui/field';
import { DialogHeader } from '../../../shared/ui/dialog-header';
import { Notice } from '../../../shared/ui/notice';
import { Tabs, type TabItem } from '../../../shared/ui/tabs';
import { ViewMotion } from '../../../shared/ui/view-motion';
import { AdminApi } from '../admin-api';
import { AdminStore } from '../admin-store';
import { FeatureSummary } from '../feature-summary';
import {
  ModelPolicyEditor,
  modelPolicyDraft,
  modelPolicyRequest,
  type ModelPolicyDraft,
} from '../model-policy-editor';

/** Users are edited in the user inspector and created in `AdminUserCreateDialog`. */
export type AccessKind = 'role' | 'group' | 'feature';
export type AccessItem = AdminRoleDto | AdminGroupDto | AdminFeatureDto;
type GroupSection = 'general' | 'features' | 'models' | 'storage';
interface Editor {
  kind: AccessKind;
  id: string;
  name: string;
  /** Fixed at open so the heading names what is being edited, not the draft name. */
  title: string;
  subtitle: string;
  enabled: boolean;
  ids: string[];
  modelPolicy: ModelPolicyDraft;
  storage: string;
  /** Kept as it is: the order is changed on the features list. */
  order: number;
  isNew: boolean;
}

/** Creates or edits a role, feature group or feature in one dialog. */
@Component({
  selector: 'nx-access-editor-dialog',
  imports: [
    Checkbox,
    ChoiceGroups,
    CompactDialog,
    DialogHeader,
    Field,
    FeatureSummary,
    ModelPolicyEditor,
    NgTemplateOutlet,
    Notice,
    Tabs,
    ViewMotion,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrls: ['../form-sections.scss', './access-editor-dialog.scss'],
  templateUrl: './access-editor-dialog.html',
})
export class AccessEditorDialog {
  readonly store = inject(AdminStore);
  private readonly api = inject(AdminApi);
  readonly editor = signal<Editor | null>(null);
  readonly saving = signal(false);
  readonly editorError = signal('');
  /** A feature group's settings are split into these tabs; one save covers all of them. */
  readonly groupSection = signal<GroupSection>('general');
  readonly groupSections: readonly TabItem[] = [
    { value: 'general', label: '基本資料' },
    { value: 'features', label: '功能授權' },
    { value: 'models', label: 'AI 模型' },
    { value: 'storage', label: '附件容量' },
  ];
  readonly enabledNote: Record<AccessKind, string> = {
    role: '停用後，擁有此角色的使用者不再取得它的功能群組。',
    group: '停用後此群組不授予功能、模型或附件容量。',
    feature: '停用後任何群組都不會授予此功能。',
  };
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('editorDialog');
  /** A group grants features, listed by the sidebar's categories. */
  readonly featureChoices = computed<ChoiceGroup[]>(() =>
    groupFeatures(this.store.catalog()?.features || []).map((section) => ({
      id: section.id,
      name: section.name,
      options: section.features.map((feature) => ({
        id: feature.id,
        label: feature.name + (feature.enabled ? '' : '（停用）'),
        description: feature.id,
      })),
    })),
  );
  /** A role joins feature groups; they have no categories, so they form one card. */
  readonly groupChoices = computed<ChoiceGroup[]>(() => [
    {
      id: 'groups',
      name: '功能群組',
      options: (this.store.catalog()?.groups || []).map((group) => ({
        id: group.id,
        label: group.name + (group.enabled ? '' : '（停用）'),
        description: group.id,
      })),
    },
  ]);
  /** What a role would grant once saved: enabled features of its enabled groups. */
  readonly proposedFeatures = computed(() => {
    const e = this.editor(),
      c = this.store.catalog();
    if (!e || !e.enabled || e.kind !== 'role' || !c) return [];
    const groups = new Set(e.ids);
    const features = new Set(
      c.groups.filter((x) => x.enabled && groups.has(x.id)).flatMap((x) => x.featureIds),
    );
    return c.features.filter((x) => x.enabled && features.has(x.id));
  });
  open(kind: AccessKind, item?: AccessItem) {
    this.editorError.set('');
    const role = kind === 'role' ? (item as AdminRoleDto) : null,
      group = kind === 'group' ? (item as AdminGroupDto) : null,
      feature = kind === 'feature' ? (item as AdminFeatureDto) : null;
    const name = role?.name || group?.name || feature?.name || '';
    this.editor.set({
      kind,
      id: item?.id || '',
      name,
      title: (item ? '編輯' : '新增') + this.label(kind) + (item ? '：' + name : ''),
      subtitle: feature ? feature.id + ' · ' + feature.route : item?.id || '',
      enabled: role?.enabled ?? group?.enabled ?? feature?.enabled ?? true,
      ids: role?.groupIds || group?.featureIds || [],
      modelPolicy: modelPolicyDraft(group?.policy),
      storage:
        group?.policy?.storedAttachmentLimitBytes == null
          ? ''
          : storageLimitGb(group.policy.storedAttachmentLimitBytes),
      order: feature?.sortOrder ?? 0,
      isNew: !item,
    });
    this.groupSection.set('general');
    this.dialog().nativeElement.showModal();
  }
  update<K extends keyof Editor>(key: K, value: Editor[K]) {
    this.editor.update((old) => (old ? { ...old, [key]: value } : null));
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
    // An error shows on the tab that holds the field, so the fix is in view.
    const fail = (section: GroupSection, message: string) => {
      if (e.kind === 'group') this.groupSection.set(section);
      this.editorError.set(message);
    };
    if (!e.id || !e.name.trim()) return fail('general', '請輸入識別碼與名稱。');
    let policy: GroupPolicyRequest | undefined;
    if (e.kind === 'group') {
      try {
        policy = modelPolicyRequest(e.modelPolicy);
      } catch (error) {
        return fail('models', safeMessage(error));
      }
      try {
        policy.storedAttachmentLimitBytes = parseStorageLimitGb(e.storage);
      } catch (error) {
        return fail('storage', safeMessage(error));
      }
    }
    this.saving.set(true);
    this.editorError.set('');
    try {
      if (e.kind === 'role')
        await this.api.role(e.id, { name: e.name, enabled: e.enabled, groupIds: e.ids });
      if (e.kind === 'group' && policy)
        await this.api.group(e.id, {
          name: e.name,
          enabled: e.enabled,
          featureIds: e.ids,
          policy,
        });
      if (e.kind === 'feature')
        await this.api.feature(e.id, {
          name: e.name,
          enabled: e.enabled,
          sortOrder: e.order,
        });
      this.dialog().nativeElement.close();
      this.store.saved('已儲存', '授權變更會在下一次操作生效。');
    } catch (error) {
      this.editorError.set(safeMessage(error));
    } finally {
      this.saving.set(false);
    }
  }
  label(kind: string) {
    return ({ role: '角色', group: '功能群組', feature: '功能' } as Record<string, string>)[kind];
  }
}
