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
import { action } from '@shared/browser/action';
import { nameInitial } from '@shared/browser/format';
import { uniqueId } from '@shared/browser/unique-id';
import { CompactDialog } from '@shared/ui/compact-dialog';
import { ConfirmDialog } from '@shared/ui/confirm-dialog';
import { DateTimePicker } from '@shared/ui/date-time-picker';
import { DialogActions } from '@shared/ui/dialog-actions';
import { DialogHeader } from '@shared/ui/dialog-header';
import { DraftEditor } from '@shared/ui/draft-editor';
import { Field } from '@shared/ui/field';
import { Notice } from '@shared/ui/notice';
import { Select } from '@shared/ui/select';
import { ViewSwitch, type ViewOption } from '@shared/ui/view-switch';
import { directorySearch } from '@features/work/collaboration/directory-search';
import { PersonPicker } from '@features/work/collaboration/person-picker';
import { RepositoriesApi } from '../repositories-api';
import {
  GRANT_PERMISSIONS,
  LOGIN_MAX,
  REASON_MAX,
  REQUEST_PERMISSIONS,
  today,
} from './repository-access-presentation';

/** 授予對象：平台的人，或（只有系統管理員）沒有平台帳號的 Gitea 帳號，例如部署用的服務帳號。 */
interface Draft {
  user: { id: string; displayName: string; account: string } | null;
  login: string;
  byLogin: boolean;
  permission: string;
  expiresOn: string;
  reason: string;
}
interface Target {
  id: string;
  fullName: string;
  administrator: boolean;
}
const same = (a: Draft, b: Draft) =>
  a.user?.id === b.user?.id &&
  a.login.trim() === b.login.trim() &&
  a.byLogin === b.byLogin &&
  a.permission === b.permission &&
  a.expiresOn === b.expiresOn &&
  a.reason.trim() === b.reason.trim();

/**
 * 不經簽核直接授予權限：負責人可授予讀取或寫入給平台上的人；系統管理員另可授予管理權限，或授予 Gitea 帳號（要寫理由）。
 * 對象已有權限時以這次的內容取代。授權寫到 Gitea 由同步工作處理，送出後幾秒內生效。
 */
@Component({
  selector: 'nx-repository-grant-dialog',
  imports: [
    CompactDialog,
    ConfirmDialog,
    DateTimePicker,
    DialogActions,
    DialogHeader,
    Field,
    Notice,
    PersonPicker,
    Select,
    ViewSwitch,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<dialog
      nxCompactDialog
      #grantDialog
      class="platform-dialog"
      size="md"
      [busy]="granting.pending()"
      (cancel)="cancel($event)"
    >
      @if (editor.current(); as e) {
        <form class="dialog-form" novalidate (submit)="grant($event)">
          <nx-dialog-header heading="授予權限" [detail]="e.fullName" closeLabel="關閉授予權限" />
          <div class="dialog-body">
            @if (granting.error()) {
              <nx-notice tone="danger" [message]="granting.error()" />
            }
            <div class="form-sections">
              <section class="form-section" role="group" [attr.aria-labelledby]="id + '-who'">
                <header class="form-section-head">
                  <h3 [id]="id + '-who'">對象</h3>
                </header>
                <div class="form-section-body">
                  @if (e.administrator) {
                    <nx-view-switch
                      label="授予對象的種類"
                      appearance="segment"
                      [options]="targets"
                      [value]="e.draft.byLogin ? 'login' : 'user'"
                      [disabled]="granting.pending()"
                      (valueChange)="chooseTarget($event)"
                    />
                  }
                  @if (e.draft.byLogin) {
                    <label
                      >Gitea 帳號<input
                        #login
                        nxField
                        autocomplete="off"
                        spellcheck="false"
                        [attr.maxlength]="loginMax"
                        [value]="e.draft.login"
                        [readOnly]="granting.pending()"
                        (input)="update('login', login.value)"
                    /></label>
                    <p class="form-note">沒有平台帳號的 Gitea 帳號，例如部署用的服務帳號。</p>
                  } @else if (e.draft.user; as user) {
                    <div class="form-choice">
                      <span class="avatar" aria-hidden="true">{{ initial(user.displayName) }}</span>
                      <span class="form-choice-text"
                        ><strong>{{ user.displayName }}</strong
                        ><small>{{ user.account }}</small></span
                      >
                      <button
                        type="button"
                        class="secondary-button"
                        [disabled]="granting.pending()"
                        (click)="changeUser()"
                      >
                        更換
                      </button>
                    </div>
                  } @else {
                    <nx-person-picker
                      label="尋找要授予的人"
                      [search]="people"
                      [disabled]="granting.pending()"
                      (picked)="choose($event)"
                    />
                  }
                  @if (whoError()) {
                    <p class="form-note field-error">{{ whoError() }}</p>
                  }
                </div>
              </section>
              <section class="form-section" role="group" [attr.aria-labelledby]="id + '-access'">
                <header class="form-section-head">
                  <h3 [id]="id + '-access'">權限</h3>
                  <p class="form-note">對象已有權限時，以這次的內容取代。</p>
                </header>
                <div class="form-section-body">
                  <div class="form-row">
                    <label
                      >權限<nx-select
                        label="權限"
                        [options]="permissions()"
                        [value]="e.draft.permission"
                        [disabled]="granting.pending()"
                        (valueChange)="update('permission', $event)"
                    /></label>
                    <nx-date-time-picker
                      label="到期日（選填）"
                      [withTime]="false"
                      [required]="false"
                      [min]="minDate()"
                      [value]="e.draft.expiresOn"
                      [disabled]="granting.pending()"
                      (valueChange)="update('expiresOn', $event)"
                    />
                  </div>
                  <label
                    >{{ e.draft.byLogin ? '理由' : '理由（選填）'
                    }}<textarea
                      #reason
                      nxField
                      rows="2"
                      [attr.maxlength]="reasonMax"
                      [value]="e.draft.reason"
                      [readOnly]="granting.pending()"
                      (input)="update('reason', reason.value)"
                    ></textarea>
                  </label>
                  @if (reasonError()) {
                    <p class="form-note field-error">{{ reasonError() }}</p>
                  }
                </div>
              </section>
            </div>
          </div>
          <nx-dialog-actions
            label="授予"
            pendingLabel="授予中…"
            [pending]="granting.pending()"
            (dismiss)="requestClose()"
          />
        </form>
      }
    </dialog>
    <nx-confirm-dialog />`,
})
export class RepositoryGrantDialog {
  readonly id = uniqueId('nx-repository-grant');
  private readonly api = inject(RepositoriesApi);
  private readonly injector = inject(Injector);
  readonly granted = output<void>();
  readonly loginMax = LOGIN_MAX;
  readonly reasonMax = REASON_MAX;
  /** 只有系統管理員能切到 Gitea 帳號。 */
  readonly targets: ViewOption[] = [
    { value: 'user', label: '平台上的人' },
    { value: 'login', label: 'Gitea 帳號' },
  ];
  protected readonly initial = nameInitial;
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('grantDialog');
  private readonly confirmation = viewChild.required(ConfirmDialog);
  private readonly picker = viewChild(PersonPicker);
  readonly editor = new DraftEditor<Draft, Target>(same);
  readonly granting = action();
  readonly whoError = signal('');
  readonly reasonError = signal('');
  readonly minDate = signal(today());
  readonly people = directorySearch(
    () => {
      const user = this.editor.current()?.draft.user;
      return user ? [user.id] : [];
    },
    { includeSelf: true },
  );
  /** 管理權限只有系統管理員能給。 */
  readonly permissions = computed(() =>
    this.editor.current()?.administrator ? GRANT_PERMISSIONS : REQUEST_PERMISSIONS,
  );

  /** `preset` 帶入既有的對象（把 Gitea 上直接加的協作者轉成平台授權）。 */
  open(
    repository: { id: string; owner: string; name: string },
    administrator: boolean,
    preset: {
      user?: { id: string; displayName: string } | null;
      login?: string;
      permission?: string;
    } = {},
  ) {
    this.granting.reset();
    this.whoError.set('');
    this.reasonError.set('');
    this.minDate.set(today());
    this.people.clear();
    const permission =
      preset.permission && (administrator || preset.permission !== 'admin')
        ? preset.permission
        : 'read';
    this.editor.open(
      { id: repository.id, fullName: `${repository.owner}/${repository.name}`, administrator },
      {
        user: preset.user ? { ...preset.user, account: '' } : null,
        login: preset.user ? '' : (preset.login ?? ''),
        byLogin: !preset.user && !!preset.login && administrator,
        permission,
        expiresOn: '',
        reason: '',
      },
    );
    this.dialog().nativeElement.showModal();
  }
  update<K extends keyof Draft>(key: K, value: Draft[K]) {
    this.editor.update(key, value);
    if (key === 'login') this.whoError.set('');
    if (key === 'reason') this.reasonError.set('');
  }
  choose(user: DirectoryUserDto) {
    this.editor.update('user', {
      id: user.id,
      displayName: user.displayName,
      account: user.account,
    });
    this.whoError.set('');
    this.people.clear();
  }
  /** 清掉選的人回到選擇器，焦點交給搜尋框（按下的按鈕會被移除）。 */
  changeUser() {
    this.editor.update('user', null);
    afterNextRender(() => this.picker()?.focus(), { injector: this.injector });
  }
  chooseTarget(target: string) {
    const draft = this.editor.current()?.draft;
    const byLogin = target === 'login';
    if (!draft || draft.byLogin === byLogin) return;
    this.editor.patch({ byLogin, user: null, login: '' });
    this.whoError.set('');
    this.reasonError.set('');
  }
  cancel(event: Event) {
    event.preventDefault();
    void this.requestClose();
  }
  async requestClose() {
    if (this.granting.pending()) return;
    if (
      await this.editor.confirmDiscard(
        this.confirmation(),
        '權限尚未授予，關閉後會捨棄已填寫的內容。',
      )
    )
      this.dialog().nativeElement.close();
  }
  async grant(event: Event) {
    event.preventDefault();
    const editing = this.editor.current();
    if (!editing || this.granting.pending()) return;
    const { draft } = editing;
    const login = draft.login.trim();
    const reason = draft.reason.trim();
    this.whoError.set(
      draft.byLogin ? (login ? '' : '請填寫 Gitea 帳號。') : draft.user ? '' : '請選擇要授予的人。',
    );
    this.reasonError.set(
      draft.byLogin && !reason ? '授予 Gitea 帳號要寫理由，日後覆核才知道用途。' : '',
    );
    if (this.whoError() || this.reasonError()) return;
    const outcome = await this.granting.run(() =>
      this.api.grantAccess(editing.id, {
        userId: draft.byLogin ? null : (draft.user?.id ?? null),
        login: draft.byLogin ? login : null,
        permission: draft.permission,
        expiresOn: draft.expiresOn || null,
        reason: reason || null,
      }),
    );
    if (!outcome.ok) return;
    this.editor.markSaved();
    this.dialog().nativeElement.close();
    this.granted.emit();
  }
}
