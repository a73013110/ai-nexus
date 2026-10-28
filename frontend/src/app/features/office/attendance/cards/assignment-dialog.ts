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
import type { CardAssignmentDto, DirectoryUserDto } from '@core/api/schema';
import { formatDateTimeInput, nameInitial, parseDateTimeInput } from '@shared/browser/format';
import { uniqueId } from '@shared/browser/unique-id';
import { CompactDialog } from '@shared/ui/compact-dialog';
import { ConfirmDialog } from '@shared/ui/confirm-dialog';
import { DateTimePicker } from '@shared/ui/date-time-picker';
import { DialogHeader } from '@shared/ui/dialog-header';
import { Field } from '@shared/ui/field';
import { Notice } from '@shared/ui/notice';
import { PendingButton } from '@shared/ui/pending-button';
import { directorySearch } from '@features/work/collaboration/directory-search';
import { PersonPicker } from '@features/work/collaboration/person-picker';
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
  cardId: string;
  cardNumber: string;
  title: string;
  /** 「結束持有」：以修改送出，提示文字不同。 */
  ending: boolean;
  /** 新增時卡片目前的持有人：標題改為「轉給其他人」，接手時不再另外確認。 */
  transferFrom: string | null;
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
 * 由 store 找出目前持有人並在這裡確認接手（從「轉給其他人」開啟、對象就是畫面上寫明的人時不再問）；
 * 修改既有期間時重疊一律拒絕，不接手。
 * 持有人是使用者或只有姓名二擇一：選擇器找不到帳號時，最後一列「使用姓名」記下輸入的姓名。
 */
@Component({
  selector: 'nx-assignment-dialog',
  imports: [
    CompactDialog,
    ConfirmDialog,
    DateTimePicker,
    DialogHeader,
    Field,
    Notice,
    PendingButton,
    PersonPicker,
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
            <div class="form-sections">
              <section class="form-section" role="group" [attr.aria-labelledby]="id + '-holder'">
                <header class="form-section-head">
                  <h3 [id]="id + '-holder'">持有人</h3>
                </header>
                <div class="form-section-body">
                  @if (holder(); as h) {
                    <div class="form-choice">
                      <span class="avatar" [class.is-name]="!h.bound" aria-hidden="true">{{
                        initial(h.name)
                      }}</span>
                      <span class="form-choice-text"
                        ><strong>{{ h.name }}</strong
                        ><small>{{ h.detail }}</small></span
                      >
                      @if (!e.ending) {
                        <button
                          type="button"
                          class="secondary-button"
                          [disabled]="store.busy()"
                          (click)="changeHolder()"
                        >
                          {{ h.bound ? '更換' : '綁定帳號' }}
                        </button>
                      }
                    </div>
                  } @else {
                    <nx-person-picker
                      label="尋找持有人"
                      emptyText="沒有符合的帳號；新進員工可直接使用姓名。"
                      nameHint="尚無帳號時只記姓名，之後可再綁定帳號"
                      [search]="people"
                      [disabled]="store.busy()"
                      (picked)="choose($event)"
                      (named)="useName($event)"
                    />
                  }
                  @if (holderError()) {
                    <p class="form-note field-error" [id]="id + '-holder-error'">
                      {{ holderError() }}
                    </p>
                  }
                  @if (e.transferFrom && !e.id) {
                    <p class="form-note">
                      {{ e.transferFrom }} 的持有期間會在下方開始時間結束，歷史仍會保留。
                    </p>
                  }
                </div>
              </section>
              <section class="form-section" role="group" [attr.aria-labelledby]="id + '-period'">
                <header class="form-section-head">
                  <h3 [id]="id + '-period'">期間</h3>
                  <p class="form-note">台北時間；開始時間之後的刷卡才歸戶給這位持有人。</p>
                </header>
                <div class="form-section-body">
                  <div class="form-row">
                    <nx-date-time-picker
                      label="開始時間"
                      [value]="e.draft.startsAt"
                      [disabled]="store.busy()"
                      (valueChange)="update('startsAt', $event)"
                    />
                    <nx-date-time-picker
                      label="結束時間（留空＝持有中）"
                      [required]="false"
                      [min]="e.draft.startsAt"
                      [value]="e.draft.endsAt"
                      [disabled]="store.busy()"
                      (valueChange)="update('endsAt', $event)"
                    />
                  </div>
                  @if (periodError()) {
                    <p class="form-note field-error">{{ periodError() }}</p>
                  }
                  <label
                    >備註（選填）<input
                      #note
                      nxField
                      autocomplete="off"
                      [attr.maxlength]="noteMax"
                      [value]="e.draft.note"
                      [readOnly]="store.busy()"
                      (input)="update('note', note.value)"
                  /></label>
                </div>
              </section>
            </div>
          </div>
          <footer class="dialog-foot">
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
              [disabled]="!store.busy() && !!e.id && !dirty()"
            >
              {{
                store.busy()
                  ? e.ending
                    ? '結束中…'
                    : e.id
                      ? '儲存中…'
                      : e.transferFrom
                        ? '轉交中…'
                        : '指派中…'
                  : e.ending
                    ? '結束持有'
                    : e.id
                      ? '儲存'
                      : e.transferFrom
                        ? '轉交'
                        : '指派'
              }}
            </button>
          </footer>
        </form>
      }
    </dialog>
    <nx-confirm-dialog />`,
})
export class AssignmentDialog {
  readonly id = uniqueId('nx-assignment-dialog');
  readonly store = inject(AttendanceCardsStore);
  readonly nameMax = NAME_MAX;
  readonly noteMax = NOTE_MAX;
  protected readonly initial = nameInitial;
  private readonly injector = inject(Injector);
  private readonly picker = viewChild(PersonPicker);
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
  /** 已決定的持有人；沒有時顯示選擇器。使用者優先，舊資料兩者都有時姓名只是備援。 */
  readonly holder = computed(() => {
    const draft = this.editor()?.draft;
    if (draft?.user)
      return {
        bound: true,
        name: draft.user.displayName,
        detail: draft.user.account || '平台帳號',
      };
    const name = draft?.holderName.trim();
    return name ? { bound: false, name, detail: '尚無帳號，只記姓名' } : null;
  });
  readonly dirty = computed(() => {
    const editor = this.editor(),
      opened = this.opened();
    return !!editor && !!opened && !same(editor.draft, opened);
  });

  /**
   * 新增；`holder` 是卡片目前的持有人，有值時就是轉給其他人。
   * 補指派待指派的刷卡時帶入第一筆沒有持有人的刷卡當開始、下一段期間的開始當結束，期間才不會與既有的重疊。
   */
  create(
    card: { id: string; cardNumber: string; holder?: string | null },
    startsAt?: string,
    endsAt?: string | null,
  ) {
    this.show({
      id: null,
      cardId: card.id,
      cardNumber: card.cardNumber,
      title: card.holder ? '轉給其他人' : '指派持有人',
      ending: false,
      transferFrom: card.holder ?? null,
      draft: {
        user: null,
        holderName: '',
        startsAt: wall(startsAt ?? new Date().toISOString()),
        endsAt: wall(endsAt),
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
        transferFrom: null,
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
    this.setHolder({ id: user.id, displayName: user.displayName, account: user.account }, '');
  }
  useName(name: string) {
    this.setHolder(null, name.slice(0, NAME_MAX));
  }
  /** 清掉持有人回到選擇器，焦點交給搜尋框（按下的按鈕會被移除）。 */
  changeHolder() {
    this.setHolder(null, '');
    afterNextRender(() => this.picker()?.focus(), { injector: this.injector });
  }
  private setHolder(user: Holder | null, holderName: string) {
    this.editor.update((editor) =>
      editor ? { ...editor, draft: { ...editor.draft, user, holderName } } : null,
    );
    this.holderError.set('');
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
    if (!editor || this.store.busy() || (editor.id && !this.dirty())) return;
    const body = this.validate(editor.draft);
    if (!body) return;
    this.error.set('');
    const outcome = editor.id
      ? await this.store.updateAssignment(
          editor.id,
          body,
          editor.ending ? '已結束持有' : '已儲存持有期間',
        )
      : await this.store.assign({ ...body, cardId: editor.cardId }, (holder) =>
          holder === editor.transferFrom
            ? Promise.resolve(true)
            : this.confirmation().ask({
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
