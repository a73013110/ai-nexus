import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import type { UnitDto } from '@core/api/schema';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { safeMessage } from '@core/errors/safe-errors';
import { uniqueId } from '@shared/browser/unique-id';
import { Checkbox } from '@shared/ui/checkbox';
import { CompactDialog } from '@shared/ui/compact-dialog';
import { ConfirmDialog } from '@shared/ui/confirm-dialog';
import { DialogHeader } from '@shared/ui/dialog-header';
import { DialogSaveBar } from '@shared/ui/dialog-save-bar';
import { Field } from '@shared/ui/field';
import { Notice } from '@shared/ui/notice';
import { PendingButton } from '@shared/ui/pending-button';
import { TreeSelect } from '@shared/ui/tree-select';
import { OrganizationApi } from './organization-api';
import { OrganizationStore } from './organization-store';
import { unitDialogTour } from './organization-tour';
import { MAX_DEPTH, heightOf, subtree, unitNodes } from './unit-tree';

interface Draft {
  /** 空字串為最上層。 */
  parentId: string;
  name: string;
  enabled: boolean;
}
interface Editor {
  id: string | null;
  title: string;
  draft: Draft;
  /** 來自公文系統的單位（`gdDepNo` 有值）。 */
  imported: boolean;
}

const sameDraft = (a: Draft, b: Draft) =>
  a.parentId === b.parentId && a.name.trim() === b.name.trim() && a.enabled === b.enabled;

/**
 * 新增或修改一個組織單位。修改時可換上層（整棵下層跟著搬，排到新一層最後），
 * 上層選單不列出自己與下層，也不列出搬過去會超過八層的單位；伺服器仍會再檢查一次。
 */
@Component({
  selector: 'nx-unit-dialog',
  imports: [
    Checkbox,
    CompactDialog,
    ConfirmDialog,
    DialogHeader,
    DialogSaveBar,
    Field,
    Notice,
    PendingButton,
    TreeSelect,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<dialog
      nxCompactDialog
      #unitDialog
      class="platform-dialog"
      size="md"
      [busy]="saving()"
      (cancel)="cancel($event)"
    >
      @if (editor(); as e) {
        <form class="dialog-form" novalidate (submit)="save($event)">
          <nx-dialog-header [heading]="e.title" closeLabel="關閉單位編輯" [tour]="tour()" />
          <div class="dialog-body">
            @if (error()) {
              <nx-notice tone="danger" [message]="error()" />
            }
            @if (imported()) {
              <nx-notice
                class="unit-imported"
                tone="warning"
                message="來自公文系統：這裡的修改會在下次匯入時改回。要保留停用等調整，匯入時不勾選這個部門；長期調整請改公文系統。"
              />
            }
            <div class="form-sections">
              <section class="form-section" role="group" [attr.aria-labelledby]="id + '-place'">
                <header class="form-section-head">
                  <h3 [id]="id + '-place'">名稱與位置</h3>
                  <p class="form-note">同一個上層底下名稱不可重複；組織最多八層。</p>
                </header>
                <div class="form-section-body">
                  <label class="form-field-short unit-name-field"
                    >名稱<input
                      #name
                      nxField
                      required
                      maxlength="120"
                      autocomplete="off"
                      [attr.aria-invalid]="nameError() ? true : null"
                      [attr.aria-describedby]="nameError() ? id + '-error' : null"
                      [value]="e.draft.name"
                      [readOnly]="locked()"
                      (input)="update('name', name.value)"
                  /></label>
                  @if (nameError()) {
                    <p class="form-note field-error" [id]="id + '-error'">{{ nameError() }}</p>
                  }
                  <label class="form-field-short unit-parent-field"
                    >上層單位<nx-tree-select
                      label="上層單位"
                      allLabel="最上層"
                      noun="單位"
                      [nodes]="parents()"
                      [value]="e.draft.parentId"
                      [disabled]="locked()"
                      (valueChange)="update('parentId', $event)"
                  /></label>
                </div>
              </section>
              <section class="form-section" role="group" [attr.aria-labelledby]="id + '-state'">
                <header class="form-section-head">
                  <h3 [id]="id + '-state'">狀態</h3>
                </header>
                <div class="form-section-body">
                  <nx-checkbox
                    class="unit-enabled"
                    label="啟用"
                    description="停用後保留成員與歷史，但它與所有下層都不參與資料範圍，也不能再加入成員。"
                    [checked]="e.draft.enabled"
                    [disabled]="locked()"
                    (checkedChange)="update('enabled', $event)"
                  />
                </div>
              </section>
            </div>
          </div>
          @if (!e.id) {
            <footer class="dialog-foot">
              @if (session.testing()) {
                <span class="dialog-note" role="status">{{ session.testingNote() }}</span>
              }
              <button
                type="button"
                class="secondary-button"
                [attr.aria-disabled]="saving() || null"
                (click)="requestClose()"
              >
                取消
              </button>
              <button
                nxPending
                type="submit"
                class="primary-button"
                [pending]="saving()"
                [disabled]="session.testing()"
              >
                {{ saving() ? '建立中…' : '建立單位' }}
              </button>
            </footer>
          } @else {
            <nx-dialog-save-bar
              label="儲存單位"
              [dirty]="dirty()"
              [saving]="saving()"
              [disabled]="session.testing()"
              [note]="session.testingNote()"
              (revert)="revert()"
            />
          }
        </form>
      }
    </dialog>
    <nx-confirm-dialog />`,
  styles: `
    .field-error {
      color: var(--error);
    }
  `,
})
export class UnitDialog {
  readonly id = uniqueId('nx-unit-dialog');
  readonly session = inject(WorkspaceSession);
  private readonly api = inject(OrganizationApi);
  private readonly store = inject(OrganizationStore);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('unitDialog');
  private readonly nameInput = viewChild<ElementRef<HTMLInputElement>>('name');
  private readonly confirmation = viewChild.required(ConfirmDialog);
  readonly editor = signal<Editor | null>(null);
  private readonly opened = signal<Draft | null>(null);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly nameError = signal('');
  readonly locked = computed(() => this.saving() || this.session.testing());
  readonly imported = computed(() => !!this.editor()?.imported);
  readonly tour = computed(() =>
    unitDialogTour({
      editing: !!this.editor()?.id,
      testing: this.session.testing(),
      imported: this.imported(),
    }),
  );
  readonly dirty = computed(() => {
    const editor = this.editor(),
      opened = this.opened();
    return !!editor && !!opened && !sameDraft(editor.draft, opened);
  });
  /** 可選的上層：排除自己與下層，以及放進去後整棵子樹會超過上限的單位；「最上層」由選單釘在最上方。 */
  readonly parents = computed(() => {
    const units = this.store.units();
    const id = this.editor()?.id;
    const excluded = id ? subtree(units, id) : new Set<string>();
    const height = id ? heightOf(units, id) : 1;
    return unitNodes(units, (row) => !excluded.has(row.unit.id) && row.depth + height <= MAX_DEPTH);
  });

  create(parent: UnitDto | null = null) {
    this.show(null, parent ? `新增下層單位：${parent.name}` : '新增單位', false, {
      parentId: parent?.id ?? '',
      name: '',
      enabled: true,
    });
  }
  edit(unit: UnitDto) {
    this.show(unit.id, `編輯單位：${unit.name}`, unit.gdDepNo != null, {
      parentId: unit.parentId ?? '',
      name: unit.name,
      enabled: unit.enabled,
    });
  }
  private show(id: string | null, title: string, imported: boolean, draft: Draft) {
    this.error.set('');
    this.nameError.set('');
    this.editor.set({ id, title, draft, imported });
    this.opened.set(draft);
    this.dialog().nativeElement.showModal();
  }
  update<K extends keyof Draft>(key: K, value: Draft[K]) {
    this.editor.update((editor) =>
      editor ? { ...editor, draft: { ...editor.draft, [key]: value } } : null,
    );
    if (key === 'name') this.nameError.set('');
  }
  revert() {
    const opened = this.opened();
    this.editor.update((editor) => (editor && opened ? { ...editor, draft: opened } : editor));
    this.error.set('');
    this.nameError.set('');
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
          : '尚未建立單位，關閉後會捨棄已填寫的內容。',
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
    const name = draft.name.trim();
    if (!name || name.length > 120) {
      this.nameError.set('請輸入 120 字元內的名稱。');
      this.nameInput()?.nativeElement.focus();
      return;
    }
    const body = { parentId: draft.parentId || null, name, enabled: draft.enabled };
    this.saving.set(true);
    this.error.set('');
    try {
      if (editor.id) await this.api.updateUnit(editor.id, body);
      else this.store.select(await this.api.createUnit(body));
      this.opened.set(draft);
      this.dialog().nativeElement.close();
      this.store.toast.success(editor.id ? '已儲存' : `已新增「${name}」`);
      this.store.changed();
    } catch (error) {
      this.error.set(safeMessage(error));
    } finally {
      this.saving.set(false);
    }
  }
}
