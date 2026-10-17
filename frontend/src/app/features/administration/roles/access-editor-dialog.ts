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
 * 使用者在使用者檢視器中編輯、在 `AdminUserCreateDialog` 中建立。`menu` 是導覽群組：只整理選單、
 * 不授予任何權限。
 */
export type AccessKind = 'role' | 'group' | 'feature' | 'menu';
export type AccessItem = AdminRoleDto | AdminGroupDto | AdminFeatureDto | NavigationGroupDto;
type GroupSection = 'settings' | 'models';
interface Editor {
  kind: AccessKind;
  id: string;
  name: string;
  /** 開啟時固定，讓標題顯示正在編輯的對象，而不是草稿名稱。 */
  title: string;
  subtitle: string;
  enabled: boolean;
  ids: string[];
  modelPolicy: ModelPolicyDraft;
  storage: string;
  /** 保留原狀：順序在功能清單上調整。 */
  order: number;
  /** 功能所屬的選單群組，或群組的上層；空字串代表最上層。 */
  placement: string;
  /** 功能的圖示名稱。 */
  icon: string;
  isNew: boolean;
}

/** 草稿是否與已儲存的內容相同。 */
function sameEditor(first: Editor, second: Editor) {
  return (
    first.id === second.id &&
    first.name === second.name &&
    first.enabled === second.enabled &&
    sameIds(first.ids, second.ids) &&
    sameModelPolicy(first.modelPolicy, second.modelPolicy) &&
    first.storage.trim() === second.storage.trim() &&
    first.placement === second.placement &&
    first.icon === second.icon
  );
}

let sequence = 0;

/**
 * 在同一個對話框中建立或編輯角色、功能群組或功能，版面與使用者檢視器相同：內容放在 `.form-sections`，
 * 編輯時底部為 `DialogSaveBar`。
 * 群組會在旁邊多一個分頁放模型政策；一次儲存涵蓋兩者。有未儲存的變更時，關閉會先詢問。
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
  /** 開啟時的草稿，用於還原與未儲存變更的檢查。 */
  private readonly opened = signal<Editor | null>(null);
  readonly dirty = computed(() => {
    const draft = this.editor(),
      opened = this.opened();
    return !!draft && !!opened && !sameEditor(draft, opened);
  });
  readonly saving = signal(false);
  /** 儲存中或測試其他身分時，欄位唯讀。 */
  readonly locked = computed(() => this.saving() || this.session.testing());
  readonly editorError = signal('');
  /** 功能群組的設定：它的區段頁面與模型政策。 */
  readonly groupSection = signal<GroupSection>('settings');
  /** 兩個分頁由同一次儲存送出，各自標記自己持有的變更。 */
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
  /** 群組授予功能，清單由選單的群組列出。 */
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
  /** 角色加入功能群組；群組沒有分類，所以整體顯示為一張卡片。 */
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
   * 功能或選單群組可以移到的位置：最上層或任一群組，以路徑顯示。群組不能移到自己或自己的子群組之下。
   */
  readonly placements = computed<SelectOption[]>(() => {
    const event2 = this.editor(),
      groups = this.store.navigationGroups();
    const excluded =
      event2?.kind === 'menu' && !event2.isNew
        ? descendantIds(groups, event2.id)
        : new Set<string>();
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
  /** 角色儲存後會授予的內容：已啟用群組中已啟用的功能。 */
  readonly proposedFeatures = computed(() => {
    const event3 = this.editor(),
      card = this.store.catalog();
    if (!event3 || !event3.enabled || event3.kind !== 'role' || !card) return [];
    const groups = new Set(event3.ids);
    const features = new Set(
      card.groups
        .filter((item) => item.enabled && groups.has(item.id))
        .flatMap((item) => item.featureIds),
    );
    return card.features.filter((item) => item.enabled && features.has(item.id));
  });
  /** 關閉後草稿仍保留，讓對話框在退場動畫中不會被清空。 */
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
      // 沒有頁面的功能（例如 `logs.export` 這類權限）沒有路由可顯示。
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
  /** 每個分頁與每次開啟，都從內容頂端開始。 */
  selectSection(section: GroupSection) {
    this.groupSection.set(section);
    this.body()?.nativeElement.scrollTo({ top: 0 });
  }
  update<K extends keyof Editor>(key: K, value: Editor[K]) {
    this.editor.update((old) => (old ? { ...old, [key]: value } : null));
  }
  /** 還原為開啟時的值；分頁停留在原位。 */
  revert() {
    this.editor.set(this.opened());
    this.editorError.set('');
  }
  /** Escape、關閉鈕、抽屜的滑動關閉都會走到這裡。 */
  cancel(event: Event) {
    event.preventDefault();
    void this.requestClose();
  }
  async requestClose() {
    if (this.saving()) return;
    const event4 = this.editor();
    if (
      event4 &&
      this.dirty() &&
      !(await this.confirmation().ask({
        title: '放棄未儲存的變更？',
        message: event4.isNew
          ? `尚未建立${this.label(event4.kind)}，關閉後會捨棄已填寫的內容。`
          : `「${event4.title}」的變更尚未儲存，關閉後會恢復為已儲存的設定。`,
        confirm: '放棄變更',
      }))
    )
      return;
    this.dialog().nativeElement.close();
  }
  async save(event: Event) {
    event.preventDefault();
    const event5 = this.editor();
    if (!event5 || this.locked() || (!event5.isNew && !this.dirty())) return;
    // 錯誤會打開含有該欄位的分頁並聚焦（或顯示訊息），
    // 即使欄位位於頁面較下方，修正處也能被看到。
    const fail = (section: GroupSection, message: string, field?: string) => {
      if (event5.kind === 'group') this.selectSection(section);
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
    if (!event5.id || !event5.name.trim())
      return fail('settings', '請輸入識別碼與名稱。', event5.id ? 'name' : 'id');
    let policy: GroupPolicyRequest | undefined;
    if (event5.kind === 'group') {
      try {
        policy = modelPolicyRequest(event5.modelPolicy);
      } catch (error) {
        return fail('models', safeMessage(error));
      }
      try {
        policy.storedAttachmentLimitBytes = parseStorageLimitGb(event5.storage);
      } catch (error) {
        return fail('settings', safeMessage(error), 'storage');
      }
    }
    this.saving.set(true);
    this.editorError.set('');
    try {
      if (event5.kind === 'role')
        await this.api.role(event5.id, {
          name: event5.name,
          enabled: event5.enabled,
          groupIds: event5.ids,
        });
      if (event5.kind === 'group' && policy)
        await this.api.group(event5.id, {
          name: event5.name,
          enabled: event5.enabled,
          featureIds: event5.ids,
          policy,
        });
      if (event5.kind === 'feature')
        await this.api.feature(event5.id, {
          name: event5.name,
          enabled: event5.enabled,
          // 移到其他群組的功能，放到該群組的最後。
          sortOrder: this.order(event5, () =>
            (this.store.catalog()?.features ?? []).filter(
              (item) => (item.groupId ?? '') === event5.placement && item.id !== event5.id,
            ),
          ),
          groupId: event5.placement || null,
          icon: event5.icon,
        });
      if (event5.kind === 'menu') {
        const body = {
          name: event5.name,
          parentId: event5.placement || null,
          // 新的群組，或移到另一個上層之下的群組，放在新的同層兄弟之後。
          sortOrder: this.order(event5, () =>
            this.store
              .navigationGroups()
              .filter(
                (item) => (item.parentId ?? '') === event5.placement && item.id !== event5.id,
              ),
          ),
        };
        if (event5.isNew) await this.api.createNavigationGroup({ id: event5.id, ...body });
        else await this.api.updateNavigationGroup(event5.id, body);
      }
      this.opened.set(event5);
      this.dialog().nativeElement.close();
      if (event5.kind === 'menu') {
        this.store.toast.success(event5.isNew ? '已建立選單分群' : '已儲存', '側欄選單已更新。');
        this.store.reordered();
      } else
        this.store.saved(
          event5.isNew ? '已建立' + this.label(event5.kind) : '已儲存',
          '授權變更會在下一次操作生效。',
        );
    } catch (error) {
      this.editorError.set(safeMessage(error));
    } finally {
      this.saving.set(false);
    }
  }
  /** 已儲存的位置；若位置有變，則是新上層清單的末尾。 */
  private order(event6: Editor, siblings: () => readonly { sortOrder: number }[]) {
    return !event6.isNew && event6.placement === this.opened()?.placement
      ? event6.order
      : nextSortOrder(siblings());
  }
  label(kind: AccessKind) {
    return ({ role: '角色', group: '功能群組', feature: '功能', menu: '選單分群' } as const)[kind];
  }
}
