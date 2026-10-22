import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { apiResource } from '@core/api/api-resource';
import type { CalendarDayDto } from '@core/api/schema';
import { WorkspaceSession } from '@core/auth/workspace-session';
import { FeaturePage } from '@core/layout/feature-page';
import { ViewScope } from '@shared/browser/view-scope';
import {
  DataTable,
  DataTableColumn,
  DataTableRow,
  TableState,
  type TableColumn,
} from '@shared/ui/data-table';
import { Icon } from '@shared/ui/icon';
import { Notice } from '@shared/ui/notice';
import { queryState, type QueryStateOptions } from '@shared/ui/query-state';
import { StatusBadge } from '@shared/ui/status-badge';
import { AttendanceApi } from '../attendance-api';
import {
  ATTENDANCE_DAY_TYPE,
  MONTH,
  attendanceProblem,
  errorCode,
  isWeekend,
  monthBounds,
  monthLabel,
  shiftMonth,
  taipeiToday,
  weekday,
} from '../attendance-presentation';
import { CalendarDayDialog } from './calendar-day-dialog';

interface CalendarQuery {
  month: string;
}
const currentMonth = () => taipeiToday().slice(0, 7);
/** 月份寫進網址（本月不寫），重新整理與分享連結停在同一個月。 */
const calendarQueryOptions: QueryStateOptions<CalendarQuery> = {
  defaults: () => ({ month: currentMonth() }),
  toParams: (query) => ({ month: query.month === currentMonth() ? undefined : query.month }),
  fromParams: (params, defaults) => {
    const month = params.get('month') ?? '';
    return { month: MONTH.test(month) ? month : defaults.month };
  },
};

/** 讀取結果；公文系統不可用時不是空月份（空清單會被當成整月上班），而是帶著原因的失敗。 */
interface MonthRead {
  month: string;
  days: CalendarDayDto[] | null;
  problem: string;
}

/**
 * 假日設定（`attendance.calendar`）：逐月檢視公文系統（GD）的假日行事曆；有 `.maintain` 時可修改上午、下午放假與備註。
 * 只列出整個月，GD 沒有的日期標示「未建立」：回寫只能改 GD 既有的日期，出勤判定時週末當放假、平日無法判定。
 */
@Component({
  selector: 'nx-attendance-calendar-page',
  imports: [
    CalendarDayDialog,
    DataTable,
    DataTableColumn,
    DataTableRow,
    FeaturePage,
    Icon,
    Notice,
    StatusBadge,
    TableState,
  ],
  providers: [ViewScope],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: ':host { display: block; min-width: 0; }',
  styleUrl: './attendance-calendar-page.scss',
  templateUrl: './attendance-calendar-page.html',
})
export class AttendanceCalendarPage {
  readonly session = inject(WorkspaceSession);
  private readonly api = inject(AttendanceApi);
  readonly query = queryState(calendarQueryOptions);
  readonly month = computed(() => this.query.applied().month);
  readonly title = computed(() => monthLabel(this.month()));
  private readonly read = apiResource({
    feature: 'attendance.calendar',
    params: () => this.month(),
    loader: async (month, signal): Promise<MonthRead> => {
      const { from, to } = monthBounds(month);
      try {
        return { month, days: await this.api.calendarDays(from, to, signal), problem: '' };
      } catch (error) {
        // GD 的 503 有固定原因，轉成說明；其他失敗（含取消）照一般讀取處理。
        if (errorCode(error).startsWith('attendance_gd_'))
          return { month, days: null, problem: attendanceProblem(error) };
        throw error;
      }
    },
  });
  readonly loading = this.read.loading;
  readonly readError = computed(() => this.read.error() || this.loaded()?.problem || '');
  /** 目前月份的讀取結果；換月時舊月份不會短暫顯示在新標題下。 */
  readonly loaded = computed(() => {
    const value = this.read.value();
    return value && value.month === this.month() ? value : null;
  });
  readonly canMaintain = computed(() => this.session.has('attendance.calendar.maintain'));
  /** 回寫遇到 `attendance_gd_read_only` 後停用編輯，直到重新進入頁面（設定要改並重啟才會恢復）。 */
  readonly readOnly = signal(false);
  readonly editable = computed(
    () => this.canMaintain() && !this.readOnly() && !this.session.testing(),
  );
  readonly columns: TableColumn[] = [
    { id: 'date', label: '日期', hideable: false },
    { id: 'type', label: '類型', hideable: false },
    { id: 'morning', label: '上午' },
    { id: 'afternoon', label: '下午' },
    { id: 'remark', label: '備註' },
  ];
  readonly rows = computed(() => {
    const loaded = this.loaded();
    if (!loaded?.days) return [];
    const byDate = new Map(loaded.days.map((day) => [day.date, day]));
    const { from, to } = monthBounds(loaded.month);
    const today = taipeiToday();
    const rows = [];
    for (let day = Number(from.slice(8)); day <= Number(to.slice(8)); day++) {
      const date = `${loaded.month}-${String(day).padStart(2, '0')}`;
      const value = byDate.get(date) ?? null;
      rows.push({
        date,
        label: `${date.slice(5).replace('-', '/')}（${weekday(date)}）`,
        weekend: isWeekend(date),
        today: date === today,
        value,
        type: value ? dayType(value) : '',
      });
    }
    return rows;
  });
  readonly offDays = computed(
    () => this.loaded()?.days?.filter((day) => day.morningOff || day.afternoonOff).length ?? 0,
  );
  private readonly dialog = viewChild.required(CalendarDayDialog);

  go(delta: number) {
    this.query.commit({ month: shiftMonth(this.month(), delta) });
  }
  thisMonth() {
    this.query.commit({ month: currentMonth() });
  }
  reload() {
    this.read.reload();
  }
  edit(day: CalendarDayDto | null) {
    if (day && this.editable()) this.dialog().open(day);
  }
  /** 伺服器回傳修改後的值，直接換進目前月份，不必重讀整個月。 */
  saved(day: CalendarDayDto) {
    this.read.value.update((current) =>
      current?.days
        ? { ...current, days: current.days.map((item) => (item.date === day.date ? day : item)) }
        : current,
    );
  }
}

function dayType(day: CalendarDayDto) {
  return ATTENDANCE_DAY_TYPE[
    day.morningOff && day.afternoonOff
      ? 'DayOff'
      : day.morningOff
        ? 'MorningOff'
        : day.afternoonOff
          ? 'AfternoonOff'
          : 'FullDay'
  ];
}
