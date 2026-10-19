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
} from '@core/api/schema';
import { catalogFeature, featureSections } from '@core/api/catalog';
import { CATALOG_SCOPES } from '@core/api/feature-catalog';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { safeMessage } from '@core/errors/safe-errors';
import { featureIcon } from '@core/layout/navigation-tree';
import { parseStorageLimitGb, storageLimitGb } from '@shared/browser/storage-limit';
import { uniqueId } from '@shared/browser/unique-id';
import { Checkbox } from '@shared/ui/checkbox';
import { ChoiceGroups, type ChoiceGroup } from '@shared/ui/choice-groups';
import { CompactDialog } from '@shared/ui/compact-dialog';
import { ConfirmDialog } from '@shared/ui/confirm-dialog';
import { DialogHeader } from '@shared/ui/dialog-header';
import { DialogSaveBar } from '@shared/ui/dialog-save-bar';
import { Field } from '@shared/ui/field';
import { Icon } from '@shared/ui/icon';
import { InfoPopover } from '@shared/ui/info-popover';
import { Notice } from '@shared/ui/notice';
import { Select, type SelectOption } from '@shared/ui/select';
import { Tabs, type TabItem } from '@shared/ui/tabs';
import { ViewMotion } from '@shared/ui/view-motion';
import { AccessControlApi } from '../access-control-api';
import { AccessControlStore, accessChoice } from '../access-control-store';
import { FeatureSummary } from './feature-summary';
import { sameIds } from './same-ids';
import {
  ModelPolicyEditor,
  modelPolicyDraft,
  modelPolicyRequest,
  sameModelPolicy,
  type ModelPolicyDraft,
} from '@features/platform/inference/model-policies/model-policy-editor';

/** 使用者在使用者檢視器中編輯、在 `AdminUserCreateDialog` 中建立；側欄選單在選單頁編輯。 */
export type AccessKind = 'role' | 'group' | 'feature';
export type AccessItem = AdminRoleDto | AdminGroupDto | AdminFeatureDto;
type GroupSection = 'settings' | 'models';
const LABELS: Record<AccessKind, string> = { role: '角色', group: '功能群組', feature: '功能' };
interface Editor {
  kind: AccessKind;
  id: string;
  name: string;
  /** 開啟時固定，讓標題顯示正在編輯的對象，而不是草稿名稱。 */
  title: string;
  subtitle: string;
  enabled: boolean;
  ids: string[];
  /** 群組授權的資料範圍（功能 ID → 範圍）；未選過的功能取它支援的最窄範圍，見 `grantScopes`。 */
  scopes: Record<string, string>;
  modelPolicy: ModelPolicyDraft;
  storage: string;
  /** 功能的目錄預設名稱；名稱留白或與它相同就是不覆寫。 */
  defaultName: string;
  isNew: boolean;
}

/** 功能在目錄宣告支援的資料範圍，由窄到寬；空的表示授權不帶範圍（ADR 0017）。 */
function supportedScopes(id: string): readonly string[] {
  return catalogFeature(id)?.scopes ?? [];
}

/**
 * 送給伺服器的範圍：只含已勾選、且目錄有宣告範圍的功能，每個都有一個它支援的值。
 * 沒選過（或舊資料的值已不支援）時取最窄的範圍，授予時預設最小權限。
 */
export function grantScopes(ids: readonly string[], scopes: Record<string, string>) {
  const result: Record<string, string> = {};
  for (const id of ids) {
    const supported = supportedScopes(id);
    if (supported.length) result[id] = supported.includes(scopes[id]) ? scopes[id] : supported[0];
  }
  return result;
}

function sameScopes(first: Editor, second: Editor) {
  const a = grantScopes(first.ids, first.scopes),
    b = grantScopes(second.ids, second.scopes);
  return Object.keys(a).every((id) => a[id] === b[id]);
}

/** 草稿是否與已儲存的內容相同。 */
function sameEditor(first: Editor, second: Editor) {
  return (
    first.id === second.id &&
    first.name === second.name &&
    first.enabled === second.enabled &&
    sameIds(first.ids, second.ids) &&
    sameScopes(first, second) &&
    sameModelPolicy(first.modelPolicy, second.modelPolicy) &&
    first.storage.trim() === second.storage.trim()
  );
}

/** 送給伺服器的名稱覆寫：留白或等於目錄預設名稱時為 `null`，之後目錄改名也會跟著變。 */
export function featureNameOverride(name: string, defaultName: string) {
  const trimmed = name.trim();
  return trimmed && trimmed !== defaultName ? trimmed : null;
}

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
    .feature-identity {
      display: flex;
      align-items: center;
      gap: var(--p-space-2);
      color: var(--secondary);
    }
  `,
})
export class AccessEditorDialog {
  readonly id = uniqueId('nx-access-editor');
  readonly store = inject(AccessControlStore);
  readonly session = inject(WorkspaceSession);
  private readonly api = inject(AccessControlApi);
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
  };
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('editorDialog');
  private readonly body = viewChild<ElementRef<HTMLElement>>('body');
  private readonly confirmation = viewChild.required(ConfirmDialog);
  private readonly injector = inject(Injector);
  /** 群組授予功能，依側欄的位置分組（`featureSections`），與使用者在側欄看到的一致。 */
  readonly featureChoices = computed<ChoiceGroup[]>(() =>
    featureSections(
      this.store.catalog()?.features ?? [],
      this.store.catalog()?.navigationSections ?? [],
    ).map((section) => ({
      id: section.id,
      name: section.name,
      options: section.features.map(accessChoice),
    })),
  );
  /** 角色加入功能群組；群組沒有分類，所以整體顯示為一張卡片。 */
  readonly groupChoices = computed<ChoiceGroup[]>(() => [
    {
      id: 'groups',
      name: '全部功能群組',
      options: (this.store.catalog()?.groups || []).map(accessChoice),
    },
  ]);
  /** 已勾選且有資料範圍的功能，各自的範圍選項只列目錄宣告支援的。 */
  readonly scopedGrants = computed(() => {
    const draft = this.editor();
    if (!draft || draft.kind !== 'group') return [];
    const scopes = grantScopes(draft.ids, draft.scopes);
    const names = new Map(
      (this.store.catalog()?.features || []).map((item) => [item.id, item.name]),
    );
    return Object.keys(scopes).map((id) => ({
      id,
      name: names.get(id) || catalogFeature(id)?.name || id,
      value: scopes[id],
      options: CATALOG_SCOPES.filter((scope) => supportedScopes(id).includes(scope.id)).map(
        (scope): SelectOption => ({ value: scope.id, label: scope.name }),
      ),
    }));
  });
  readonly icon = featureIcon;
  /** 圖示來自功能目錄，編輯器只顯示。 */
  catalogIcon(id: string) {
    return catalogFeature(id)?.icon ?? '';
  }
  /** 角色儲存後會授予的內容：已啟用群組中已啟用的功能。 */
  readonly proposedFeatures = computed(() => {
    const draft = this.editor(),
      catalog = this.store.catalog();
    if (!draft || !draft.enabled || draft.kind !== 'role' || !catalog) return [];
    const groups = new Set(draft.ids);
    const features = new Set(
      catalog.groups
        .filter((item) => item.enabled && groups.has(item.id))
        .flatMap((item) => item.featureIds),
    );
    return catalog.features.filter((item) => item.enabled && features.has(item.id));
  });
  /** 關閉後草稿仍保留，讓對話框在退場動畫中不會被清空。 */
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
      // 沒有頁面的功能（例如 `diagnostics.logs.export` 這類動作）沒有網址可顯示。
      subtitle: feature ? [feature.id, feature.route].filter(Boolean).join(' · ') : item?.id || '',
      enabled: role?.enabled ?? group?.enabled ?? feature?.enabled ?? true,
      ids: [...(role?.groupIds || group?.featureIds || [])],
      scopes: { ...group?.scopes },
      modelPolicy: modelPolicyDraft(group?.policy),
      storage:
        group?.policy?.storedAttachmentLimitBytes == null
          ? ''
          : storageLimitGb(group.policy.storedAttachmentLimitBytes),
      defaultName: (feature && catalogFeature(feature.id)?.name) || '',
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
  updateScope(feature: string, scope: string) {
    this.editor.update((old) =>
      old ? { ...old, scopes: { ...old.scopes, [feature]: scope } } : null,
    );
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
    const draft = this.editor();
    if (
      draft &&
      this.dirty() &&
      !(await this.confirmation().ask({
        title: '放棄未儲存的變更？',
        message: draft.isNew
          ? `尚未建立${this.label(draft.kind)}，關閉後會捨棄已填寫的內容。`
          : `「${draft.title}」的變更尚未儲存，關閉後會恢復為已儲存的設定。`,
        confirm: '放棄變更',
      }))
    )
      return;
    this.dialog().nativeElement.close();
  }
  async save(event: Event) {
    event.preventDefault();
    const draft = this.editor();
    if (!draft || this.locked() || (!draft.isNew && !this.dirty())) return;
    // 錯誤會打開含有該欄位的分頁並聚焦（或顯示訊息），
    // 即使欄位位於頁面較下方，修正處也能被看到。
    const fail = (section: GroupSection, message: string, field?: string) => {
      if (draft.kind === 'group') this.selectSection(section);
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
    // 功能的名稱可以留白，表示沿用目錄的預設名稱；其他項目都要有名稱。
    if (!draft.id || (draft.kind !== 'feature' && !draft.name.trim()))
      return fail('settings', '請輸入識別碼與名稱。', draft.id ? 'name' : 'id');
    let policy: GroupPolicyRequest | undefined;
    if (draft.kind === 'group') {
      try {
        policy = modelPolicyRequest(draft.modelPolicy);
      } catch (error) {
        return fail('models', safeMessage(error));
      }
      try {
        policy.storedAttachmentLimitBytes = parseStorageLimitGb(draft.storage);
      } catch (error) {
        return fail('settings', safeMessage(error), 'storage');
      }
    }
    this.saving.set(true);
    this.editorError.set('');
    try {
      if (draft.kind === 'role')
        await this.api.role(draft.id, {
          name: draft.name,
          enabled: draft.enabled,
          groupIds: draft.ids,
        });
      if (draft.kind === 'group' && policy)
        await this.api.group(draft.id, {
          name: draft.name,
          enabled: draft.enabled,
          featureIds: draft.ids,
          scopes: grantScopes(draft.ids, draft.scopes),
          policy,
        });
      if (draft.kind === 'feature')
        await this.api.feature(draft.id, {
          name: featureNameOverride(draft.name, draft.defaultName),
          enabled: draft.enabled,
        });
      this.opened.set(draft);
      this.dialog().nativeElement.close();
      this.store.saved(
        draft.isNew ? '已建立' + this.label(draft.kind) : '已儲存',
        '授權變更會在下一次操作生效。',
      );
    } catch (error) {
      this.editorError.set(safeMessage(error));
    } finally {
      this.saving.set(false);
    }
  }
  label(kind: AccessKind) {
    return LABELS[kind];
  }
}
