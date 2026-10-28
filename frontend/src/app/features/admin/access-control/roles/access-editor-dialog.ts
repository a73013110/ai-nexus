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
import { EmptyState } from '@shared/ui/empty-state';
import { Field } from '@shared/ui/field';
import { Icon } from '@shared/ui/icon';
import { InfoPopover } from '@shared/ui/info-popover';
import { Notice } from '@shared/ui/notice';
import { PendingButton } from '@shared/ui/pending-button';
import { Tabs, type TabItem } from '@shared/ui/tabs';
import { ViewMotion } from '@shared/ui/view-motion';
import { ViewSwitch, type ViewOption } from '@shared/ui/view-switch';
import { AccessControlApi } from '../access-control-api';
import { AccessControlStore, accessChoice } from '../access-control-store';
import { accessEditorTour, accessPageNames } from '../access-control-tour';
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
/** 功能群組的頁籤：基本資料與附件容量、授予功能、各功能的資料範圍、AI 模型；一次儲存涵蓋全部。 */
export type GroupSection = 'settings' | 'features' | 'scopes' | 'models';
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
  /** 主管另可看的範圍（功能 ID → 範圍）；沒有或空字串＝不另外，見 `grantManagerScopes`。 */
  managerScopes: Record<string, string>;
  modelPolicy: ModelPolicyDraft;
  storage: string;
  /** 功能的目錄預設名稱；名稱留白或與它相同就是不覆寫。 */
  defaultName: string;
  isNew: boolean;
}

/** 授權上的兩種範圍：`scopes` 給群組的所有成員，`managerScopes` 是成員中組織勾選為主管的人另可看的（ADR 0017、0022）。 */
type ScopeAxis = 'scopes' | 'managerScopes';
/** 「主管另可看」的「不另外」；選項的值是字串，用空字串代表沒有主管範圍。 */
const NO_MANAGER_SCOPE = '';

/**
 * 選了某個範圍後「看得到誰」的白話說明。名稱只是代碼的標籤，管理員常分不清「所屬單位」含不含下層，
 * 所以每次選擇都換成一句完整的話；新增範圍代碼時要在這裡補上，否則只顯示名稱。
 */
const MEMBER_REACH: Record<string, string> = {
  own: '成員只看得到自己的資料',
  assigned: '成員看得到指派給自己的資料',
  unit: '成員看得到所屬單位裡每個人的資料',
  'unit-tree': '成員看得到所屬單位及所有下層單位的資料',
  all: '成員看得到所有人的資料',
};
const MANAGER_REACH: Record<string, string> = {
  managed: '所管單位',
  'managed-tree': '所管單位及所有下層單位',
};

/** 功能在目錄宣告支援的範圍，由窄到寬；空的表示授權不帶這種範圍。 */
function supportedScopes(id: string, axis: ScopeAxis): readonly string[] {
  return catalogFeature(id)?.[axis] ?? [];
}

function scopeOptions(ids: readonly string[]): ViewOption[] {
  return CATALOG_SCOPES.filter((scope) => ids.includes(scope.id)).map((scope) => ({
    value: scope.id,
    label: scope.name,
  }));
}

function scopeName(id: string) {
  return CATALOG_SCOPES.find((scope) => scope.id === id)?.name ?? id;
}

/** 一列範圍的白話結果：成員看得到誰，加上主管另外看得到的（所有成員已是全部時說明已含）。 */
export function scopeReach(scope: string, managerScope: string | null, managed: boolean) {
  const members = MEMBER_REACH[scope] ?? scopeName(scope);
  if (managed && scope === 'all') return members + '，已包含主管所管的單位。';
  if (!managerScope) return members + '。';
  return `${members}；擔任主管的人另外看得到${MANAGER_REACH[managerScope] ?? scopeName(managerScope)}的資料。`;
}

/**
 * 送給伺服器的範圍：只含已勾選、且目錄有宣告範圍的功能，每個都有一個它支援的值。
 * 沒選過（或舊資料的值已不支援）時取最窄的範圍，授予時預設最小權限。
 */
export function grantScopes(ids: readonly string[], scopes: Record<string, string>) {
  const result: Record<string, string> = {};
  for (const id of ids) {
    const supported = supportedScopes(id, 'scopes');
    if (supported.length) result[id] = supported.includes(scopes[id]) ? scopes[id] : supported[0];
  }
  return result;
}

/**
 * 送給伺服器的主管範圍：只含已勾選、目錄有宣告主管範圍且選了支援值的功能；其餘（含新勾選的）是不另外。
 * 所有成員已是全部時不送：伺服器也不會另外計算。
 */
export function grantManagerScopes(
  ids: readonly string[],
  scopes: Record<string, string>,
  managerScopes: Record<string, string>,
) {
  const members = grantScopes(ids, scopes);
  const result: Record<string, string> = {};
  for (const id of ids)
    if (members[id] !== 'all' && supportedScopes(id, 'managerScopes').includes(managerScopes[id]))
      result[id] = managerScopes[id];
  return result;
}

function sameRecord(a: Record<string, string>, b: Record<string, string>) {
  const keys = Object.keys(a);
  return keys.length === Object.keys(b).length && keys.every((id) => a[id] === b[id]);
}

function sameScopes(first: Editor, second: Editor) {
  return (
    sameRecord(grantScopes(first.ids, first.scopes), grantScopes(second.ids, second.scopes)) &&
    sameRecord(
      grantManagerScopes(first.ids, first.scopes, first.managerScopes),
      grantManagerScopes(second.ids, second.scopes, second.managerScopes),
    )
  );
}

/** 「群組設定」頁籤持有的欄位；角色與功能也只比較這些加上 `ids`。 */
function sameBasics(first: Editor, second: Editor) {
  return (
    first.id === second.id &&
    first.name === second.name &&
    first.enabled === second.enabled &&
    first.storage.trim() === second.storage.trim()
  );
}

/** 草稿是否與已儲存的內容相同。 */
function sameEditor(first: Editor, second: Editor) {
  return (
    sameBasics(first, second) &&
    sameIds(first.ids, second.ids) &&
    sameScopes(first, second) &&
    sameModelPolicy(first.modelPolicy, second.modelPolicy)
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
 * 群組分成群組設定、功能授權、資料範圍與 AI 模型四個頁籤；一次儲存涵蓋全部。有未儲存的變更時，關閉會先詢問。
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
    EmptyState,
    Field,
    FeatureSummary,
    Icon,
    InfoPopover,
    ModelPolicyEditor,
    NgTemplateOutlet,
    Notice,
    PendingButton,
    Tabs,
    ViewMotion,
    ViewSwitch,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './access-editor-dialog.html',
  styleUrl: './access-editor-dialog.scss',
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
  readonly editorError = signal('');
  /** 功能群組目前的頁籤。 */
  readonly groupSection = signal<GroupSection>('settings');
  /**
   * 四個頁籤由同一次儲存送出，各自標記自己持有的變更。功能授權與資料範圍帶數量，不切過去也知道授予了幾個、
   * 有幾個要選範圍；新勾選有範圍的功能時兩者都會標記，因為它的預設範圍也會一起儲存。
   */
  readonly groupSections = computed<readonly (TabItem & { value: GroupSection })[]>(() => {
    const draft = this.editor(),
      opened = this.opened();
    const changed = (same: (first: Editor, second: Editor) => boolean) =>
      !!draft && !!opened && !same(draft, opened);
    return [
      { value: 'settings', label: '群組設定', dirty: changed(sameBasics) },
      {
        value: 'features',
        label: '功能授權',
        count: draft?.ids.length,
        dirty: changed((first, second) => sameIds(first.ids, second.ids)),
      },
      {
        value: 'scopes',
        label: '資料範圍',
        count: this.scopedGrants().length,
        dirty: changed(sameScopes),
      },
      {
        value: 'models',
        label: 'AI 模型',
        dirty: changed((first, second) => sameModelPolicy(first.modelPolicy, second.modelPolicy)),
      },
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
  /** 功能依側欄的位置分組（`featureSections`），與使用者在側欄看到的一致。 */
  private readonly sections = computed(() =>
    featureSections(
      this.store.catalog()?.features ?? [],
      this.store.catalog()?.navigationSections ?? [],
    ),
  );
  /** 群組授予功能；已勾選且有資料範圍的功能在 id 後附上目前的範圍，不切頁籤也看得到。 */
  readonly featureChoices = computed<ChoiceGroup[]>(() => {
    const draft = this.editor();
    const scopes = draft?.kind === 'group' ? grantScopes(draft.ids, draft.scopes) : {};
    return this.sections().map((section) => ({
      id: section.id,
      name: section.name,
      options: section.features.map((item) => {
        const choice = accessChoice(item);
        return scopes[item.id]
          ? {
              ...choice,
              description: `${choice.description} · 資料範圍：${scopeName(scopes[item.id])}`,
            }
          : choice;
      }),
    }));
  });
  /** 角色加入功能群組；群組沒有分類，所以整體顯示為一張卡片。 */
  readonly groupChoices = computed<ChoiceGroup[]>(() => [
    {
      id: 'groups',
      name: '全部功能群組',
      options: (this.store.catalog()?.groups || []).map(accessChoice),
    },
  ]);
  /**
   * 已勾選且有資料範圍的功能一列，選項只列目錄宣告支援的（由窄到寬）。`manager` 是「主管另可看」，
   * 功能沒宣告主管範圍時為 null；所有成員已是全部時 `included`，畫面改說明已含。
   * `fresh` 是這次才勾選的功能：它已套用最窄的預設範圍，提醒管理員確認。
   */
  readonly scopedGrants = computed(() => {
    const draft = this.editor();
    if (!draft || draft.kind !== 'group') return [];
    const scopes = grantScopes(draft.ids, draft.scopes),
      managerScopes = grantManagerScopes(draft.ids, draft.scopes, draft.managerScopes);
    const granted = new Set(this.opened()?.ids ?? []);
    const sections = new Map(
      this.sections().flatMap((section) => section.features.map((item) => [item.id, section.name])),
    );
    const names = new Map(
      (this.store.catalog()?.features || []).map((item) => [item.id, item.name]),
    );
    return Object.keys(scopes).map((id) => {
      const managers = supportedScopes(id, 'managerScopes');
      return {
        id,
        name: names.get(id) || catalogFeature(id)?.name || id,
        icon: catalogFeature(id)?.icon ?? '',
        section: sections.get(id) ?? '',
        value: scopes[id],
        options: scopeOptions(supportedScopes(id, 'scopes')),
        manager: managers.length
          ? {
              value: managers.includes(draft.managerScopes[id])
                ? draft.managerScopes[id]
                : NO_MANAGER_SCOPE,
              options: [{ value: NO_MANAGER_SCOPE, label: '不另外' }, ...scopeOptions(managers)],
              included: scopes[id] === 'all',
            }
          : null,
        reach: scopeReach(scopes[id], managerScopes[id] ?? null, managers.length > 0),
        fresh: !granted.has(id),
      };
    });
  });
  /** 能設定範圍的功能名稱，空狀態用來說明哪些功能勾選後才會出現在資料範圍頁籤。 */
  readonly scopableNames = computed(() =>
    this.sections()
      .flatMap((section) => section.features)
      .filter((item) => supportedScopes(item.id, 'scopes').length)
      .map((item) => item.name)
      .join('、'),
  );
  /** 有任一功能能另外給主管時，才需要說明主管是誰。 */
  readonly managerScoped = computed(() => this.scopedGrants().some((grant) => grant.manager));
  readonly tour = computed(() => {
    const draft = this.editor();
    return draft
      ? accessEditorTour({
          names: accessPageNames((id) => this.session.featureName(id)),
          kind: draft.kind,
          creating: draft.isNew,
          section: this.groupSection(),
          scoped: this.scopedGrants().length > 0,
        })
      : null;
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
      managerScopes: { ...group?.managerScopes },
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
  updateScope(feature: string, scope: string, axis: ScopeAxis = 'scopes') {
    this.editor.update((old) =>
      old ? { ...old, [axis]: { ...old[axis], [feature]: scope } } : null,
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
    if (!draft || this.saving() || (!draft.isNew && !this.dirty())) return;
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
          managerScopes: grantManagerScopes(draft.ids, draft.scopes, draft.managerScopes),
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
