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
import type { PunchReimportResult } from '@core/api/schema';
import { formatNumber } from '@shared/browser/format';
import { ViewScope } from '@shared/browser/view-scope';
import { CompactDialog } from '@shared/ui/compact-dialog';
import { DateTimePicker } from '@shared/ui/date-time-picker';
import { DialogHeader } from '@shared/ui/dialog-header';
import { Notice } from '@shared/ui/notice';
import { PendingButton } from '@shared/ui/pending-button';
import { ViewMotion } from '@shared/ui/view-motion';
import { ViewSwitch, type ViewOption } from '@shared/ui/view-switch';
import { AttendanceApi } from '../attendance-api';
import { attendanceProblem, taipeiToday } from '../attendance-presentation';
import { reimportTour } from '../attendance-tour';
import { MAX_REIMPORT_DAYS } from './imports-query';

type ReimportMode = 'single' | 'range';

const MODES: readonly ViewOption[] = [
  { value: 'single', label: '單一檔案' },
  { value: 'range', label: '日期區間' },
];
const DAY = 86_400_000;
const isDate = (value: string) => /^\d{4}-\d{2}-\d{2}$/.test(value);
/** 公曆日期的日序；以 UTC 計算，不受瀏覽器時區與日光節約影響。 */
const dayNumber = (date: string) => Date.parse(date + 'T00:00:00Z') / DAY;
const shiftDays = (date: string, days: number) =>
  new Date((dayNumber(date) + days) * DAY).toISOString().slice(0, 10);

/** 檔名日期（`YYYY-MM-DD`）對應的每日檔名 `yyyyMMdd.txt`；日期無效時為空字串。 */
export const punchFileName = (date: string) =>
  isDate(date) ? date.replaceAll('-', '') + '.txt' : '';

/** 要重匯的檔名日期區間（含頭尾）；日期無效、顛倒或超過上限時為錯誤說明。 */
export function reimportPlan(
  from: string,
  to: string,
): { from: string; to: string; count: number } | { error: string } {
  if (!isDate(from) || !isDate(to)) return { error: '' };
  const count = dayNumber(to) - dayNumber(from) + 1;
  if (count < 1) return { error: '結束日期不能早於開始日期。' };
  if (count > MAX_REIMPORT_DAYS)
    return { error: `一次最多重匯 ${MAX_REIMPORT_DAYS} 天（${count} 天超過上限）。` };
  return { from, to, count };
}

/** 檔名清單：最多列三個，其餘以「等 N 個檔案」帶過。 */
const fileList = (names: readonly string[]) =>
  names.length <= 3
    ? names.join('、')
    : `${names.slice(0, 3).join('、')} 等 ${names.length} 個檔案`;

/**
 * 重匯結果給頁面的呈現：有成功的檔用 toast（剛完成的操作），失敗的檔留在頁面提示（要留著讀的原因）；
 * 找不到的檔不是失敗（打卡機那天沒有輸出），附在其中一則裡。
 */
export function reimportOutcome(result: PunchReimportResult): {
  toast?: { title: string; description: string };
  failure?: string;
} {
  const succeeded = result.imports.filter((x) => x.status !== 'failed');
  const failed = result.imports.filter((x) => x.status === 'failed');
  const missing = result.missingFiles.length ? `找不到 ${fileList(result.missingFiles)}。` : '';
  const punches = formatNumber(succeeded.reduce((sum, x) => sum + x.punchCount, 0));
  const toast = succeeded.length
    ? {
        title:
          succeeded.length === 1
            ? `已重匯 ${succeeded[0].fileName}`
            : `已重匯 ${succeeded.length} 個檔案`,
        description: `${punches} 筆刷卡` + (missing ? `；${missing}` : ''),
      }
    : undefined;
  if (!failed.length) return { toast };
  const reason =
    failed.length === 1
      ? failed[0].errorMessage
        ? `原因：${failed[0].errorMessage}`
        : ''
      : '原因見各檔的匯入紀錄。';
  return {
    toast,
    failure:
      `重匯 ${fileList(failed.map((x) => x.fileName))} 失敗，刷卡維持上次成功的內容。` +
      reason +
      (toast ? '' : missing),
  };
}

/**
 * 手動重匯打卡檔（`attendance.imports.reimport`）：預設一個檔名日期，可切換成日期區間；不論檔案有沒有變動都重匯。
 * 整段都找不到檔或資料夾讀不到是請求失敗、不留紀錄，訊息留在對話框；其餘結果（含失敗的匯入與找不到的檔）交給頁面呈現。
 */
@Component({
  selector: 'nx-reimport-dialog',
  imports: [
    CompactDialog,
    DateTimePicker,
    DialogHeader,
    Notice,
    PendingButton,
    ViewMotion,
    ViewSwitch,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    .reimport-range {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(10rem, 1fr));
      gap: var(--p-space-3);
    }
    .field-error {
      color: var(--error);
    }
  `,
  template: `<dialog
    nxCompactDialog
    #reimportDialog
    class="platform-dialog"
    size="sm"
    [busy]="running()"
    (cancel)="cancel($event)"
  >
    <form class="dialog-form" novalidate (submit)="submit($event)">
      <nx-dialog-header heading="手動重匯打卡檔" closeLabel="關閉重匯" [tour]="tour()" />
      <div class="dialog-body">
        @if (error()) {
          <nx-notice tone="danger" [message]="error()" />
        }
        <nx-view-switch
          label="重匯範圍"
          appearance="segment"
          [fill]="true"
          [options]="modes"
          [value]="mode()"
          [disabled]="running()"
          (valueChange)="switchMode($any($event))"
        />
        <div [nxViewMotion]="mode()">
          @if (mode() === 'single') {
            <nx-date-time-picker
              label="檔名日期"
              [withTime]="false"
              [max]="today"
              [value]="date()"
              [disabled]="running()"
              (valueChange)="date.set($event)"
            />
          } @else {
            <div class="reimport-range">
              <nx-date-time-picker
                label="開始檔名日期"
                [withTime]="false"
                [max]="today"
                [value]="from()"
                [disabled]="running()"
                (valueChange)="from.set($event)"
              />
              <nx-date-time-picker
                label="結束檔名日期"
                [withTime]="false"
                [min]="from()"
                [max]="today"
                [value]="to()"
                [disabled]="running()"
                (valueChange)="to.set($event)"
              />
            </div>
            @if (rangeError()) {
              <p class="form-note field-error" role="alert">{{ rangeError() }}</p>
            }
          }
        </div>
        <p class="form-note reimport-note">
          <span aria-live="polite">{{ summary() }}</span>
          檔名日期不是出勤日，檔案可能含前一天深夜的刷卡；不論檔案有沒有變都會重新匯入。
        </p>
      </div>
      <footer class="dialog-foot">
        <button
          type="button"
          class="secondary-button"
          [attr.aria-disabled]="running() || null"
          (click)="close()"
        >
          取消
        </button>
        <button
          nxPending
          type="submit"
          class="primary-button"
          [pending]="running()"
          [disabled]="!running() && !range()"
        >
          {{ running() ? '重匯中…' : '重匯' }}
        </button>
      </footer>
    </form>
  </dialog>`,
})
export class ReimportDialog {
  private readonly api = inject(AttendanceApi);
  private readonly scope = inject(ViewScope);
  readonly imported = output<PunchReimportResult>();
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('reimportDialog');
  readonly modes = MODES;
  readonly today = taipeiToday();
  readonly mode = signal<ReimportMode>('single');
  readonly date = signal(this.today);
  readonly from = signal(this.today);
  readonly to = signal(this.today);
  readonly running = signal(false);
  readonly error = signal('');
  readonly tour = computed(() => reimportTour({ mode: this.mode() }));
  /** 這次要重匯的區間；單一檔案是起訖相同的區間。無效時為 undefined，送出停用。 */
  private readonly plan = computed(() =>
    this.mode() === 'single'
      ? reimportPlan(this.date(), this.date())
      : reimportPlan(this.from(), this.to()),
  );
  readonly range = computed(() => {
    const plan = this.plan();
    return 'error' in plan ? undefined : plan;
  });
  readonly rangeError = computed(() => {
    const plan = this.plan();
    return 'error' in plan ? plan.error : '';
  });
  readonly summary = computed(() => {
    const range = this.range();
    if (!range) return this.mode() === 'single' ? '重匯 yyyyMMdd.txt。' : '';
    return range.count === 1
      ? `重匯 ${punchFileName(range.from)}。`
      : `重匯 ${punchFileName(range.from)}～${punchFileName(range.to)}，共 ${range.count} 個檔案。`;
  });

  open() {
    const today = taipeiToday();
    this.error.set('');
    this.mode.set('single');
    this.date.set(today);
    this.from.set(today);
    this.to.set(today);
    this.dialog().nativeElement.showModal();
  }
  /** 切到區間時從單一檔案選的日期開始、到今天為止（不超過上限），切回時保留各自的選擇。 */
  switchMode(mode: ReimportMode) {
    if (mode === this.mode() || this.running()) return;
    if (mode === 'range' && isDate(this.date())) {
      const today = taipeiToday();
      const last = shiftDays(this.date(), MAX_REIMPORT_DAYS - 1);
      this.from.set(this.date());
      this.to.set(last < today ? last : today);
    }
    this.error.set('');
    this.mode.set(mode);
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
    const range = this.range();
    if (this.running() || !range) return;
    const alive = this.scope.guard();
    this.running.set(true);
    this.error.set('');
    try {
      const result = await this.api.reimport(range.from, range.to);
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
