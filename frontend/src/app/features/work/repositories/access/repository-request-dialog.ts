import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  inject,
  output,
  signal,
  viewChild,
} from '@angular/core';
import type { ManagedRepositoryDto } from '@core/api/schema';
import { action } from '@shared/browser/action';
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
import { RepositoriesApi } from '../repositories-api';
import {
  REASON_MAX,
  REQUEST_PERMISSIONS,
  permissionName,
  today,
} from './repository-access-presentation';

interface Draft {
  permission: string;
  /** `YYYY-MM-DD`；空字串表示不限期。 */
  expiresOn: string;
  reason: string;
}
interface Target {
  id: string;
  fullName: string;
  /** 目前持有的權限；有時是申請升級或延長。 */
  current: string | null;
}
const same = (a: Draft, b: Draft) =>
  a.permission === b.permission &&
  a.expiresOn === b.expiresOn &&
  a.reason.trim() === b.reason.trim();

/**
 * 申請一個儲存庫的讀取或寫入權限，送出後走簽核；核准後由同步工作把帳號加為 Gitea 協作者。
 * 已持有時是申請升級或延長，涵蓋不了的才送得出（伺服器判斷）。
 */
@Component({
  selector: 'nx-repository-request-dialog',
  imports: [
    CompactDialog,
    ConfirmDialog,
    DateTimePicker,
    DialogActions,
    DialogHeader,
    Field,
    Notice,
    Select,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<dialog
      nxCompactDialog
      #requestDialog
      class="platform-dialog"
      size="md"
      [busy]="sending.pending()"
      (cancel)="cancel($event)"
    >
      @if (editor.current(); as e) {
        <form class="dialog-form" novalidate (submit)="send($event)">
          <nx-dialog-header
            [heading]="e.current ? '申請變更權限' : '申請儲存庫權限'"
            [detail]="e.fullName"
            closeLabel="關閉申請"
          />
          <div class="dialog-body">
            @if (sending.error()) {
              <nx-notice tone="danger" [message]="sending.error()" />
            }
            <div class="form-sections">
              <section class="form-section" role="group" [attr.aria-labelledby]="id + '-access'">
                <header class="form-section-head">
                  <h3 [id]="id + '-access'">權限</h3>
                  <p class="form-note">
                    {{
                      e.current
                        ? '目前是「' +
                          permissionName(e.current) +
                          '」；核准後以這次申請的內容取代。'
                        : '核准後自動加入 Gitea 的協作者。'
                    }}
                  </p>
                </header>
                <div class="form-section-body">
                  <div class="form-row">
                    <nx-select
                      label="權限"
                      [options]="permissions"
                      [value]="e.draft.permission"
                      [disabled]="sending.pending()"
                      (valueChange)="editor.update('permission', $event)"
                    />
                    <nx-date-time-picker
                      label="到期日（選填）"
                      [withTime]="false"
                      [required]="false"
                      [min]="minDate()"
                      [value]="e.draft.expiresOn"
                      [disabled]="sending.pending()"
                      (valueChange)="editor.update('expiresOn', $event)"
                    />
                  </div>
                </div>
              </section>
              <section class="form-section" role="group" [attr.aria-labelledby]="id + '-reason'">
                <header class="form-section-head">
                  <h3 [id]="id + '-reason'">理由</h3>
                  <p class="form-note">審核人依此判斷，寫明要做的工作。</p>
                </header>
                <div class="form-section-body">
                  <label
                    >申請理由<textarea
                      #reason
                      nxField
                      rows="3"
                      [attr.maxlength]="reasonMax"
                      [attr.aria-invalid]="!!reasonError() || null"
                      [value]="e.draft.reason"
                      [readOnly]="sending.pending()"
                      (input)="updateReason(reason.value)"
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
            label="送出申請"
            pendingLabel="送出中…"
            [pending]="sending.pending()"
            (dismiss)="requestClose()"
          />
        </form>
      }
    </dialog>
    <nx-confirm-dialog />`,
})
export class RepositoryRequestDialog {
  readonly id = uniqueId('nx-repository-request');
  private readonly api = inject(RepositoriesApi);
  /** 送出成功，帶簽核申請的 ID。 */
  readonly requested = output<string>();
  readonly permissions = REQUEST_PERMISSIONS;
  readonly reasonMax = REASON_MAX;
  readonly permissionName = permissionName;
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('requestDialog');
  private readonly confirmation = viewChild.required(ConfirmDialog);
  readonly editor = new DraftEditor<Draft, Target>(same);
  readonly sending = action();
  readonly reasonError = signal('');
  readonly minDate = signal(today());

  open(repository: ManagedRepositoryDto) {
    const current = repository.myGrant?.permission ?? null;
    this.sending.reset();
    this.reasonError.set('');
    this.minDate.set(today());
    this.editor.open(
      { id: repository.id, fullName: `${repository.owner}/${repository.name}`, current },
      { permission: current === 'read' ? 'write' : 'read', expiresOn: '', reason: '' },
    );
    this.dialog().nativeElement.showModal();
  }
  updateReason(value: string) {
    this.editor.update('reason', value);
    this.reasonError.set('');
  }
  cancel(event: Event) {
    event.preventDefault();
    void this.requestClose();
  }
  async requestClose() {
    if (this.sending.pending()) return;
    if (
      await this.editor.confirmDiscard(
        this.confirmation(),
        '申請尚未送出，關閉後會捨棄已填寫的內容。',
      )
    )
      this.dialog().nativeElement.close();
  }
  async send(event: Event) {
    event.preventDefault();
    const editing = this.editor.current();
    if (!editing || this.sending.pending()) return;
    const reason = editing.draft.reason.trim();
    if (!reason) {
      this.reasonError.set('請填寫申請理由。');
      return;
    }
    const outcome = await this.sending.run(() =>
      this.api.requestAccess(editing.id, {
        permission: editing.draft.permission,
        expiresOn: editing.draft.expiresOn || null,
        reason,
      }),
    );
    if (!outcome.ok) return;
    this.editor.markSaved();
    this.dialog().nativeElement.close();
    this.requested.emit(outcome.value.instanceId);
  }
}
