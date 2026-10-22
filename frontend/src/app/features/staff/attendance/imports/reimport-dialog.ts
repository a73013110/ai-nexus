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
import type { PunchImportDto } from '@core/api/schema';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { ViewScope } from '@shared/browser/view-scope';
import { CompactDialog } from '@shared/ui/compact-dialog';
import { DateTimePicker } from '@shared/ui/date-time-picker';
import { DialogHeader } from '@shared/ui/dialog-header';
import { Notice } from '@shared/ui/notice';
import { AttendanceApi } from '../attendance-api';
import { attendanceProblem, taipeiToday } from '../attendance-presentation';

/**
 * 手動重匯某一天的打卡檔（`attendance.imports.reimport`）：不論檔案有沒有變動都重匯。
 * 找不到檔或資料夾讀不到是請求失敗、不留紀錄，訊息留在對話框；檔案存在但匯入失敗會回傳失敗的紀錄，交給頁面提示。
 */
@Component({
  selector: 'nx-reimport-dialog',
  imports: [CompactDialog, DateTimePicker, DialogHeader, Notice],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<dialog
    nxCompactDialog
    #reimportDialog
    class="platform-dialog"
    size="sm"
    (cancel)="cancel($event)"
  >
    <form class="dialog-form" novalidate (submit)="submit($event)">
      <nx-dialog-header heading="手動重匯打卡檔" closeLabel="關閉重匯" />
      <div class="dialog-body">
        @if (error()) {
          <nx-notice tone="danger" [message]="error()" />
        }
        <nx-date-time-picker
          label="檔名日期"
          [withTime]="false"
          [max]="today"
          [value]="date()"
          [disabled]="running()"
          (valueChange)="date.set($event)"
        />
        <p class="form-note">
          重匯 {{ fileName() || 'yyyyMMdd.txt' }}。檔名日期不是出勤日，檔案可能含前一天深夜的刷卡；
          不論檔案有沒有變都會重新匯入。
        </p>
      </div>
      <footer class="dialog-foot">
        @if (session.testing()) {
          <span class="dialog-note" role="status">{{ session.testingNote() }}</span>
        }
        <button type="button" class="secondary-button" [disabled]="running()" (click)="close()">
          取消
        </button>
        <button
          type="submit"
          class="primary-button"
          [disabled]="running() || session.testing() || !fileName()"
        >
          {{ running() ? '重匯中…' : '重匯' }}
        </button>
      </footer>
    </form>
  </dialog>`,
})
export class ReimportDialog {
  readonly session = inject(WorkspaceSession);
  private readonly api = inject(AttendanceApi);
  private readonly scope = inject(ViewScope);
  readonly imported = output<PunchImportDto>();
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('reimportDialog');
  readonly today = taipeiToday();
  readonly date = signal(this.today);
  readonly running = signal(false);
  readonly error = signal('');
  readonly fileName = computed(() =>
    /^\d{4}-\d{2}-\d{2}$/.test(this.date()) ? this.date().replaceAll('-', '') + '.txt' : '',
  );

  open() {
    this.error.set('');
    this.date.set(taipeiToday());
    this.dialog().nativeElement.showModal();
  }
  close() {
    if (!this.running()) this.dialog().nativeElement.close();
  }
  cancel(event: Event) {
    event.preventDefault();
    this.close();
  }
  async submit(event: Event) {
    event.preventDefault();
    if (this.running() || this.session.testing() || !this.fileName()) return;
    const alive = this.scope.guard();
    this.running.set(true);
    this.error.set('');
    try {
      const result = await this.api.reimport(this.date());
      if (!alive()) return;
      this.dialog().nativeElement.close();
      this.imported.emit(result);
    } catch (error) {
      if (alive()) this.error.set(attendanceProblem(error));
    } finally {
      if (alive()) this.running.set(false);
    }
  }
}
