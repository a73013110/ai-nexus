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
import { nameInitial } from '@shared/browser/format';
import { uniqueId } from '@shared/browser/unique-id';
import { CompactDialog } from '@shared/ui/compact-dialog';
import { ConfirmDialog } from '@shared/ui/confirm-dialog';
import { DialogHeader } from '@shared/ui/dialog-header';
import { DialogSaveBar } from '@shared/ui/dialog-save-bar';
import { Field } from '@shared/ui/field';
import { Notice } from '@shared/ui/notice';
import { PendingButton } from '@shared/ui/pending-button';
import { directorySearch } from '@features/work/collaboration/directory-search';
import { PersonPicker } from '@features/work/collaboration/person-picker';
import { AttendanceApi } from '../attendance-api';
import { attendanceProblem } from '../attendance-presentation';

/** 與後端 `RosterExclusion.NoteMaxLength` 相同；伺服器仍會再檢查。 */
const NOTE_MAX = 200;

interface Person {
  id: string;
  displayName: string;
  account: string;
}
interface Editor {
  /** 修改備註時人已固定，不能更換。 */
  editing: boolean;
  person: Person | null;
  note: string;
}

/**
 * 把一個人設為不列入出勤，或修改他的備註。人員以目錄搜尋（只有使用者能標；只有姓名的持有人綁上帳號後再標），
 * 選好後換成 `.form-choice`＋「更換」，與指派持有人同一套。
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
              <section class="form-section" role="group" [attr.aria-labelledby]="id + '-person'">
                <header class="form-section-head">
                  <h3 [id]="id + '-person'">人員</h3>
                  <p class="form-note">HR 看全公司出勤時不列出；本人與主管照常看得到。</p>
                </header>
                <div class="form-section-body">
                  @if (e.person; as p) {
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
                  @if (personError()) {
                    <p class="form-note field-error">{{ personError() }}</p>
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
  protected readonly initial = nameInitial;
  /** 儲存成功，名單要重讀。 */
  readonly saved = output<void>();
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('exclusionDialog');
  private readonly picker = viewChild(PersonPicker);
  private readonly confirmation = viewChild.required(ConfirmDialog);
  readonly editor = signal<Editor | null>(null);
  private readonly opened = signal<Editor | null>(null);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly personError = signal('');
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
      (editor.person?.id !== opened.person?.id || editor.note.trim() !== opened.note.trim())
    );
  });

  create() {
    this.show({ editing: false, person: null, note: '' });
  }
  edit(person: Person, note: string | null | undefined) {
    this.show({ editing: true, person, note: note ?? '' });
  }
  private show(editor: Editor) {
    this.error.set('');
    this.personError.set('');
    this.people.clear();
    this.editor.set(editor);
    this.opened.set(editor);
    this.dialog().nativeElement.showModal();
  }
  update(change: Partial<Editor>) {
    this.editor.update((editor) => (editor ? { ...editor, ...change } : null));
  }
  choose(user: DirectoryUserDto) {
    this.update({ person: { id: user.id, displayName: user.displayName, account: user.account } });
    this.personError.set('');
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
    if (!editor.person) {
      this.personError.set('請選擇人員。');
      this.picker()?.focus();
      return;
    }
    this.saving.set(true);
    this.error.set('');
    try {
      await this.api.exclude(editor.person.id, { note: editor.note.trim() || null });
      this.opened.set(editor);
      this.dialog().nativeElement.close();
      this.saved.emit();
    } catch (error) {
      this.error.set(attendanceProblem(error));
    } finally {
      this.saving.set(false);
    }
  }
}
