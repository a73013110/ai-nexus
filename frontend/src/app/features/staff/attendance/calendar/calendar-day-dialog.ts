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
import type { CalendarDayDto } from '@core/api/schema';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { uniqueId } from '@shared/browser/unique-id';
import { ViewScope } from '@shared/browser/view-scope';
import { Checkbox } from '@shared/ui/checkbox';
import { CompactDialog } from '@shared/ui/compact-dialog';
import { ConfirmDialog } from '@shared/ui/confirm-dialog';
import { DialogHeader } from '@shared/ui/dialog-header';
import { DialogSaveBar } from '@shared/ui/dialog-save-bar';
import { Field } from '@shared/ui/field';
import { Notice } from '@shared/ui/notice';
import { ToastService } from '@shared/ui/toast-service';
import { AttendanceApi } from '../attendance-api';
import { attendanceProblem, errorCode, weekday } from '../attendance-presentation';

interface DayDraft {
  morningOff: boolean;
  afternoonOff: boolean;
  remark: string;
}
/** 與後端 `UpdateCalendarDay.RemarkMaxLength` 相同；實際上限是 GD 欄位的位元組，存不下時伺服器回 `attendance_calendar_remark_invalid`。 */
const REMARK_MAX = 255;
const same = (a: DayDraft, b: DayDraft) =>
  a.morningOff === b.morningOff &&
  a.afternoonOff === b.afternoonOff &&
  a.remark.trim() === b.remark.trim();

/**
 * 修改公文系統（GD）某一天的上午、下午放假與備註（`attendance.calendar.maintain`）。只能改 GD 已有的日期；
 * GD 唯讀、連不上或對不到公文帳號時顯示原因，唯讀另外通知頁面停用編輯。
 */
@Component({
  selector: 'nx-calendar-day-dialog',
  imports: [Checkbox, CompactDialog, ConfirmDialog, DialogHeader, DialogSaveBar, Field, Notice],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<dialog
      nxCompactDialog
      #dayDialog
      class="platform-dialog"
      size="sm"
      (cancel)="cancel($event)"
    >
      @if (day(); as current) {
        <form class="dialog-form" novalidate (submit)="save($event)">
          <nx-dialog-header [heading]="title()" closeLabel="關閉假日編輯" />
          <div class="dialog-body">
            @if (error()) {
              <nx-notice class="calendar-day-error" tone="danger" [message]="error()" />
            }
            <nx-checkbox
              label="上午放假"
              [checked]="draft().morningOff"
              [disabled]="locked()"
              (checkedChange)="update('morningOff', $event)"
            />
            <nx-checkbox
              label="下午放假"
              [checked]="draft().afternoonOff"
              [disabled]="locked()"
              (checkedChange)="update('afternoonOff', $event)"
            />
            <label
              >備註<input
                #remark
                nxField
                autocomplete="off"
                [attr.maxlength]="remarkMax"
                [attr.aria-describedby]="id + '-remark'"
                [value]="draft().remark"
                [readOnly]="locked()"
                (input)="update('remark', remark.value)"
            /></label>
            <p class="form-note" [id]="id + '-remark'">
              寫回公文系統；罕用字與表情符號公文系統存不下，會被拒絕。
            </p>
          </div>
          <nx-dialog-save-bar
            label="儲存假日"
            [dirty]="dirty()"
            [saving]="saving()"
            [disabled]="session.testing()"
            [note]="session.testingNote()"
            (revert)="revert()"
          />
        </form>
      }
    </dialog>
    <nx-confirm-dialog />`,
})
export class CalendarDayDialog {
  readonly id = uniqueId('nx-calendar-day');
  readonly session = inject(WorkspaceSession);
  private readonly api = inject(AttendanceApi);
  private readonly scope = inject(ViewScope);
  private readonly toast = inject(ToastService);
  readonly remarkMax = REMARK_MAX;
  readonly saved = output<CalendarDayDto>();
  /** 伺服器回 `attendance_gd_read_only`：部署設定只讀 GD，頁面停用編輯。 */
  readonly readOnly = output<void>();
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dayDialog');
  private readonly confirmation = viewChild.required(ConfirmDialog);
  readonly day = signal<CalendarDayDto | null>(null);
  readonly draft = signal<DayDraft>({ morningOff: false, afternoonOff: false, remark: '' });
  private readonly opened = signal<DayDraft | null>(null);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly locked = computed(() => this.saving() || this.session.testing());
  readonly dirty = computed(() => {
    const opened = this.opened();
    return !!opened && !same(this.draft(), opened);
  });
  readonly title = computed(() => {
    const day = this.day();
    return day ? `編輯假日：${day.date.replaceAll('-', '/')}（${weekday(day.date)}）` : '';
  });

  open(day: CalendarDayDto) {
    const draft = {
      morningOff: day.morningOff,
      afternoonOff: day.afternoonOff,
      remark: day.remark ?? '',
    };
    this.error.set('');
    this.day.set(day);
    this.draft.set(draft);
    this.opened.set(draft);
    this.dialog().nativeElement.showModal();
  }
  update<K extends keyof DayDraft>(key: K, value: DayDraft[K]) {
    this.draft.update((draft) => ({ ...draft, [key]: value }));
  }
  revert() {
    const opened = this.opened();
    if (opened) this.draft.set(opened);
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
        message: `「${this.title()}」的變更尚未儲存，關閉後會恢復為公文系統目前的內容。`,
        confirm: '放棄變更',
      }))
    )
      return;
    this.dialog().nativeElement.close();
  }

  async save(event: Event) {
    event.preventDefault();
    const day = this.day();
    if (!day || this.locked() || !this.dirty()) return;
    const draft = this.draft();
    const alive = this.scope.guard();
    this.saving.set(true);
    this.error.set('');
    try {
      const saved = await this.api.updateCalendarDay(day.date, {
        morningOff: draft.morningOff,
        afternoonOff: draft.afternoonOff,
        remark: draft.remark.trim() || null,
      });
      if (!alive()) return;
      this.opened.set(draft);
      this.dialog().nativeElement.close();
      this.toast.success(`已更新 ${day.date.replaceAll('-', '/')} 的假日設定`);
      this.saved.emit(saved);
    } catch (error) {
      if (!alive()) return;
      this.error.set(attendanceProblem(error));
      if (errorCode(error) === 'attendance_gd_read_only') this.readOnly.emit();
    } finally {
      if (alive()) this.saving.set(false);
    }
  }
}
