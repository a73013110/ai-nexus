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
import { safeMessage } from '@core/errors/safe-errors';
import { uniqueId } from '@shared/browser/unique-id';
import { Checkbox } from '@shared/ui/checkbox';
import { CompactDialog } from '@shared/ui/compact-dialog';
import { ConfirmDialog } from '@shared/ui/confirm-dialog';
import { DialogHeader } from '@shared/ui/dialog-header';
import { DialogSaveBar } from '@shared/ui/dialog-save-bar';
import { Field } from '@shared/ui/field';
import { InfoPopover } from '@shared/ui/info-popover';
import { Notice } from '@shared/ui/notice';
import { PendingButton } from '@shared/ui/pending-button';
import { Select, type SelectOption } from '@shared/ui/select';
import { TreeSelect } from '@shared/ui/tree-select';
import { OrganizationApi } from './organization-api';
import { OrganizationStore } from './organization-store';
import { unitDialogTour } from './organization-tour';
import { MAX_DEPTH, MAX_HEAD_RANK, headsOf, heightOf, subtree, unitNodes } from './unit-tree';

interface Draft {
  /** 空字串為最上層。 */
  parentId: string;
  name: string;
  enabled: boolean;
  /** 首長單位的層級；空字串為一般單位。 */
  headRank: string;
}
interface Editor {
  id: string | null;
  title: string;
  draft: Draft;
  /** 來自公文系統的單位（`gdDepNo` 有值）。 */
  imported: boolean;
}

const sameDraft = (a: Draft, b: Draft) =>
  a.parentId === b.parentId &&
  a.name.trim() === b.name.trim() &&
  a.enabled === b.enabled &&
  a.headRank === b.headRank;

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
    InfoPopover,
    Notice,
    PendingButton,
    Select,
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
                message="來自公文系統，這裡的修改會在下次匯入時改回。要保留停用等調整，匯入時請不勾選這個部門。長期調整請改公文系統。"
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
                      [readOnly]="saving()"
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
                      [disabled]="saving()"
                      (valueChange)="update('parentId', $event)"
                  /></label>
                </div>
              </section>
              <section class="form-section" role="group" [attr.aria-labelledby]="id + '-head'">
                <header class="form-section-head">
                  <h3 [id]="id + '-head'">首長單位</h3>
                  <p class="form-note">與各處同層的董事會、總經理室等，標為首長單位來帶領上層。</p>
                  <nx-info-popover label="首長單位的規則" [width]="320">
                    <ul class="form-note-list">
                      <li>
                        主管另外管轄上層，以及上層底下層級不比它高的單位與下層；數字越小層級越高，例如董事會
                        1、總經理室 2。
                      </li>
                      <li>
                        上層的主管改由首長單位擔任：儲存後取消上層的主管勾選，之後也不能再勾選。
                      </li>
                      <li>只影響主管範圍；成員的「所屬單位及下層」仍照單位樹。</li>
                      <li>本地設定，公文系統匯入不會覆蓋；單位被匯入搬到別的上層時清除。</li>
                    </ul>
                  </nx-info-popover>
                </header>
                <div class="form-section-body">
                  <label class="form-field-short unit-head-field"
                    >層級<nx-select
                      label="首長單位的層級"
                      [options]="headRanks()"
                      [value]="e.draft.headRank"
                      [disabled]="saving() || !!headBlocked()"
                      (valueChange)="update('headRank', $event)"
                  /></label>
                  @if (headBlocked(); as reason) {
                    <p class="form-note">{{ reason }}</p>
                  }
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
                    [disabled]="saving()"
                    (checkedChange)="update('enabled', $event)"
                  />
                </div>
              </section>
            </div>
          </div>
          @if (!e.id) {
            <footer class="dialog-foot">
              <button
                type="button"
                class="secondary-button"
                [attr.aria-disabled]="saving() || null"
                (click)="requestClose()"
              >
                取消
              </button>
              <button nxPending type="submit" class="primary-button" [pending]="saving()">
                {{ saving() ? '建立中…' : '建立單位' }}
              </button>
            </footer>
          } @else {
            <nx-dialog-save-bar
              label="儲存單位"
              [dirty]="dirty()"
              [saving]="saving()"
              (revert)="revert()"
            />
          }
        </form>
      }
    </dialog>
    <nx-confirm-dialog />`,
})
export class UnitDialog {
  readonly id = uniqueId('nx-unit-dialog');
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
  readonly imported = computed(() => !!this.editor()?.imported);
  readonly tour = computed(() =>
    unitDialogTour({
      editing: !!this.editor()?.id,
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
  /** 不能設為首長單位的原因；已設的仍可改回一般單位，所以有值時不擋。 */
  readonly headBlocked = computed(() => {
    const editor = this.editor();
    if (!editor || editor.draft.headRank) return '';
    if (!editor.draft.parentId) return '最上層的單位沒有上層可帶領。';
    return editor.id && headsOf(this.store.units(), editor.id).length
      ? '底下已有首長單位，不能再設為首長單位。'
      : '';
  });
  /** 層級選項；同一個上層已被其他單位使用的層級停用，並標出是誰。 */
  readonly headRanks = computed<SelectOption[]>(() => {
    const editor = this.editor();
    const taken = new Map(
      (editor?.draft.parentId ? headsOf(this.store.units(), editor.draft.parentId) : [])
        .filter((unit) => unit.id !== editor?.id)
        .map((unit) => [unit.headRank!, unit.name]),
    );
    return [
      { value: '', label: '不是首長單位' },
      ...Array.from({ length: MAX_HEAD_RANK }, (_, index) => {
        const rank = index + 1;
        const owner = taken.get(rank);
        return {
          value: String(rank),
          label: `層級 ${rank}${rank === 1 ? '（最高）' : ''}`,
          description: owner ? `已由「${owner}」使用` : undefined,
          disabled: !!owner,
        };
      }),
    ];
  });

  create(parent: UnitDto | null = null) {
    this.show(null, parent ? `新增下層單位：${parent.name}` : '新增單位', false, {
      parentId: parent?.id ?? '',
      name: '',
      enabled: true,
      headRank: '',
    });
  }
  edit(unit: UnitDto) {
    this.show(unit.id, `編輯單位：${unit.name}`, unit.gdDepNo != null, {
      parentId: unit.parentId ?? '',
      name: unit.name,
      enabled: unit.enabled,
      headRank: unit.headRank == null ? '' : String(unit.headRank),
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
    if (!editor || this.saving() || (editor.id && !this.dirty())) return;
    const draft = editor.draft;
    const name = draft.name.trim();
    if (!name || name.length > 120) {
      this.nameError.set('請輸入 120 字元內的名稱。');
      this.nameInput()?.nativeElement.focus();
      return;
    }
    const body = {
      parentId: draft.parentId || null,
      name,
      enabled: draft.enabled,
      headRank: draft.headRank ? Number(draft.headRank) : null,
    };
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
