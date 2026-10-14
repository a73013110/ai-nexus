import { NgTemplateOutlet } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  Injector,
  afterNextRender,
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
import { ConfirmDialog } from '../../../shared/ui/confirm-dialog';
import { DialogHeader } from '../../../shared/ui/dialog-header';
import { DialogSaveBar } from '../../../shared/ui/dialog-save-bar';
import { Field } from '../../../shared/ui/field';
import { InfoPopover } from '../../../shared/ui/info-popover';
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
  sameModelPolicy,
  type ModelPolicyDraft,
} from '../model-policy-editor';
import { sameIds } from '../users/user-role-fields';

/** Users are edited in the user inspector and created in `AdminUserCreateDialog`. */
export type AccessKind = 'role' | 'group' | 'feature';
export type AccessItem = AdminRoleDto | AdminGroupDto | AdminFeatureDto;
type GroupSection = 'settings' | 'models';
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

/** Whether a draft would save what is already saved. */
function sameEditor(a: Editor, b: Editor) {
  return (
    a.id === b.id &&
    a.name === b.name &&
    a.enabled === b.enabled &&
    sameIds(a.ids, b.ids) &&
    sameModelPolicy(a.modelPolicy, b.modelPolicy) &&
    a.storage.trim() === b.storage.trim()
  );
}

let sequence = 0;

/**
 * Creates or edits a role, feature group or feature in one dialog, laid out like the user
 * inspector: `.form-sections` in the body and, when editing, `DialogSaveBar` at the foot. A group
 * adds a tab for its model policy beside that page; one save covers both. Closing with unsaved
 * changes asks first.
 */
@Component({
  selector: 'nx-access-editor-dialog',
  imports: [
    Checkbox,
    ChoiceGroups,
    CompactDialog,
    ConfirmDialog,
    DialogHeader,
    DialogSaveBar,
    Field,
    FeatureSummary,
    InfoPopover,
    ModelPolicyEditor,
    NgTemplateOutlet,
    Notice,
    Tabs,
    ViewMotion,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './access-editor-dialog.html',
})
export class AccessEditorDialog {
  readonly id = `nx-access-editor-${++sequence}`;
  readonly store = inject(AdminStore);
  private readonly api = inject(AdminApi);
  readonly editor = signal<Editor | null>(null);
  /** The draft as opened, for revert and the unsaved-changes check. */
  private readonly opened = signal<Editor | null>(null);
  readonly dirty = computed(() => {
    const draft = this.editor(),
      opened = this.opened();
    return !!draft && !!opened && !sameEditor(draft, opened);
  });
  readonly saving = signal(false);
  /** Fields are read-only while saving or while testing another identity. */
  readonly locked = computed(() => this.saving() || this.store.testing());
  readonly testingNote = '測試身分期間不能修改管理設定';
  readonly editorError = signal('');
  /** A feature group's settings: its page of sections, and its model policy. */
  readonly groupSection = signal<GroupSection>('settings');
  readonly groupSections: readonly (TabItem & { value: GroupSection })[] = [
    { value: 'settings', label: '群組設定' },
    { value: 'models', label: 'AI 模型' },
  ];
  readonly enabledNote: Record<AccessKind, string> = {
    role: '停用後，擁有此角色的使用者不再取得它的功能群組。',
    group: '停用後不授予功能、模型或附件容量。',
    feature: '停用後任何群組都不會授予此功能。',
  };
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('editorDialog');
  private readonly body = viewChild<ElementRef<HTMLElement>>('body');
  private readonly confirmation = viewChild.required(ConfirmDialog);
  private readonly injector = inject(Injector);
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
      name: '全部功能群組',
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
  /** The draft stays after closing, so the dialog does not empty during its exit. */
  open(kind: AccessKind, item?: AccessItem) {
    this.editorError.set('');
    const role = kind === 'role' ? (item as AdminRoleDto) : null,
      group = kind === 'group' ? (item as AdminGroupDto) : null,
      feature = kind === 'feature' ? (item as AdminFeatureDto) : null;
    const name = role?.name || group?.name || feature?.name || '';
    const draft: Editor = {
      kind,
      id: item?.id || '',
      name,
      title: (item ? '編輯' : '新增') + this.label(kind) + (item ? '：' + name : ''),
      // A feature without a page (a permission such as `logs.export`) has no route to show.
      subtitle: feature ? [feature.id, feature.route].filter(Boolean).join(' · ') : item?.id || '',
      enabled: role?.enabled ?? group?.enabled ?? feature?.enabled ?? true,
      ids: [...(role?.groupIds || group?.featureIds || [])],
      modelPolicy: modelPolicyDraft(group?.policy),
      storage:
        group?.policy?.storedAttachmentLimitBytes == null
          ? ''
          : storageLimitGb(group.policy.storedAttachmentLimitBytes),
      order: feature?.sortOrder ?? 0,
      isNew: !item,
    };
    this.editor.set(draft);
    this.opened.set(draft);
    this.selectSection('settings');
    this.dialog().nativeElement.showModal();
  }
  /** Each tab, and each opening, starts at the top of the body. */
  selectSection(section: GroupSection) {
    this.groupSection.set(section);
    this.body()?.nativeElement.scrollTo({ top: 0 });
  }
  update<K extends keyof Editor>(key: K, value: Editor[K]) {
    this.editor.update((old) => (old ? { ...old, [key]: value } : null));
  }
  /** Back to the values as opened; the tab stays where it is. */
  revert() {
    this.editor.set(this.opened());
    this.editorError.set('');
  }
  /** Escape, the close button, the sheet's swipe and 「取消」 arrive here. */
  cancel(event: Event) {
    event.preventDefault();
    void this.requestClose();
  }
  async requestClose() {
    if (this.saving()) return;
    const e = this.editor();
    if (
      e &&
      this.dirty() &&
      !(await this.confirmation().ask({
        title: '放棄未儲存的變更？',
        message: e.isNew
          ? `尚未建立${this.label(e.kind)}，關閉後會捨棄已填寫的內容。`
          : `「${e.title}」的變更尚未儲存，關閉後會恢復為已儲存的設定。`,
        confirm: '放棄變更',
      }))
    )
      return;
    this.dialog().nativeElement.close();
  }
  async save(event: Event) {
    event.preventDefault();
    const e = this.editor();
    if (!e || this.locked() || (!e.isNew && !this.dirty())) return;
    // An error opens the tab that holds the field and focuses it (or shows the message), so the
    // fix is in view even when the field sits further down the page.
    const fail = (section: GroupSection, message: string, field?: string) => {
      if (e.kind === 'group') this.selectSection(section);
      this.editorError.set(message);
      afterNextRender(
        () => {
          const body = this.body()?.nativeElement;
          const target = field && body?.querySelector<HTMLElement>(`[data-field="${field}"]`);
          if (target) target.focus();
          else body?.querySelector('nx-notice')?.scrollIntoView({ block: 'nearest' });
        },
        { injector: this.injector },
      );
    };
    if (!e.id || !e.name.trim())
      return fail('settings', '請輸入識別碼與名稱。', e.id ? 'name' : 'id');
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
        return fail('settings', safeMessage(error), 'storage');
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
      this.opened.set(e);
      this.dialog().nativeElement.close();
      this.store.saved(
        e.isNew ? '已建立' + this.label(e.kind) : '已儲存',
        '授權變更會在下一次操作生效。',
      );
    } catch (error) {
      this.editorError.set(safeMessage(error));
    } finally {
      this.saving.set(false);
    }
  }
  label(kind: AccessKind) {
    return ({ role: '角色', group: '功能群組', feature: '功能' } as const)[kind];
  }
}
