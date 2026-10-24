import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  Injector,
  afterNextRender,
  computed,
  inject,
  output,
  signal,
  viewChild,
} from '@angular/core';
import type { DirectoryUserDto } from '@core/api/schema';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { ApiError } from '@core/errors/safe-errors';
import { nameInitial } from '@shared/browser/format';
import { uniqueId } from '@shared/browser/unique-id';
import { CompactDialog } from '@shared/ui/compact-dialog';
import { ConfirmDialog } from '@shared/ui/confirm-dialog';
import { DialogHeader } from '@shared/ui/dialog-header';
import { DialogSaveBar } from '@shared/ui/dialog-save-bar';
import { Field } from '@shared/ui/field';
import { Notice } from '@shared/ui/notice';
import { PendingButton } from '@shared/ui/pending-button';
import { ViewSwitch } from '@shared/ui/view-switch';
import { directorySearch } from '@features/work/collaboration/directory-search';
import { PersonPicker } from '@features/work/collaboration/person-picker';
import { AttendanceApi, type ExclusionTarget } from '../attendance-api';
import { attendanceProblem } from '../attendance-presentation';

/** 與後端 `RosterExclusion.NoteMaxLength` 相同；伺服器仍會再檢查。 */
const NOTE_MAX = 200;
/** 與後端 `Card.CardNumberMaxLength` 相同。 */
const CARD_MAX = 32;

interface Person {
  id: string;
  displayName: string;
  account: string;
}
/** 修改備註時開啟的對象：使用者，或卡號（`displayName` 是名單上的名稱）。 */
export type ExclusionSubject =
  | { userId: string; displayName: string; account: string }
  | { cardNumber: string; displayName: string };
interface Editor {
  /** 修改備註時對象已固定，不能更換。 */
  editing: boolean;
  kind: 'user' | 'card';
  person: Person | null;
  cardNumber: string;
  /** 修改時卡號列的名稱（姓名持有人或「卡號 …」）。 */
  cardName: string;
  note: string;
}

const KINDS = [
  { value: 'user', label: '使用者' },
  { value: 'card', label: '卡號' },
];

/**
 * 設為不列入出勤，或修改原因。對象是使用者（目錄搜尋，選好後換成 `.form-choice`＋「更換」，與指派持有人同一套），
 * 或卡號：沒有帳號的資料（只有姓名的持有人、未歸戶刷卡）依附在卡號上，同名同姓不會一起被隱藏。
 */
@Component({
  selector: 'nx-exclusion-dialog',
  imports: [
    CompactDialog,
    ConfirmDialog,
    DialogHeader,
    DialogSaveBar,
    Field,
    Notice,
    PendingButton,
    PersonPicker,
    ViewSwitch,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<dialog
      nxCompactDialog
      #exclusionDialog
      class="platform-dialog"
      size="md"
      [busy]="saving()"
      (cancel)="cancel($event)"
    >
      @if (editor(); as e) {
        <form class="dialog-form" novalidate (submit)="save($event)">
          <nx-dialog-header
            [heading]="e.editing ? '編輯不列入出勤' : '設為不列入出勤'"
            closeLabel="關閉不列入出勤"
          />
          <div class="dialog-body">
            @if (error()) {
              <nx-notice tone="danger" [message]="error()" />
            }
            <div class="form-sections">
              <section class="form-section" role="group" [attr.aria-labelledby]="id + '-target'">
                <header class="form-section-head">
                  <h3 [id]="id + '-target'">對象</h3>
                  <p class="form-note">HR 看全公司出勤時不列出；本人與主管照常看得到。</p>
                </header>
                <div class="form-section-body">
                  @if (!e.editing) {
                    <nx-view-switch
                      class="exclusion-kind"
                      label="對象種類"
                      appearance="segment"
                      [options]="kinds"
                      [value]="e.kind"
                      [disabled]="locked()"
                      (valueChange)="switchKind($any($event))"
                    />
                  }
                  @if (e.kind === 'card') {
                    @if (e.editing) {
                      <div class="form-choice">
                        <span class="avatar is-name" aria-hidden="true">{{
                          initial(e.cardName)
                        }}</span>
                        <span class="form-choice-text"
                          ><strong>{{ e.cardName }}</strong
                          ><small>卡號 {{ e.cardNumber }}</small></span
                        >
                      </div>
                    } @else {
                      <label
                        >卡號<input
                          #card
                          nxField
                          class="exclusion-card"
                          autocomplete="off"
                          inputmode="text"
                          [attr.maxlength]="cardMax"
                          [attr.aria-invalid]="!!targetError() || null"
                          [value]="e.cardNumber"
                          [readOnly]="locked()"
                          (input)="update({ cardNumber: card.value }); targetError.set('')"
                      /></label>
                      <p class="form-note">
                        只有姓名的持有人與未歸戶卡號依卡號設定；卡上綁了帳號的期間不受影響。
                      </p>
                    }
                  } @else if (e.person; as p) {
                    <div class="form-choice">
                      <span class="avatar" aria-hidden="true">{{ initial(p.displayName) }}</span>
                      <span class="form-choice-text"
                        ><strong>{{ p.displayName }}</strong
                        ><small>{{ p.account }}</small></span
                      >
                      @if (!e.editing) {
                        <button
                          type="button"
                          class="secondary-button"
                          [disabled]="locked()"
                          (click)="change()"
                        >
                          更換
                        </button>
                      }
                    </div>
                  } @else {
                    <nx-person-picker
                      label="尋找人員"
                      [search]="people"
                      [disabled]="locked()"
                      (picked)="choose($event)"
                    />
                  }
                  @if (targetError()) {
                    <p class="form-note field-error">{{ targetError() }}</p>
                  }
                </div>
              </section>
              <section class="form-section" role="group" [attr.aria-labelledby]="id + '-note'">
                <header class="form-section-head">
                  <h3 [id]="id + '-note'">備註</h3>
                </header>
                <div class="form-section-body">
                  <label
                    >原因（選填）<input
                      #note
                      nxField
                      autocomplete="off"
                      [attr.maxlength]="noteMax"
                      [value]="e.note"
                      [readOnly]="locked()"
                      (input)="update({ note: note.value })"
                  /></label>
                </div>
              </section>
            </div>
          </div>
          @if (!e.editing) {
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
                {{ saving() ? '設定中…' : '設為不列入' }}
              </button>
            </footer>
          } @else {
            <nx-dialog-save-bar
              label="儲存備註"
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
export class ExclusionDialog {
  readonly id = uniqueId('nx-exclusion-dialog');
  readonly session = inject(WorkspaceSession);
  private readonly api = inject(AttendanceApi);
  private readonly injector = inject(Injector);
  readonly noteMax = NOTE_MAX;
  readonly cardMax = CARD_MAX;
  readonly kinds = KINDS;
  protected readonly initial = nameInitial;
  /** 儲存成功，名單要重讀。 */
  readonly saved = output<void>();
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('exclusionDialog');
  private readonly picker = viewChild(PersonPicker);
  private readonly cardInput = viewChild<ElementRef<HTMLInputElement>>('card');
  private readonly confirmation = viewChild.required(ConfirmDialog);
  readonly editor = signal<Editor | null>(null);
  private readonly opened = signal<Editor | null>(null);
  readonly saving = signal(false);
  readonly error = signal('');
  /** 對象（人員或卡號）的欄位錯誤：沒選、找不到卡號或訪客卡。 */
  readonly targetError = signal('');
  readonly people = directorySearch(
    () => {
      const person = this.editor()?.person;
      return person ? [person.id] : [];
    },
    { includeSelf: true },
  );
  readonly locked = computed(() => this.saving() || this.session.testing());
  readonly dirty = computed(() => {
    const editor = this.editor(),
      opened = this.opened();
    return (
      !!editor &&
      !!opened &&
      (editor.kind !== opened.kind ||
        editor.person?.id !== opened.person?.id ||
        editor.cardNumber.trim() !== opened.cardNumber.trim() ||
        editor.note.trim() !== opened.note.trim())
    );
  });

  create() {
    this.show({
      editing: false,
      kind: 'user',
      person: null,
      cardNumber: '',
      cardName: '',
      note: '',
    });
  }
  edit(subject: ExclusionSubject, note: string | null | undefined) {
    this.show({
      editing: true,
      kind: 'userId' in subject ? 'user' : 'card',
      person:
        'userId' in subject
          ? { id: subject.userId, displayName: subject.displayName, account: subject.account }
          : null,
      cardNumber: 'cardNumber' in subject ? subject.cardNumber : '',
      cardName: subject.displayName,
      note: note ?? '',
    });
  }
  private show(editor: Editor) {
    this.error.set('');
    this.targetError.set('');
    this.people.clear();
    this.editor.set(editor);
    this.opened.set(editor);
    this.dialog().nativeElement.showModal();
  }
  update(change: Partial<Editor>) {
    this.editor.update((editor) => (editor ? { ...editor, ...change } : null));
  }
  switchKind(kind: Editor['kind']) {
    this.update({ kind });
    this.targetError.set('');
  }
  choose(user: DirectoryUserDto) {
    this.update({ person: { id: user.id, displayName: user.displayName, account: user.account } });
    this.targetError.set('');
    this.people.clear();
  }
  /** 清掉人員回到選擇器，焦點交給搜尋框（按下的按鈕會被移除）。 */
  change() {
    this.update({ person: null });
    afterNextRender(() => this.picker()?.focus(), { injector: this.injector });
  }
  revert() {
    this.editor.set(this.opened());
    this.error.set('');
  }
  cancel(event: Event) {
    event.preventDefault();
    void this.requestClose();
  }
  async requestClose() {
    if (this.saving()) return;
    if (
      this.dirty() &&
      !(await this.confirmation().ask({
        title: '放棄未儲存的變更？',
        message: '尚未儲存，關閉後會捨棄已填寫的內容。',
        confirm: '放棄變更',
      }))
    )
      return;
    this.dialog().nativeElement.close();
  }

  async save(event: Event) {
    event.preventDefault();
    const editor = this.editor();
    if (!editor || this.locked() || (editor.editing && !this.dirty())) return;
    const target = this.target(editor);
    if (!target) return;
    this.saving.set(true);
    this.error.set('');
    try {
      await this.api.exclude(target, { note: editor.note.trim() || null });
      this.opened.set(editor);
      this.dialog().nativeElement.close();
      this.saved.emit();
    } catch (error) {
      // 卡號是手打的：找不到或訪客卡標在欄位上，不用通用的「請重新整理」。
      if (error instanceof ApiError && error.code === 'attendance_card_not_found')
        this.targetError.set('卡號對應與刷卡紀錄都找不到這個卡號。');
      else if (error instanceof ApiError && error.code === 'attendance_card_visitor')
        this.targetError.set('訪客卡本來就不列入出勤。');
      else this.error.set(attendanceProblem(error));
    } finally {
      this.saving.set(false);
    }
  }

  /** 送出的對象；沒選人或沒填卡號時標出欄位錯誤並把焦點移過去。 */
  private target(editor: Editor): ExclusionTarget | null {
    if (editor.kind === 'card') {
      const cardNumber = editor.cardNumber.trim();
      if (cardNumber) return { cardNumber };
      this.targetError.set('請填寫卡號。');
      this.cardInput()?.nativeElement.focus();
      return null;
    }
    if (editor.person) return { userId: editor.person.id };
    this.targetError.set('請選擇人員。');
    this.picker()?.focus();
    return null;
  }
}
