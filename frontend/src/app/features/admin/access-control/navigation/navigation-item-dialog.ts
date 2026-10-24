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
import type { NavigationItemDto } from '@core/api/schema';
import { CATALOG_FEATURES } from '@core/api/feature-catalog';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { safeMessage } from '@core/errors/safe-errors';
import { uniqueId } from '@shared/browser/unique-id';
import { Checkbox } from '@shared/ui/checkbox';
import { ChoiceGroups, type ChoiceGroup } from '@shared/ui/choice-groups';
import { CompactDialog } from '@shared/ui/compact-dialog';
import { ConfirmDialog } from '@shared/ui/confirm-dialog';
import { DialogHeader } from '@shared/ui/dialog-header';
import { DialogSaveBar } from '@shared/ui/dialog-save-bar';
import { Field } from '@shared/ui/field';
import { ICON_NAMES } from '@shared/ui/icon';
import { Notice } from '@shared/ui/notice';
import { PendingButton } from '@shared/ui/pending-button';
import { Select, type SelectOption } from '@shared/ui/select';
import { ViewSwitch, type ViewOption } from '@shared/ui/view-switch';
import { AccessControlApi } from '../access-control-api';
import { accessPageNames, navigationItemTour } from '../access-control-tour';
import { AccessControlStore, accessChoice } from '../access-control-store';
import { NavigationStore } from '../navigation-store';
import { sameIds } from '../roles/same-ids';
import { NAVIGATION_KINDS, navigationKindLabel } from './navigation-kinds';
import { MAX_DEPTH, depthOf, editorTree, type EditorNode } from './navigation-order';
import type { NavigationKind } from '@core/layout/navigation-tree';

interface Draft {
  kind: NavigationKind;
  /** 新增的位置；空字串為最上層。修改時不能換位置（用拖曳或「移到…」）。 */
  parentId: string;
  featureId: string;
  name: string;
  url: string;
  openInNewTab: boolean;
  icon: string;
  audienceGroupIds: string[];
}
/** 欄位旁的驗證說明；伺服器錯誤放在 `error`。 */
interface FieldError {
  field: 'featureId' | 'name' | 'url';
  message: string;
}
interface Editor {
  id: string | null;
  title: string;
  draft: Draft;
}

/**
 * 外部連結的網址：絕對 `https://`、不含帳號密碼，與後端 `NavigationLinks.WellFormed` 相同。
 * 主機是否在允許清單內只有伺服器知道，由它回 `navigation_link_host_not_allowed`。
 */
export function wellFormedLink(value: string) {
  try {
    const url = new URL(value.trim());
    return url.protocol === 'https:' && !!url.hostname && !url.username && !url.password;
  } catch {
    return false;
  }
}

function sameDraft(first: Draft, second: Draft) {
  return (
    first.kind === second.kind &&
    first.parentId === second.parentId &&
    first.featureId === second.featureId &&
    first.name.trim() === second.name.trim() &&
    first.url.trim() === second.url.trim() &&
    first.openInNewTab === second.openInNewTab &&
    first.icon === second.icon &&
    sameIds(first.audienceGroupIds, second.audienceGroupIds)
  );
}

/**
 * 新增或修改一個選單節點（分群、頁面或外部連結）。新增時可換種類與位置；修改時種類固定、位置不動
 * （伺服器也不接受），只改內容。外部連結可限定只有某些功能群組看得到；不選＝所有登入者。
 * 失敗時保留輸入，錯誤代碼的提示顯示在對話框內。
 */
@Component({
  selector: 'nx-navigation-item-dialog',
  imports: [
    Checkbox,
    ChoiceGroups,
    CompactDialog,
    ConfirmDialog,
    DialogHeader,
    DialogSaveBar,
    Field,
    Notice,
    PendingButton,
    Select,
    ViewSwitch,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './navigation-item-dialog.html',
  styles: `
    .field-error {
      color: var(--error);
    }
  `,
})
export class NavigationItemDialog {
  readonly id = uniqueId('nx-navigation-item');
  readonly session = inject(WorkspaceSession);
  private readonly api = inject(AccessControlApi);
  private readonly access = inject(AccessControlStore);
  private readonly store = inject(NavigationStore);
  private readonly injector = inject(Injector);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('itemDialog');
  private readonly body = viewChild<ElementRef<HTMLElement>>('body');
  private readonly confirmation = viewChild.required(ConfirmDialog);
  readonly editor = signal<Editor | null>(null);
  private readonly opened = signal<Draft | null>(null);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly fieldError = signal<FieldError | null>(null);
  readonly locked = computed(() => this.saving() || this.session.testing());
  readonly dirty = computed(() => {
    const editor = this.editor(),
      opened = this.opened();
    return !!editor && !!opened && !sameDraft(editor.draft, opened);
  });
  readonly kinds: ViewOption[] = NAVIGATION_KINDS.map(({ kind, label, icon }) => ({
    value: kind,
    label,
    icon,
  }));
  /** 目錄中的頁面（動作與閱讀器沒有網址，不能放進選單），名稱用管理員的覆寫。 */
  readonly pages = computed<SelectOption[]>(() => {
    const features = new Map(
      (this.access.catalog()?.features ?? []).map((item) => [item.id, item]),
    );
    return CATALOG_FEATURES.filter((item) => item.kind === 'page').map((item) => {
      const feature = features.get(item.id);
      return {
        value: item.id,
        label: (feature?.name || item.name) + (feature?.enabled === false ? '（停用）' : ''),
        description: item.id,
      };
    });
  });
  /**
   * 新增的位置：最上層或任一分群（以路徑顯示）。新節點是葉節點，所以分群本身已在第四層時不能再放。
   */
  readonly parents = computed<SelectOption[]>(() => {
    const items = this.store.items();
    const options: SelectOption[] = [{ value: '', label: '最上層' }];
    const visit = (nodes: EditorNode[], trail: string[]) => {
      for (const node of nodes) {
        if (node.item.kind !== 'group') continue;
        const path = [...trail, this.store.name(node.item)];
        if (depthOf(items, node.item.id) < MAX_DEPTH)
          options.push({
            value: node.item.id,
            label: path.at(-1)!,
            level: path.length,
            path: trail,
          });
        visit(node.children, path);
      }
    };
    visit(editorTree(items), []);
    return options;
  });
  readonly icons: SelectOption[] = [
    { value: '', label: '不設定' },
    ...[...ICON_NAMES].sort().map((name) => ({ value: name, label: name })),
  ];
  readonly audiences = computed<ChoiceGroup[]>(() => [
    {
      id: 'groups',
      name: '功能群組',
      options: (this.access.catalog()?.groups ?? []).map(accessChoice),
    },
  ]);
  readonly label = navigationKindLabel;
  readonly tour = computed(() => {
    const editor = this.editor();
    return editor
      ? navigationItemTour({
          names: accessPageNames((id) => this.session.featureName(id)),
          kind: editor.draft.kind,
          creating: !editor.id,
          testing: this.session.testing(),
        })
      : null;
  });

  /** 新增：`parentId` 為 `null` 時放在最上層。 */
  create(kind: NavigationKind, parentId: string | null = null) {
    this.show(null, '新增選單項目', {
      kind,
      parentId: parentId ?? '',
      featureId: '',
      name: '',
      url: '',
      openInNewTab: true,
      icon: '',
      audienceGroupIds: [],
    });
  }
  edit(item: NavigationItemDto) {
    const kind = item.kind as NavigationKind;
    this.show(item.id, `編輯${navigationKindLabel(kind)}：${this.store.name(item)}`, {
      kind,
      parentId: item.parentId ?? '',
      featureId: item.featureId ?? '',
      name: item.name ?? '',
      url: item.url ?? '',
      openInNewTab: item.openInNewTab,
      icon: item.icon ?? '',
      audienceGroupIds: [...item.audienceGroupIds],
    });
  }
  private show(id: string | null, title: string, draft: Draft) {
    this.error.set('');
    this.fieldError.set(null);
    this.editor.set({ id, title, draft });
    this.opened.set(draft);
    this.dialog().nativeElement.showModal();
  }
  update<K extends keyof Draft>(key: K, value: Draft[K]) {
    this.editor.update((editor) =>
      editor ? { ...editor, draft: { ...editor.draft, [key]: value } } : null,
    );
    if (this.fieldError()?.field === key) this.fieldError.set(null);
  }
  revert() {
    const opened = this.opened();
    this.editor.update((editor) => (editor && opened ? { ...editor, draft: opened } : editor));
    this.error.set('');
    this.fieldError.set(null);
  }
  cancel(event: Event) {
    event.preventDefault();
    void this.requestClose();
  }
  async requestClose() {
    if (this.saving()) return;
    const editor = this.editor();
    if (
      editor &&
      this.dirty() &&
      !(await this.confirmation().ask({
        title: '放棄未儲存的變更？',
        message: editor.id
          ? `「${editor.title}」的變更尚未儲存，關閉後會恢復為已儲存的內容。`
          : '尚未建立選單項目，關閉後會捨棄已填寫的內容。',
        confirm: '放棄變更',
      }))
    )
      return;
    this.dialog().nativeElement.close();
  }

  async save(event: Event) {
    event.preventDefault();
    const editor = this.editor();
    if (!editor || this.locked() || (editor.id && !this.dirty())) return;
    const draft = editor.draft;
    const invalid = this.validate(draft);
    if (invalid) {
      this.fieldError.set(invalid);
      afterNextRender(
        () => {
          // 頁面欄位是 Select：焦點交給它的按鈕。
          const target = this.body()?.nativeElement.querySelector<HTMLElement>(
            `[data-field="${invalid.field}"]`,
          );
          (target?.matches('input')
            ? target
            : target?.querySelector<HTMLElement>('button')
          )?.focus();
        },
        { injector: this.injector },
      );
      return;
    }
    // 每種節點只送它該有的欄位，其餘為 null（伺服器的 RequestValidator 會拒絕多餘的欄位）。
    const link = draft.kind === 'link';
    const content = {
      kind: draft.kind,
      featureId: draft.kind === 'page' ? draft.featureId : null,
      name: draft.kind === 'page' ? null : draft.name.trim(),
      url: link ? draft.url.trim() : null,
      openInNewTab: link ? draft.openInNewTab : true,
      icon: draft.kind === 'page' ? null : draft.icon || null,
      audienceGroupIds: link ? draft.audienceGroupIds : null,
    };
    this.saving.set(true);
    this.error.set('');
    try {
      if (editor.id) await this.api.updateNavigationItem(editor.id, content);
      else await this.api.createNavigationItem({ ...content, parentId: draft.parentId || null });
      this.opened.set(draft);
      this.dialog().nativeElement.close();
      this.store.toast.success(
        editor.id ? '已儲存' : `已新增${navigationKindLabel(draft.kind)}`,
        '側欄選單已更新。',
      );
      this.store.changed();
    } catch (error) {
      this.error.set(safeMessage(error));
    } finally {
      this.saving.set(false);
    }
  }
  private validate(draft: Draft): FieldError | null {
    if (draft.kind === 'page')
      return draft.featureId ? null : { field: 'featureId', message: '請選擇要放進選單的頁面。' };
    if (!draft.name.trim()) return { field: 'name', message: '請輸入 120 字元內的名稱。' };
    if (draft.kind === 'link' && !wellFormedLink(draft.url))
      return {
        field: 'url',
        message: '請輸入 https:// 開頭的完整網址，不可包含帳號或密碼。',
      };
    return null;
  }
}
