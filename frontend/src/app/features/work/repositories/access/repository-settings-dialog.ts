import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  inject,
  output,
  viewChild,
} from '@angular/core';
import type {
  DirectoryUserDto,
  ManagedRepositoryDetailDto,
  RepositoryPersonDto,
} from '@core/api/schema';
import { action } from '@shared/browser/action';
import { nameInitial } from '@shared/browser/format';
import { uniqueId } from '@shared/browser/unique-id';
import { Checkbox } from '@shared/ui/checkbox';
import { CompactDialog } from '@shared/ui/compact-dialog';
import { ConfirmDialog } from '@shared/ui/confirm-dialog';
import { DialogHeader } from '@shared/ui/dialog-header';
import { DialogSaveBar } from '@shared/ui/dialog-save-bar';
import { DraftEditor } from '@shared/ui/draft-editor';
import { Icon } from '@shared/ui/icon';
import { Notice } from '@shared/ui/notice';
import { directorySearch } from '@features/work/collaboration/directory-search';
import { PersonPicker } from '@features/work/collaboration/person-picker';
import { RepositoriesApi } from '../repositories-api';
import { MAX_STEWARDS } from './repository-access-presentation';

interface Draft {
  requestable: boolean;
  stewards: RepositoryPersonDto[];
}
interface Target {
  id: string;
  fullName: string;
  rowVersion: number;
}
const same = (a: Draft, b: Draft) =>
  a.requestable === b.requestable &&
  a.stewards.map((person) => person.id).join() === b.stewards.map((person) => person.id).join();

/**
 * 系統管理員設定一個儲存庫：是否開放申請、誰是負責人（審核「儲存庫負責人」那一關、可直接授予）。
 * 儲存時帶打開時的版本，期間有人改過就關閉並交給抽屜重讀。
 */
@Component({
  selector: 'nx-repository-settings-dialog',
  imports: [
    Checkbox,
    CompactDialog,
    ConfirmDialog,
    DialogHeader,
    DialogSaveBar,
    Icon,
    Notice,
    PersonPicker,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<dialog
      nxCompactDialog
      #settingsDialog
      class="platform-dialog"
      size="md"
      [busy]="saving.pending()"
      (cancel)="cancel($event)"
    >
      @if (editor.current(); as e) {
        <form class="dialog-form" novalidate (submit)="save($event)">
          <nx-dialog-header
            heading="儲存庫設定"
            [detail]="e.fullName"
            closeLabel="關閉儲存庫設定"
          />
          <div class="dialog-body">
            @if (saving.error()) {
              <nx-notice tone="danger" [message]="saving.error()" />
            }
            <div class="form-sections">
              <section class="form-section" role="group" [attr.aria-labelledby]="id + '-request'">
                <header class="form-section-head">
                  <h3 [id]="id + '-request'">申請</h3>
                </header>
                <div class="form-section-body">
                  <nx-checkbox
                    label="開放申請"
                    description="關閉後清單上只有已持有權限的人與負責人看得到。已核准的權限不受影響。"
                    [checked]="e.draft.requestable"
                    [disabled]="saving.pending()"
                    (checkedChange)="editor.update('requestable', $event)"
                  />
                </div>
              </section>
              <section class="form-section" role="group" [attr.aria-labelledby]="id + '-stewards'">
                <header class="form-section-head">
                  <h3 [id]="id + '-stewards'">負責人</h3>
                  <p class="form-note">審核申請，也可以直接授予讀取或寫入。</p>
                </header>
                <div class="form-section-body">
                  <nx-person-picker
                    label="加入負責人"
                    [search]="people"
                    [disabled]="saving.pending() || e.draft.stewards.length >= maxStewards"
                    (picked)="add($event)"
                  />
                  <ul class="person-list">
                    @for (person of e.draft.stewards; track person.id) {
                      <li class="person-row">
                        <span class="avatar" aria-hidden="true">{{
                          initial(person.displayName)
                        }}</span>
                        <span class="person-row-text"
                          ><strong>{{ person.displayName }}</strong></span
                        ><button
                          type="button"
                          class="icon-button"
                          [attr.aria-label]="'移除負責人：' + person.displayName"
                          [disabled]="saving.pending()"
                          (click)="remove(person.id)"
                        >
                          <nx-icon name="close" />
                        </button>
                      </li>
                    } @empty {
                      <li class="form-note">
                        沒有負責人時，流程的「儲存庫負責人」關卡改由簽核管理員處理。
                      </li>
                    }
                  </ul>
                </div>
              </section>
            </div>
          </div>
          <nx-dialog-save-bar
            label="儲存設定"
            [dirty]="editor.dirty()"
            [saving]="saving.pending()"
            (revert)="editor.revert()"
          />
        </form>
      }
    </dialog>
    <nx-confirm-dialog />`,
})
export class RepositorySettingsDialog {
  readonly id = uniqueId('nx-repository-settings');
  private readonly api = inject(RepositoriesApi);
  readonly saved = output<void>();
  /** 版本已過期：對話框已關閉，由抽屜顯示訊息並重讀。 */
  readonly stale = output<string>();
  readonly maxStewards = MAX_STEWARDS;
  protected readonly initial = nameInitial;
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('settingsDialog');
  private readonly confirmation = viewChild.required(ConfirmDialog);
  readonly editor = new DraftEditor<Draft, Target>(same);
  readonly saving = action();
  readonly people = directorySearch(
    () => this.editor.current()?.draft.stewards.map((person) => person.id) ?? [],
    { includeSelf: true },
  );

  open(detail: ManagedRepositoryDetailDto) {
    const repository = detail.repository;
    this.saving.reset();
    this.people.clear();
    this.editor.open(
      {
        id: repository.id,
        fullName: `${repository.owner}/${repository.name}`,
        rowVersion: detail.rowVersion,
      },
      { requestable: repository.requestable, stewards: [...repository.stewards] },
    );
    this.dialog().nativeElement.showModal();
  }
  add(user: DirectoryUserDto) {
    const stewards = this.editor.current()?.draft.stewards ?? [];
    if (stewards.length >= MAX_STEWARDS || stewards.some((person) => person.id === user.id)) return;
    this.editor.update('stewards', [...stewards, { id: user.id, displayName: user.displayName }]);
    this.people.clear();
  }
  remove(id: string) {
    const stewards = this.editor.current()?.draft.stewards ?? [];
    this.editor.update(
      'stewards',
      stewards.filter((person) => person.id !== id),
    );
  }
  cancel(event: Event) {
    event.preventDefault();
    void this.requestClose();
  }
  async requestClose() {
    if (this.saving.pending()) return;
    if (
      await this.editor.confirmDiscard(
        this.confirmation(),
        '設定尚未儲存，關閉後會捨棄這次的修改。',
      )
    )
      this.dialog().nativeElement.close();
  }
  async save(event: Event) {
    event.preventDefault();
    const editing = this.editor.current();
    if (!editing || this.saving.pending() || !this.editor.dirty()) return;
    const outcome = await this.saving.run(() =>
      this.api.saveAccessSettings(editing.id, {
        requestable: editing.draft.requestable,
        stewardIds: editing.draft.stewards.map((person) => person.id),
        rowVersion: editing.rowVersion,
      }),
    );
    if (!outcome.ok) {
      if (outcome.stale) {
        this.editor.markSaved();
        this.dialog().nativeElement.close();
        this.stale.emit(outcome.error);
      }
      return;
    }
    this.editor.markSaved();
    this.dialog().nativeElement.close();
    this.saved.emit();
  }
}
