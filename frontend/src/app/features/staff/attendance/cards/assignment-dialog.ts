import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import type { CardAssignmentDto, DirectoryUserDto } from '@core/api/schema';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { formatDateTimeInput, parseDateTimeInput } from '@shared/browser/format';
import { uniqueId } from '@shared/browser/unique-id';
import { CompactDialog } from '@shared/ui/compact-dialog';
import { ConfirmDialog } from '@shared/ui/confirm-dialog';
import { DateTimePicker } from '@shared/ui/date-time-picker';
import { DialogHeader } from '@shared/ui/dialog-header';
import { Field } from '@shared/ui/field';
import { Icon } from '@shared/ui/icon';
import { Notice } from '@shared/ui/notice';
import { PendingButton } from '@shared/ui/pending-button';
import { SearchField } from '@shared/ui/search-field';
import { directorySearch } from '@features/work/collaboration/directory-search';
import { AttendanceCardsStore } from './attendance-cards-store';

/** 持有人：使用者（有帳號）或只有姓名（新進員工尚無帳號），至少一個。 */
interface Holder {
  id: string;
  displayName: string;
  account: string;
}
export interface AssignmentDraft {
  user: Holder | null;
  holderName: string;
  /** 台北牆上時間 `YYYY-MM-DDTHH:mm:ss`；送出時轉成 UTC。 */
  startsAt: string;
  endsAt: string;
  note: string;
}
interface Editor {
  /** 修改既有期間時的 ID；新增時為 null。 */
  id: string | null;
  cardId: string | null;
  cardNumber: string;
  title: string;
  /** 「結束持有」：以修改送出，提示文字不同。 */
  ending: boolean;
  draft: AssignmentDraft;
}

/** 與後端 `CardAssignment.HolderNameMaxLength`、`NoteMaxLength` 相同。 */
const NAME_MAX = 120;
const NOTE_MAX = 200;
const wall = (iso: string | null | undefined) => (iso ? formatDateTimeInput(new Date(iso)) : '');
const instant = (value: string) => {
  const date = parseDateTimeInput(value);
  return Number.isFinite(date.getTime()) ? date.toISOString() : null;
};
const same = (a: AssignmentDraft, b: AssignmentDraft) =>
  a.user?.id === b.user?.id &&
  a.holderName.trim() === b.holderName.trim() &&
  a.startsAt === b.startsAt &&
  a.endsAt === b.endsAt &&
  a.note.trim() === b.note.trim();

/**
 * 新增、修改或結束一段持有期間 `[開始, 結束)`。新增時卡片已有人持有，伺服器回 409，
 * 由 store 找出目前持有人並在這裡確認接手；修改既有期間時重疊一律拒絕，不接手。
 */
@Component({
  selector: 'nx-assignment-dialog',
  imports: [
    CompactDialog,
    ConfirmDialog,
    DateTimePicker,
    DialogHeader,
    Field,
    Icon,
    Notice,
    PendingButton,
    SearchField,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<dialog
      nxCompactDialog
      #assignmentDialog
      class="platform-dialog"
      size="md"
      [busy]="store.busy()"
      (cancel)="cancel($event)"
    >
      @if (editor(); as e) {
        <form class="dialog-form" novalidate (submit)="save($event)">
          <nx-dialog-header
            [heading]="e.title"
            [detail]="'卡號 ' + e.cardNumber"
            closeLabel="關閉持有期間"
          />
          <div class="dialog-body">
            @if (error()) {
              <nx-notice tone="danger" [message]="error()" />
            }
            @if (!e.cardId) {
              <nx-notice tone="info" message="這個卡號尚未登錄，儲存時會先登錄為員工卡再指派。" />
            }
            <div class="form-sections">
              <section class="form-section" role="group" [attr.aria-labelledby]="id + '-holder'">
                <header class="form-section-head">
                  <h3 [id]="id + '-holder'">持有人</h3>
                  <p class="form-note">選擇使用者，或在尚無帳號時只填姓名；之後可再綁上帳號。</p>
                </header>
                <div class="form-section-body">
                  @if (e.draft.user; as user) {
                    <div class="assignment-user">
                      <span
                        ><strong>{{ user.displayName }}</strong
                        ><small>{{ user.account }}</small></span
                      >
                      <button
                        type="button"
                        class="icon-button"
                        [attr.aria-label]="'移除使用者 ' + user.displayName"
                        [disabled]="locked()"
                        (click)="update('user', null)"
                      >
                        <nx-icon name="close" />
                      </button>
                    </div>
                  } @else {
                    <label
                      >使用者<nx-search-field
                        label="尋找持有人帳號"
                        placeholder="輸入至少兩個字，搜尋帳號或姓名"
                        [value]="people.text()"
                        [busy]="people.loading()"
                        [disabled]="locked()"
                        (valueChange)="people.find($event)"
                    /></label>
                    @if (people.searchable()) {
                      <div class="person-picker-results" role="region" aria-label="符合的使用者">
                        @for (user of people.results(); track user.id) {
                          <button type="button" class="person-picker-user" (click)="choose(user)">
                            <span
                              ><strong>{{ user.displayName }}</strong
                              ><small>{{ user.account }}</small></span
                            ><nx-icon name="plus" />
                          </button>
                        } @empty {
                          <!-- 防抖等待與查詢途中還沒有答案，不先說「尚無符合」。 -->
                          @if (people.loading()) {
                            <p class="form-note" role="status">正在搜尋…</p>
                          } @else {
                            <p class="form-note">尚無符合帳號；新進員工可先只填姓名。</p>
                          }
                        }
                      </div>
                    }
                  }
                  <label class="form-field-short"
                    >姓名（尚無帳號時）<input
                      #name
                      nxField
                      autocomplete="off"
                      [attr.maxlength]="nameMax"
                      [attr.aria-invalid]="holderError() ? true : null"
                      [attr.aria-describedby]="holderError() ? id + '-holder-error' : null"
                      [value]="e.draft.holderName"
                      [readOnly]="locked()"
                      (input)="update('holderName', name.value)"
                  /></label>
                  @if (holderError()) {
                    <p class="form-note field-error" [id]="id + '-holder-error'">
                      {{ holderError() }}
                    </p>
                  }
                </div>
              </section>
              <section class="form-section" role="group" [attr.aria-labelledby]="id + '-period'">
                <header class="form-section-head">
                  <h3 [id]="id + '-period'">期間</h3>
                  <p class="form-note">
                    台北時間；結束留空表示持有中。開始時間之後的刷卡才歸戶給這位持有人。
                  </p>
                </header>
                <div class="form-section-body">
                  <nx-date-time-picker
                    label="開始時間"
                    [value]="e.draft.startsAt"
                    [disabled]="locked()"
                    (valueChange)="update('startsAt', $event)"
                  />
                  <nx-date-time-picker
                    label="結束時間"
                    [required]="false"
                    [min]="e.draft.startsAt"
                    [value]="e.draft.endsAt"
                    [disabled]="locked()"
                    (valueChange)="update('endsAt', $event)"
                  />
                  @if (periodError()) {
                    <p class="form-note field-error">{{ periodError() }}</p>
                  }
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
              [disabled]="!store.busy() && (session.testing() || (!!e.id && !dirty()))"
            >
              {{
                store.busy()
                  ? e.ending
                    ? '結束中…'
                    : e.id
                      ? '儲存中…'
                      : '指派中…'
                  : e.ending
                    ? '結束持有'
                    : e.id
                      ? '儲存'
                      : '指派'
              }}
            </button>
          </footer>
        </form>
      }
    </dialog>
    <nx-confirm-dialog />`,
  styles: `
    .field-error {
      color: var(--error);
    }
    .assignment-user {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: var(--p-space-2);
      padding: var(--p-space-2) var(--p-space-3);
      border: 1px solid var(--line);
      border-radius: var(--radius-block);
    }
    .assignment-user small {
      margin-inline-start: var(--p-space-2);
      color: var(--secondary);
    }
  `,
})
export class AssignmentDialog {
  readonly id = uniqueId('nx-assignment-dialog');
  readonly session = inject(WorkspaceSession);
  readonly store = inject(AttendanceCardsStore);
  readonly nameMax = NAME_MAX;
  readonly noteMax = NOTE_MAX;
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('assignmentDialog');
  private readonly confirmation = viewChild.required(ConfirmDialog);
  readonly editor = signal<Editor | null>(null);
  private readonly opened = signal<AssignmentDraft | null>(null);
  readonly error = signal('');
  readonly holderError = signal('');
  readonly periodError = signal('');
  readonly people = directorySearch(
    () => {
      const user = this.editor()?.draft.user;
      return user ? [user.id] : [];
    },
    { includeSelf: true },
  );
  readonly locked = computed(() => this.store.busy() || this.session.testing());
  readonly dirty = computed(() => {
    const editor = this.editor(),
      opened = this.opened();
    return !!editor && !!opened && !same(editor.draft, opened);
  });

  /** 新增；未歸戶卡號帶入第一筆未歸戶刷卡當起始時間，之後的刷卡才會全部歸戶。 */
  create(card: { id: string | null; cardNumber: string }, startsAt?: string) {
    this.show({
      id: null,
      cardId: card.id,
      cardNumber: card.cardNumber,
      title: '指派持有人',
      ending: false,
      draft: {
        user: null,
        holderName: '',
        startsAt: wall(startsAt ?? new Date().toISOString()),
        endsAt: '',
        note: '',
      },
    });
  }
  /** 修改；`ending` 時把結束時間預填為現在。 */
  edit(item: CardAssignmentDto, ending = false) {
    const draft: AssignmentDraft = {
      user: item.userId
        ? { id: item.userId, displayName: item.userDisplayName ?? '', account: '' }
        : null,
      holderName: item.holderName ?? '',
      startsAt: wall(item.startsAt),
      endsAt: wall(item.endsAt),
      note: item.note ?? '',
    };
    this.show(
      {
        id: item.id,
        cardId: item.cardId,
        cardNumber: item.cardNumber,
        title: ending ? '結束持有' : '編輯持有期間',
        ending,
        draft,
      },
      ending ? { ...draft, endsAt: wall(new Date().toISOString()) } : undefined,
    );
  }
  private show(editor: Editor, start?: AssignmentDraft) {
    this.error.set('');
    this.holderError.set('');
    this.periodError.set('');
    this.people.clear();
    this.opened.set(editor.draft);
    this.editor.set(start ? { ...editor, draft: start } : editor);
    this.dialog().nativeElement.showModal();
  }
  update<K extends keyof AssignmentDraft>(key: K, value: AssignmentDraft[K]) {
    this.editor.update((editor) =>
      editor ? { ...editor, draft: { ...editor.draft, [key]: value } } : null,
    );
    if (key === 'user' || key === 'holderName') this.holderError.set('');
    if (key === 'startsAt' || key === 'endsAt') this.periodError.set('');
  }
  choose(user: DirectoryUserDto) {
    this.update('user', { id: user.id, displayName: user.displayName, account: user.account });
    this.people.clear();
  }
  cancel(event: Event) {
    event.preventDefault();
    void this.requestClose();
  }
  async requestClose() {
    if (this.store.busy()) return;
    if (
      this.dirty() &&
      !(await this.confirmation().ask({
        title: '放棄未儲存的變更？',
        message: '持有期間尚未儲存，關閉後會捨棄已填寫的內容。',
        confirm: '放棄變更',
      }))
    )
      return;
    this.dialog().nativeElement.close();
  }

  /** 本機先檢查必填與期間，伺服器仍會以相同規則再檢查一次。 */
  private validate(draft: AssignmentDraft) {
    this.holderError.set('');
    this.periodError.set('');
    const name = draft.holderName.trim();
    if (!draft.user && !name) this.holderError.set('請選擇使用者或填寫姓名。');
    else if (name.length > NAME_MAX) this.holderError.set(`姓名最多 ${NAME_MAX} 字。`);
    const startsAt = instant(draft.startsAt);
    const endsAt = draft.endsAt ? instant(draft.endsAt) : null;
    if (!startsAt) this.periodError.set('請填寫有效的開始時間。');
    else if (draft.endsAt && (!endsAt || endsAt <= startsAt))
      this.periodError.set('結束時間必須晚於開始時間。');
    if (this.holderError() || this.periodError() || !startsAt) return null;
    return {
      userId: draft.user?.id ?? null,
      holderName: name || null,
      startsAt,
      endsAt,
      note: draft.note.trim().slice(0, NOTE_MAX) || null,
    };
  }

  async save(event: Event) {
    event.preventDefault();
    const editor = this.editor();
    if (!editor || this.locked() || (editor.id && !this.dirty())) return;
    const body = this.validate(editor.draft);
    if (!body) return;
    this.error.set('');
    const outcome = editor.id
      ? await this.store.updateAssignment(
          editor.id,
          body,
          editor.ending ? '已結束持有' : '已儲存持有期間',
        )
      : await this.store.assign(
          { ...body, cardId: editor.cardId, cardNumber: editor.cardNumber },
          (holder) =>
            this.confirmation().ask({
              title: '由新持有人接手？',
              message: `卡號 ${editor.cardNumber} 在開始時間仍由 ${holder} 持有。接手後，${holder} 的持有期間會在新期間開始時結束。`,
              confirm: '接手',
            }),
        );
    if (!outcome.saved) {
      this.error.set(outcome.error);
      return;
    }
    this.opened.set(editor.draft);
    this.dialog().nativeElement.close();
  }
}
