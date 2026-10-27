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
import { CalendarDayTemplate, MonthCalendar, type CalendarDay } from '@shared/ui/month-calendar';
import { Notice } from '@shared/ui/notice';
import { PendingButton } from '@shared/ui/pending-button';
import { queryState, type QueryStateOptions } from '@shared/ui/query-state';
import { StatusBadge } from '@shared/ui/status-badge';
import { ViewSwitch, type ViewOption } from '@shared/ui/view-switch';
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
import { attendanceCalendarTour } from '../attendance-tour';
import { CalendarDayDialog } from './calendar-day-dialog';
import { HolidayGlyph, holidayKind, type HolidayKind } from './holiday-glyph';

type CalendarView = 'calendar' | 'list';
interface CalendarQuery {
  month: string;
  view: CalendarView;
}
const currentMonth = () => taipeiToday().slice(0, 7);
/** 月份與檢視寫進網址（本月、月曆不寫），重新整理與分享連結停在同一個月、同一種檢視。 */
const calendarQueryOptions: QueryStateOptions<CalendarQuery> = {
  defaults: () => ({ month: currentMonth(), view: 'calendar' }),
  toParams: (query) => ({
    month: query.month === currentMonth() ? undefined : query.month,
    view: query.view === 'list' ? 'list' : undefined,
  }),
  fromParams: (params, defaults) => {
    const month = params.get('month') ?? '';
    return {
      month: MONTH.test(month) ? month : defaults.month,
      view: params.get('view') === 'list' ? 'list' : 'calendar',
    };
  },
};
const VIEWS: ViewOption[] = [
  { value: 'calendar', label: '月曆', icon: 'calendar' },
  { value: 'list', label: '清單', icon: 'list' },
];
const MISSING = '未建立：公文系統沒有這一天，不能在這裡新增；出勤判定時週末當放假、平日無法判定。';

/** 讀取結果；公文系統不可用時不是空月份（空清單會被當成整月上班），而是帶著原因的失敗。 */
interface MonthRead {
  month: string;
  days: CalendarDayDto[] | null;
  problem: string;
}

/** 月曆格子的資料；清單與月曆都從同一份 `days` 衍生。 */
interface HolidayCell extends CalendarDay {
  value: CalendarDayDto | null;
  kind: HolidayKind | null;
  type: string;
  weekend: boolean;
}

/**
 * 假日設定（`attendance.calendar`）：逐月檢視公文系統（GD）的假日行事曆；有 `.maintain` 時可修改上午、下午放假與備註。
 * 預設是月曆（半天放假畫在格子的上半或下半），清單是同一份資料的逐日表格，手機上讀備註較方便。
 * 只列出整個月，GD 沒有的日期標示「未建立」：回寫只能改 GD 既有的日期，出勤判定時週末當放假、平日無法判定。
 */
@Component({
  selector: 'nx-attendance-calendar-page',
  imports: [
    CalendarDayDialog,
    CalendarDayTemplate,
    DataTable,
    DataTableColumn,
    DataTableRow,
    FeaturePage,
    HolidayGlyph,
    Icon,
    MonthCalendar,
    Notice,
    PendingButton,
    StatusBadge,
    TableState,
    ViewSwitch,
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
  readonly view = computed(() => this.query.applied().view);
  readonly views = VIEWS;
  readonly title = computed(() => monthLabel(this.month()));
  readonly today = taipeiToday();
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
  /** 換月或首次讀取：這個月還沒有資料，格子畫骨架。 */
  readonly loading = this.read.loading;
  /** 使用者感覺得到的讀取（含換月與重試）；已有內容時淡化。 */
  readonly refreshing = this.read.refreshing;
  readonly readError = computed(() => this.read.error() || this.loaded()?.problem || '');
  /** 目前月份的讀取結果；換月時舊月份不會短暫顯示在新標題下。 */
  readonly loaded = computed(() => {
    const value = this.read.value();
    return value && value.month === this.month() ? value : null;
  });
  readonly canMaintain = computed(() => this.session.has('attendance.calendar.maintain'));
  /** 回寫遇到 `attendance_gd_read_only` 後停用編輯，直到重新進入頁面（設定要改並重啟才會恢復）。 */
  readonly readOnly = signal(false);
  readonly editable = computed(() => this.canMaintain() && !this.readOnly());
  readonly tour = computed(() =>
    attendanceCalendarTour({
      view: this.view(),
      maintain: this.canMaintain(),
      gdReadOnly: this.readOnly(),
    }),
  );
  /** 正在編輯（或剛編輯過）的日期，月曆上標成選取。 */
  readonly selected = signal<string | null>(null);
  readonly columns: TableColumn[] = [
    { id: 'date', label: '日期', hideable: false },
    { id: 'type', label: '類型', hideable: false },
    { id: 'morning', label: '上午' },
    { id: 'afternoon', label: '下午' },
    { id: 'remark', label: '備註' },
  ];
  /** 整個月逐日的值；讀不到（載入中、失敗）時是空的，不會把整月標成「未建立」。 */
  readonly days = computed(() => {
    const loaded = this.loaded();
    if (!loaded?.days) return [];
    const byDate = new Map(loaded.days.map((day) => [day.date, day]));
    const { from, to } = monthBounds(loaded.month);
    const editable = this.editable();
    const days: HolidayCell[] = [];
    for (let day = Number(from.slice(8)); day <= Number(to.slice(8)); day++) {
      const date = `${loaded.month}-${String(day).padStart(2, '0')}`;
      const value = byDate.get(date) ?? null;
      const kind = value ? holidayKind(value.morningOff, value.afternoonOff) : null;
      const type = kind ? ATTENDANCE_DAY_TYPE[kind] : '';
      days.push({
        date,
        value,
        kind,
        type,
        weekend: isWeekend(date),
        description: value ? [type, value.remark].filter(Boolean).join('，') : MISSING,
        disabled: !editable || !value,
      });
    }
    return days;
  });
  readonly rows = computed(() =>
    this.days().map((day) => ({
      ...day,
      label: `${day.date.slice(5).replace('-', '/')}（${weekday(day.date)}）`,
      today: day.date === this.today,
    })),
  );
  readonly offDays = computed(
    () => this.loaded()?.days?.filter((day) => day.morningOff || day.afternoonOff).length ?? 0,
  );
  private readonly dialog = viewChild.required(CalendarDayDialog);

  go(delta: number) {
    this.query.commit({ month: shiftMonth(this.month(), delta) });
  }
  goTo(month: string) {
    if (MONTH.test(month) && month !== this.month()) this.query.commit({ month });
  }
  thisMonth() {
    this.query.commit({ month: currentMonth() });
  }
  showView(view: string) {
    this.query.commit({ view: view === 'list' ? 'list' : 'calendar' });
  }
  reload() {
    this.read.reload();
  }
  edit(day: CalendarDayDto | null) {
    if (!day || !this.editable()) return;
    this.selected.set(day.date);
    this.dialog().open(day);
  }
  editDate(date: string) {
    this.edit(this.days().find((day) => day.date === date)?.value ?? null);
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
