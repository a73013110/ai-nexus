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
  NavigationGroupDto,
} from '@core/api/schema';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { safeMessage } from '@core/errors/safe-errors';
import {
  descendantIds,
  featureIcon,
  navigationSections,
  navigationTree,
} from '@core/layout/navigation-tree';
import { parseStorageLimitGb, storageLimitGb } from '@shared/browser/storage-limit';
import { Checkbox } from '@shared/ui/checkbox';
import { ChoiceGroups, type ChoiceGroup } from '@shared/ui/choice-groups';
import { CompactDialog } from '@shared/ui/compact-dialog';
import { ConfirmDialog } from '@shared/ui/confirm-dialog';
import { DialogHeader } from '@shared/ui/dialog-header';
import { DialogSaveBar } from '@shared/ui/dialog-save-bar';
import { Field } from '@shared/ui/field';
import { ICON_NAMES, Icon } from '@shared/ui/icon';
import { InfoPopover } from '@shared/ui/info-popover';
import { Notice } from '@shared/ui/notice';
import { Select, type SelectOption } from '@shared/ui/select';
import { Tabs, type TabItem } from '@shared/ui/tabs';
import { ViewMotion } from '@shared/ui/view-motion';
import { AdministrationApi } from '../administration-api';
import { AdministrationStore } from '../administration-store';
import { FeatureSummary } from './feature-summary';
import { nextSortOrder } from './feature-order';
import {
  ModelPolicyEditor,
  modelPolicyDraft,
  modelPolicyRequest,
  sameModelPolicy,
  type ModelPolicyDraft,
} from '../model-policies/model-policy-editor';
import { sameIds } from '../users/user-role-fields';

/**
 * Users are edited in the user inspector and created in `AdminUserCreateDialog`. `menu` is a
 * navigation group: it only arranges the menu and grants nothing.
 */
export type AccessKind = 'role' | 'group' | 'feature' | 'menu';
export type AccessItem = AdminRoleDto | AdminGroupDto | AdminFeatureDto | NavigationGroupDto;
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
  /** A feature's menu group, or a menu group's parent; '' is the top level. */
  placement: string;
  /** A feature's icon name. */
  icon: string;
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
    a.storage.trim() === b.storage.trim() &&
    a.placement === b.placement &&
    a.icon === b.icon
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
    Icon,
    InfoPopover,
    ModelPolicyEditor,
    NgTemplateOutlet,
    Notice,
    Select,
    Tabs,
    ViewMotion,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './access-editor-dialog.html',
  styles: `
    .feature-icon-field {
      display: flex;
      align-items: center;
      gap: var(--p-space-2);
    }
    .feature-icon-field nx-select {
      flex: 1;
      min-width: 0;
    }
  `,
})
export class AccessEditorDialog {
  readonly id = `nx-access-editor-${++sequence}`;
  readonly store = inject(AdministrationStore);
  readonly session = inject(WorkspaceSession);
  private readonly api = inject(AdministrationApi);
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
  readonly locked = computed(() => this.saving() || this.session.testing());
  readonly editorError = signal('');
  /** A feature group's settings: its page of sections, and its model policy. */
  readonly groupSection = signal<GroupSection>('settings');
  /** One save covers both tabs, so each marks the changes it holds. */
  readonly groupSections = computed<readonly (TabItem & { value: GroupSection })[]>(() => {
    const draft = this.editor(),
      opened = this.opened();
    const models = !!draft && !!opened && !sameModelPolicy(draft.modelPolicy, opened.modelPolicy);
    const settings =
      !!draft && !!opened && !sameEditor({ ...draft, modelPolicy: opened.modelPolicy }, opened);
    return [
      { value: 'settings', label: '群組設定', dirty: settings },
      { value: 'models', label: 'AI 模型', dirty: models },
    ];
  });
  readonly enabledNote: Record<AccessKind, string> = {
    role: '停用後，擁有此角色的使用者不再取得它的功能群組。',
    group: '停用後不授予功能、模型或附件容量。',
    feature: '停用後任何群組都不會授予此功能。',
    menu: '',
  };
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('editorDialog');
  private readonly body = viewChild<ElementRef<HTMLElement>>('body');
  private readonly confirmation = viewChild.required(ConfirmDialog);
  private readonly injector = inject(Injector);
  /** A group grants features, listed by the menu's groups. */
  readonly featureChoices = computed<ChoiceGroup[]>(() =>
    navigationSections(
      navigationTree(this.store.catalog()?.features || [], this.store.navigationGroups()),
      { topName: '最上層' },
    ).map((section) => ({
      id: section.id || 'top',
      name: section.path,
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
  /**
   * Where a feature or menu group can go: the top level or any group, shown by its path. A group
   * cannot move under itself or its own subgroups.
   */
  readonly placements = computed<SelectOption[]>(() => {
    const e = this.editor(),
      groups = this.store.navigationGroups();
    const excluded =
      e?.kind === 'menu' && !e.isNew ? descendantIds(groups, e.id) : new Set<string>();
    return [
      { value: '', label: '最上層（不分群）' },
      ...navigationSections(navigationTree([], groups, { keepEmpty: true }))
        .filter((section) => !excluded.has(section.id))
        .map((section) => ({ value: section.id, label: section.path })),
    ];
  });
  readonly icons: SelectOption[] = [...ICON_NAMES].sort().map((name) => ({
    value: name,
    label: name,
  }));
  readonly icon = featureIcon;
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
      feature = kind === 'feature' ? (item as AdminFeatureDto) : null,
      menu = kind === 'menu' ? (item as NavigationGroupDto) : null;
    const name = role?.name || group?.name || feature?.name || menu?.name || '';
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
      order: feature?.sortOrder ?? menu?.sortOrder ?? 0,
      placement: feature?.groupId ?? menu?.parentId ?? '',
      icon: feature?.icon ?? '',
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
          // A feature moved to another group goes to that group's end.
          sortOrder: this.order(e, () =>
            (this.store.catalog()?.features ?? []).filter(
              (x) => (x.groupId ?? '') === e.placement && x.id !== e.id,
            ),
          ),
          groupId: e.placement || null,
          icon: e.icon,
        });
      if (e.kind === 'menu') {
        const body = {
          name: e.name,
          parentId: e.placement || null,
          // A new group, or one moved under another parent, goes after its new siblings.
          sortOrder: this.order(e, () =>
            this.store
              .navigationGroups()
              .filter((x) => (x.parentId ?? '') === e.placement && x.id !== e.id),
          ),
        };
        if (e.isNew) await this.api.createNavigationGroup({ id: e.id, ...body });
        else await this.api.updateNavigationGroup(e.id, body);
      }
      this.opened.set(e);
      this.dialog().nativeElement.close();
      if (e.kind === 'menu') {
        this.store.toast.success(e.isNew ? '已建立選單分群' : '已儲存', '側欄選單已更新。');
        this.store.reordered();
      } else
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
  /** The saved place, or the end of the new parent's list when the placement changed. */
  private order(e: Editor, siblings: () => readonly { sortOrder: number }[]) {
    return !e.isNew && e.placement === this.opened()?.placement
      ? e.order
      : nextSortOrder(siblings());
  }
  label(kind: AccessKind) {
    return ({ role: '角色', group: '功能群組', feature: '功能', menu: '選單分群' } as const)[kind];
  }
}
