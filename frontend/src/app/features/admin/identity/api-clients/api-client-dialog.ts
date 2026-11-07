import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  inject,
  output,
  signal,
  viewChild,
} from '@angular/core';
import type { ApiClientDto, IssuedApiClientKeyDto } from '@core/api/schema';
import { uniqueId } from '@shared/browser/unique-id';
import { CompactDialog } from '@shared/ui/compact-dialog';
import { ConfirmDialog } from '@shared/ui/confirm-dialog';
import { DialogHeader } from '@shared/ui/dialog-header';
import { DialogSaveBar } from '@shared/ui/dialog-save-bar';
import { DraftEditor } from '@shared/ui/draft-editor';
import { Field } from '@shared/ui/field';
import { Notice } from '@shared/ui/notice';
import { PendingButton } from '@shared/ui/pending-button';
import { ApiClientsStore } from './api-clients-store';

interface Draft {
  name: string;
  contact: string;
}
/** 新增（`client` 為 null）或編輯一個外部系統。 */
interface Target {
  client: ApiClientDto | null;
}

/** 與後端 `ApiClient.NameMaxLength`、`ContactMaxLength` 相同；伺服器仍會再檢查。 */
const NAME_MAX = 100;
const CONTACT_MAX = 100;
const sameDraft = (a: Draft, b: Draft) =>
  a.name.trim() === b.name.trim() && a.contact.trim() === b.contact.trim();

/**
 * 新增或編輯外部系統的名稱與聯絡人。新增成功後由頁面接著顯示一次金鑰；編輯不換金鑰。
 * 停用與啟用在列選單直接切換，不在這裡。
 */
@Component({
  selector: 'nx-api-client-dialog',
  imports: [
    CompactDialog,
    ConfirmDialog,
    DialogHeader,
    DialogSaveBar,
    Field,
    Notice,
    PendingButton,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<dialog
      nxCompactDialog
      #dialog
      class="platform-dialog"
      size="md"
      [busy]="store.busy()"
      (cancel)="cancel($event)"
    >
      @if (editor.current(); as e) {
        <form class="dialog-form" novalidate (submit)="save($event)">
          <nx-dialog-header
            [heading]="e.client ? '編輯外部系統' : '新增外部系統'"
            [detail]="e.client?.name ?? ''"
            closeLabel="關閉外部系統編輯"
          />
          <div class="dialog-body">
            @if (error()) {
              <nx-notice tone="danger" [message]="error()" />
            }
            <p class="form-note">
              {{
                e.client
                  ? '金鑰不變，對方不必更新設定。'
                  : '新增後會產生金鑰，只顯示一次，請當場交給對方。'
              }}
            </p>
            <label
              >名稱<input
                #name
                nxField
                required
                autocomplete="off"
                placeholder="公文系統"
                [attr.maxlength]="nameMax"
                [attr.aria-invalid]="nameError() ? true : null"
                [attr.aria-describedby]="nameError() ? id + '-error' : null"
                [value]="e.draft.name"
                [readOnly]="store.busy()"
                (input)="update('name', name.value)"
            /></label>
            @if (nameError()) {
              <p class="form-note field-error" [id]="id + '-error'">{{ nameError() }}</p>
            }
            <label
              >聯絡人（選填）<input
                #contact
                nxField
                autocomplete="off"
                placeholder="王小明 #1234"
                [attr.maxlength]="contactMax"
                [value]="e.draft.contact"
                [readOnly]="store.busy()"
                (input)="update('contact', contact.value)"
            /></label>
          </div>
          @if (!e.client) {
            <footer class="dialog-foot">
              <button
                type="button"
                class="secondary-button"
                [attr.aria-disabled]="store.busy() || null"
                (click)="requestClose()"
              >
                取消
              </button>
              <button nxPending type="submit" class="primary-button" [pending]="store.busy()">
                {{ store.busy() ? '新增中…' : '新增外部系統' }}
              </button>
            </footer>
          } @else {
            <nx-dialog-save-bar
              label="儲存外部系統"
              [dirty]="editor.dirty()"
              [saving]="store.busy()"
              (revert)="revert()"
            />
          }
        </form>
      }
    </dialog>
    <nx-confirm-dialog />`,
})
export class ApiClientDialog {
  readonly id = uniqueId('nx-api-client-dialog');
  readonly store = inject(ApiClientsStore);
  readonly nameMax = NAME_MAX;
  readonly contactMax = CONTACT_MAX;
  /** 新增成功：頁面接著顯示只出現一次的金鑰。 */
  readonly created = output<IssuedApiClientKeyDto>();
  /** 編輯時版本已過期：對話框已關閉，由頁面說明並重讀。 */
  readonly stale = output<void>();
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  private readonly nameInput = viewChild<ElementRef<HTMLInputElement>>('name');
  private readonly confirmation = viewChild.required(ConfirmDialog);
  readonly editor = new DraftEditor<Draft, Target>(sameDraft);
  readonly error = signal('');
  readonly nameError = signal('');

  create() {
    this.show({ client: null }, { name: '', contact: '' });
  }
  edit(client: ApiClientDto) {
    this.show({ client }, { name: client.name, contact: client.contact ?? '' });
  }
  private show(target: Target, draft: Draft) {
    this.error.set('');
    this.nameError.set('');
    this.editor.open(target, draft);
    this.dialog().nativeElement.showModal();
  }
  update<K extends keyof Draft>(key: K, value: Draft[K]) {
    this.editor.update(key, value);
    if (key === 'name') this.nameError.set('');
  }
  revert() {
    this.editor.revert();
    this.error.set('');
    this.nameError.set('');
  }
  cancel(event: Event) {
    event.preventDefault();
    void this.requestClose();
  }
  async requestClose() {
    if (this.store.busy()) return;
    const message = this.editor.current()?.client
      ? '變更尚未儲存，關閉後會恢復為已儲存的內容。'
      : '尚未新增外部系統，關閉後會捨棄已填寫的內容。';
    if (await this.editor.confirmDiscard(this.confirmation(), message))
      this.dialog().nativeElement.close();
  }

  async save(event: Event) {
    event.preventDefault();
    const editor = this.editor.current();
    if (!editor || this.store.busy() || (editor.client && !this.editor.dirty())) return;
    const name = editor.draft.name.trim();
    if (!name) {
      this.nameError.set('請輸入系統名稱。');
      this.nameInput()?.nativeElement.focus();
      return;
    }
    const fields = { name, contact: editor.draft.contact.trim() || null };
    this.error.set('');
    if (editor.client) {
      const outcome = await this.store.edit(editor.client, fields);
      if (!outcome.ok && !outcome.stale) return this.error.set(outcome.error);
      // 版本已過期時也關掉：再送也會被拒，要從重讀後的資料重新開始。
      this.close();
      if (!outcome.ok) this.stale.emit();
      return;
    }
    const outcome = await this.store.create(fields);
    if (!outcome.ok) return this.error.set(outcome.error);
    this.close();
    this.created.emit(outcome.value);
  }
  private close() {
    this.editor.markSaved();
    this.dialog().nativeElement.close();
  }
}
