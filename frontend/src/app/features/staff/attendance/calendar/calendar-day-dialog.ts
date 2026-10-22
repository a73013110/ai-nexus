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
import { CompactDialog } from '@shared/ui/compact-dialog';
import { ConfirmDialog } from '@shared/ui/confirm-dialog';
import { DialogHeader } from '@shared/ui/dialog-header';
import { DialogSaveBar } from '@shared/ui/dialog-save-bar';
import { Field } from '@shared/ui/field';
import { Icon } from '@shared/ui/icon';
import { Notice } from '@shared/ui/notice';
import { ToastService } from '@shared/ui/toast-service';
import { AttendanceApi } from '../attendance-api';
import { attendanceProblem, errorCode, weekday } from '../attendance-presentation';
import { HOLIDAY_KINDS, HolidayGlyph, holidayKind, type HolidayKind } from './holiday-glyph';

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
 * 上午、下午兩個布林在畫面上是四選一（上班日／上午放假／下午放假／放假）：兩個獨立勾選要使用者自己
 * 組合出「全天」，選項卡片直接說出結果，並用與月曆相同的半格圖形。
 */
@Component({
  selector: 'nx-calendar-day-dialog',
  imports: [
    CompactDialog,
    ConfirmDialog,
    DialogHeader,
    DialogSaveBar,
    Field,
    HolidayGlyph,
    Icon,
    Notice,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './calendar-day-dialog.scss',
  template: `<dialog
      nxCompactDialog
      #dayDialog
      class="platform-dialog"
      size="sm"
      (cancel)="cancel($event)"
    >
      @if (day()) {
        <form class="dialog-form" novalidate (submit)="save($event)">
          <nx-dialog-header
            [heading]="title()"
            [detail]="weekdayName()"
            closeLabel="關閉假日編輯"
          />
          <div class="dialog-body">
            @if (error()) {
              <nx-notice class="calendar-day-error" tone="danger" [message]="error()" />
            }
            <fieldset class="day-kind" [disabled]="locked()">
              <legend>
                放假時段
                @if (kind() !== openedKind()) {
                  <small>原為「{{ openedLabel() }}」</small>
                }
              </legend>
              <div class="day-kind-options">
                @for (option of kinds; track option.kind) {
                  <label class="day-kind-option" [class.is-checked]="kind() === option.kind">
                    <input
                      type="radio"
                      class="sr-only"
                      [name]="id + '-kind'"
                      [value]="option.kind"
                      [checked]="kind() === option.kind"
                      (change)="choose(option.kind)"
                    />
                    <nx-holiday-glyph [kind]="option.kind" />
                    <span class="day-kind-text">
                      <b>{{ option.label }}</b>
                      <small>{{ option.hint }}</small>
                    </span>
                    <nx-icon class="day-kind-check" name="check" />
                  </label>
                }
              </div>
            </fieldset>
            <div class="day-remark">
              <label [for]="id + '-remark'">備註 <small>選填</small></label>
              <input
                #remark
                nxField
                autocomplete="off"
                placeholder="例如：國慶日、颱風停班"
                [id]="id + '-remark'"
                [attr.maxlength]="remarkMax"
                [attr.aria-describedby]="id + '-remark-note'"
                [value]="draft().remark"
                [readOnly]="locked()"
                (input)="update('remark', remark.value)"
              />
              <p class="day-remark-meta">
                <span class="form-note" [id]="id + '-remark-note'"
                  >罕用字與表情符號公文系統存不下，會被拒絕。</span
                >
                <span class="day-remark-count" [class.is-near]="remarkNear()" aria-hidden="true"
                  >{{ draft().remark.length }} / {{ remarkMax }}</span
                >
              </p>
            </div>
          </div>
          <nx-dialog-save-bar
            label="儲存至公文系統"
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
  readonly kinds = HOLIDAY_KINDS;
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
  readonly kind = computed(() => holidayKind(this.draft().morningOff, this.draft().afternoonOff));
  readonly openedKind = computed(() => {
    const opened = this.opened();
    return opened ? holidayKind(opened.morningOff, opened.afternoonOff) : this.kind();
  });
  readonly openedLabel = computed(
    () => HOLIDAY_KINDS.find((option) => option.kind === this.openedKind())?.label ?? '',
  );
  /** 剩不到 20 字時計數變色；上限是字數，GD 實際以位元組計，所以只提醒、不擋。 */
  readonly remarkNear = computed(() => this.draft().remark.length > REMARK_MAX - 20);
  /** 「編輯假日：2026 年 10 月 5 日」，星期放在標題旁（detail），無障礙名稱仍是完整日期。 */
  readonly title = computed(() => {
    const date = this.day()?.date;
    return date
      ? `編輯假日：${date.slice(0, 4)} 年 ${Number(date.slice(5, 7))} 月 ${Number(date.slice(8))} 日`
      : '';
  });
  readonly weekdayName = computed(() => {
    const date = this.day()?.date;
    return date ? `星期${weekday(date)}` : '';
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
  choose(kind: HolidayKind) {
    const option = HOLIDAY_KINDS.find((item) => item.kind === kind);
    if (!option) return;
    this.draft.update((draft) => ({
      ...draft,
      morningOff: option.morningOff,
      afternoonOff: option.afternoonOff,
    }));
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
