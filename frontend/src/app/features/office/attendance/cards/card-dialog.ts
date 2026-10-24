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
import { Checkbox } from '@shared/ui/checkbox';
import { CompactDialog } from '@shared/ui/compact-dialog';
import { ConfirmDialog } from '@shared/ui/confirm-dialog';
import { DialogHeader } from '@shared/ui/dialog-header';
import { DialogSaveBar } from '@shared/ui/dialog-save-bar';
import { Field } from '@shared/ui/field';
import { Notice } from '@shared/ui/notice';
import { PendingButton } from '@shared/ui/pending-button';
import { AttendanceCardsStore } from './attendance-cards-store';

export interface CardDraft {
  cardNumber: string;
  hidden: boolean;
  note: string;
}
/** 新增（`card` 為 null）或修改一張卡的備註；卡號建立後不改，隱藏在抽屜裡直接切換。 */
interface Editor {
  card: CardDto | null;
  draft: CardDraft;
}

/** 與後端 `Card.CardNumberMaxLength`、`Card.NoteMaxLength` 相同；伺服器仍會再檢查。 */
const NUMBER_MAX = 32;
const NOTE_MAX = 200;
const sameDraft = (a: CardDraft, b: CardDraft) =>
  a.cardNumber.trim() === b.cardNumber.trim() &&
  a.hidden === b.hidden &&
  a.note.trim() === b.note.trim();

/**
 * 新增卡號或修改備註。打卡檔出現過的卡號匯入時已自動建立，這裡只用在還沒刷過卡的新卡（例如先發卡再報到）；
 * 卡號去掉前後空白後存入、不補零，建立後不改（刷卡以卡號字串對應）。
 */
@Component({
  selector: 'nx-card-dialog',
  imports: [
    Checkbox,
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
      #cardDialog
      class="platform-dialog"
      size="md"
      [busy]="store.busy()"
      (cancel)="cancel($event)"
    >
      @if (editor(); as e) {
        <form class="dialog-form" novalidate (submit)="save($event)">
          <nx-dialog-header
            [heading]="e.card ? '編輯備註' : '新增卡號'"
            [detail]="e.card ? '卡號 ' + e.card.cardNumber : ''"
            closeLabel="關閉卡號編輯"
          />
          <div class="dialog-body">
            @if (error()) {
              <nx-notice tone="danger" [message]="error()" />
            }
            @if (e.card) {
              <label
                >備註（選填）<input
                  #note
                  nxField
                  autocomplete="off"
                  [attr.maxlength]="noteMax"
                  [value]="e.draft.note"
                  [readOnly]="locked()"
                  (input)="update('note', note.value)"
              /></label>
            } @else {
              <div class="form-sections">
                <section class="form-section" role="group" [attr.aria-labelledby]="id + '-card'">
                  <header class="form-section-head">
                    <h3 [id]="id + '-card'">卡號</h3>
                    <p class="form-note">
                      刷過的卡號匯入時會自動出現；這裡用來先建立還沒刷過的新卡。
                    </p>
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
                    <label
                      >備註（選填）<input
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
                <section class="form-section" role="group" [attr.aria-labelledby]="id + '-records'">
                  <header class="form-section-head">
                    <h3 [id]="id + '-records'">出勤紀錄</h3>
                  </header>
                  <div class="form-section-body">
                    <nx-checkbox
                      label="在出勤紀錄隱藏"
                      description="例如訪客卡、公務卡；HR 看全公司出勤時不列出，本人與主管照常看得到。"
                      [checked]="e.draft.hidden"
                      [disabled]="locked()"
                      (checkedChange)="update('hidden', $event)"
                    />
                  </div>
                </section>
              </div>
            }
          </div>
          @if (!e.card) {
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
                {{ store.busy() ? '新增中…' : '新增卡號' }}
              </button>
            </footer>
          } @else {
            <nx-dialog-save-bar
              label="儲存備註"
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
  readonly numberMax = NUMBER_MAX;
  readonly noteMax = NOTE_MAX;
  /** 新增的卡片，頁面用來直接開啟它的詳情。 */
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

  create() {
    this.show({ card: null, draft: { cardNumber: '', hidden: false, note: '' } });
  }
  editNote(card: CardDto) {
    this.show({
      card,
      draft: { cardNumber: card.cardNumber, hidden: card.hiddenFromRecords, note: card.note ?? '' },
    });
  }
  private show(editor: Editor) {
    this.error.set('');
    this.numberError.set('');
    this.editor.set(editor);
    this.opened.set(editor.draft);
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
        message: editor.card
          ? '備註的變更尚未儲存，關閉後會恢復為已儲存的內容。'
          : '尚未新增卡號，關閉後會捨棄已填寫的內容。',
        confirm: '放棄變更',
      }))
    )
      return;
    this.dialog().nativeElement.close();
  }

  async save(event: Event) {
    event.preventDefault();
    const editor = this.editor();
    if (!editor || this.locked() || (editor.card && !this.dirty())) return;
    const draft = editor.draft;
    const note = draft.note.trim() || null;
    this.error.set('');
    if (editor.card) {
      const outcome = await this.store.saveNote(editor.card, note);
      if (!outcome.saved) return this.error.set(outcome.error);
      this.opened.set(draft);
      return this.dialog().nativeElement.close();
    }
    const cardNumber = draft.cardNumber.trim();
    if (!cardNumber || cardNumber.length > NUMBER_MAX) {
      this.numberError.set(`請輸入 ${NUMBER_MAX} 字元內的卡號。`);
      this.numberInput()?.nativeElement.focus();
      return;
    }
    const outcome = await this.store.createCard({
      cardNumber,
      hiddenFromRecords: draft.hidden,
      note,
    });
    if (!outcome.saved) return this.error.set(outcome.error);
    this.opened.set(draft);
    this.dialog().nativeElement.close();
    this.created.emit(outcome.value);
  }
}
