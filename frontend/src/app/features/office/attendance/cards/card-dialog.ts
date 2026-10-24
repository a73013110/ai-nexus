import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  inject,
  output,
  signal,
  viewChild,
} from '@angular/core';
import type { CardDto } from '@core/api/schema';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { uniqueId } from '@shared/browser/unique-id';
import { CompactDialog } from '@shared/ui/compact-dialog';
import { ConfirmDialog } from '@shared/ui/confirm-dialog';
import { DialogHeader } from '@shared/ui/dialog-header';
import { DialogSaveBar } from '@shared/ui/dialog-save-bar';
import { Field } from '@shared/ui/field';
import { Notice } from '@shared/ui/notice';
import { PendingButton } from '@shared/ui/pending-button';
import { ViewSwitch } from '@shared/ui/view-switch';
import { AttendanceCardsStore } from './attendance-cards-store';
import { CARD_KINDS } from '../attendance-presentation';

export interface CardDraft {
  cardNumber: string;
  kind: string;
  note: string;
}
interface Editor {
  id: string | null;
  title: string;
  draft: CardDraft;
}

/** 與後端 `Card.CardNumberMaxLength`、`Card.NoteMaxLength` 相同；伺服器仍會再檢查。 */
const NUMBER_MAX = 32;
const NOTE_MAX = 200;
const sameDraft = (a: CardDraft, b: CardDraft) =>
  a.cardNumber.trim() === b.cardNumber.trim() &&
  a.kind === b.kind &&
  a.note.trim() === b.note.trim();

/**
 * 登錄或修改一張卡。卡號去掉前後空白後存入、不補零；已有持有期間的卡不能改成訪客卡（伺服器回 `attendance_card_has_assignments`）。
 */
@Component({
  selector: 'nx-card-dialog',
  imports: [
    CompactDialog,
    ConfirmDialog,
    DialogHeader,
    DialogSaveBar,
    Field,
    Notice,
    PendingButton,
    ViewSwitch,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<dialog
      nxCompactDialog
      #cardDialog
      class="platform-dialog"
      size="md"
      [busy]="store.busy()"
      (cancel)="cancel($event)"
    >
      @if (editor(); as e) {
        <form class="dialog-form" novalidate (submit)="save($event)">
          <nx-dialog-header [heading]="e.title" closeLabel="關閉卡片編輯" />
          <div class="dialog-body">
            @if (error()) {
              <nx-notice tone="danger" [message]="error()" />
            }
            <div class="form-sections">
              <section class="form-section" role="group" [attr.aria-labelledby]="id + '-basics'">
                <header class="form-section-head">
                  <h3 [id]="id + '-basics'">卡片</h3>
                  <p class="form-note">卡號與打卡機輸出的相同，不補零；只有員工卡會歸戶到人。</p>
                </header>
                <div class="form-section-body">
                  <label class="form-field-short"
                    >卡號<input
                      #number
                      nxField
                      class="card-number-input"
                      required
                      autocomplete="off"
                      [attr.maxlength]="numberMax"
                      [attr.aria-invalid]="numberError() ? true : null"
                      [attr.aria-describedby]="numberError() ? id + '-error' : null"
                      [value]="e.draft.cardNumber"
                      [readOnly]="locked()"
                      (input)="update('cardNumber', number.value)"
                  /></label>
                  @if (numberError()) {
                    <p class="form-note field-error" [id]="id + '-error'">{{ numberError() }}</p>
                  }
                  <div class="ui-field">
                    <span aria-hidden="true">種類</span>
                    <nx-view-switch
                      label="卡片種類"
                      appearance="segment"
                      [options]="kinds"
                      [value]="e.draft.kind"
                      [disabled]="locked()"
                      (valueChange)="update('kind', $event)"
                    />
                  </div>
                  <label
                    >備註<input
                      #note
                      nxField
                      autocomplete="off"
                      [attr.maxlength]="noteMax"
                      [value]="e.draft.note"
                      [readOnly]="locked()"
                      (input)="update('note', note.value)"
                  /></label>
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
                [attr.aria-disabled]="store.busy() || null"
                (click)="requestClose()"
              >
                取消
              </button>
              <button
                nxPending
                type="submit"
                class="primary-button"
                [pending]="store.busy()"
                [disabled]="session.testing()"
              >
                {{ store.busy() ? '登錄中…' : '登錄卡片' }}
              </button>
            </footer>
          } @else {
            <nx-dialog-save-bar
              label="儲存卡片"
              [dirty]="dirty()"
              [saving]="store.busy()"
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
export class CardDialog {
  readonly id = uniqueId('nx-card-dialog');
  readonly session = inject(WorkspaceSession);
  readonly store = inject(AttendanceCardsStore);
  readonly kinds = CARD_KINDS;
  readonly numberMax = NUMBER_MAX;
  readonly noteMax = NOTE_MAX;
  /** 新登錄的卡片，頁面用來直接開啟它的詳情。 */
  readonly created = output<CardDto>();
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('cardDialog');
  private readonly numberInput = viewChild<ElementRef<HTMLInputElement>>('number');
  private readonly confirmation = viewChild.required(ConfirmDialog);
  readonly editor = signal<Editor | null>(null);
  private readonly opened = signal<CardDraft | null>(null);
  readonly error = signal('');
  readonly numberError = signal('');
  readonly locked = computed(() => this.store.busy() || this.session.testing());
  readonly dirty = computed(() => {
    const editor = this.editor(),
      opened = this.opened();
    return !!editor && !!opened && !sameDraft(editor.draft, opened);
  });

  create(cardNumber = '') {
    this.show(null, '登錄卡片', { cardNumber, kind: 'employee', note: '' });
  }
  edit(card: CardDto) {
    this.show(card.id, `編輯卡片：${card.cardNumber}`, {
      cardNumber: card.cardNumber,
      kind: card.kind,
      note: card.note ?? '',
    });
  }
  private show(id: string | null, title: string, draft: CardDraft) {
    this.error.set('');
    this.numberError.set('');
    this.editor.set({ id, title, draft });
    this.opened.set(draft);
    this.dialog().nativeElement.showModal();
  }
  update<K extends keyof CardDraft>(key: K, value: CardDraft[K]) {
    this.editor.update((editor) =>
      editor ? { ...editor, draft: { ...editor.draft, [key]: value } } : null,
    );
    if (key === 'cardNumber') this.numberError.set('');
  }
  revert() {
    const opened = this.opened();
    this.editor.update((editor) => (editor && opened ? { ...editor, draft: opened } : editor));
    this.error.set('');
    this.numberError.set('');
  }
  cancel(event: Event) {
    event.preventDefault();
    void this.requestClose();
  }
  async requestClose() {
    if (this.store.busy()) return;
    const editor = this.editor();
    if (
      editor &&
      this.dirty() &&
      !(await this.confirmation().ask({
        title: '放棄未儲存的變更？',
        message: editor.id
          ? `「${editor.title}」的變更尚未儲存，關閉後會恢復為已儲存的內容。`
          : '尚未登錄卡片，關閉後會捨棄已填寫的內容。',
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
    const cardNumber = draft.cardNumber.trim();
    if (!cardNumber || cardNumber.length > NUMBER_MAX) {
      this.numberError.set(`請輸入 ${NUMBER_MAX} 字元內的卡號。`);
      this.numberInput()?.nativeElement.focus();
      return;
    }
    this.error.set('');
    const outcome = await this.store.saveCard(editor.id, {
      cardNumber,
      kind: draft.kind,
      note: draft.note.trim() || null,
    });
    if (!outcome.saved) {
      this.error.set(outcome.error);
      return;
    }
    this.opened.set(draft);
    this.dialog().nativeElement.close();
    if (!editor.id && outcome.value) this.created.emit(outcome.value);
  }
}
