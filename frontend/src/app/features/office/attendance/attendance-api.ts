import { Injectable, inject } from '@angular/core';
import { ApiClient, type ApiQuery } from '@core/api/api-client';
import type {
  CalendarDayRequest,
  CardAssignmentCreateRequest,
  CardAssignmentUpdateRequest,
  CardRequest,
  RosterExclusionRequest,
} from '@core/api/schema';

/** 出勤紀錄已套用的條件（`from`、`to` 必填，最長一年），查詢與匯出共用；分頁（`offset`、`limit`）每頁另外加上。 */
export type AttendanceRecordFilters = Omit<
  ApiQuery<'/api/v1/attendance-records', 'get'>,
  'offset' | 'limit'
>;
/** 匯入紀錄已套用的條件；分頁參數（`before`、`take`）每頁另外加上。 */
export type PunchImportFilters = Omit<
  ApiQuery<'/api/v1/attendance-imports', 'get'>,
  'before' | 'take'
>;

/**
 * 出勤模組的端點：出勤紀錄（`attendance.records`）、卡號對應與不列入出勤（`attendance.cards`，寫入另需 `.maintain`）、
 * 打卡匯入紀錄（`attendance.imports`，重匯另需 `.reimport`）與假日設定（`attendance.calendar`，回寫另需 `.maintain`）。
 */
@Injectable({ providedIn: 'root' })
export class AttendanceApi {
  private readonly api = inject(ApiClient);
  /** 依人分頁：`offset` 起的 `limit` 人，回應帶總人數。 */
  records = (query: AttendanceRecordFilters, offset: number, limit: number, signal?: AbortSignal) =>
    this.api.get('/api/v1/attendance-records', { query: { ...query, offset, limit }, signal });
  /** 人員選單：範圍看得到、持有過員工卡的人（含只有姓名、還沒綁帳號的持有人），不限期間。 */
  people = (signal?: AbortSignal) => this.api.get('/api/v1/attendance-records/people', { signal });
  /** 與 `records` 同一組條件匯出 .xlsx；回應是檔案，檔名在 `Content-Disposition`。 */
  exportRecords = (query: AttendanceRecordFilters) =>
    this.api.open('/api/v1/attendance-records/export', { query });
  /** 判定規則的說明，時間是伺服器目前的設定值。 */
  rules = (signal?: AbortSignal) => this.api.get('/api/v1/attendance-rules', { signal });

  cards = (query: ApiQuery<'/api/v1/attendance-cards', 'get'>, signal?: AbortSignal) =>
    this.api.get('/api/v1/attendance-cards', { query, signal });
  createCard = (body: CardRequest) => this.api.post('/api/v1/attendance-cards', { body });
  updateCard = (id: string, body: CardRequest) =>
    this.api.put('/api/v1/attendance-cards/{id}', { path: { id }, body });
  deleteCard = (id: string) => this.api.delete('/api/v1/attendance-cards/{id}', { path: { id } });
  /** 某張卡的持有歷史，新到舊。 */
  assignments = (cardId: string, signal?: AbortSignal) =>
    this.api.get('/api/v1/attendance-card-assignments', { query: { cardId }, signal });
  /** 某位使用者持有過的卡，新到舊；只有姓名、還沒綁上帳號的期間不在其中。 */
  userAssignments = (userId: string, signal?: AbortSignal) =>
    this.api.get('/api/v1/attendance-card-assignments', { query: { userId }, signal });
  /** 已有人持有時回 409 `attendance_card_held`；使用者確認後以 `takeOver: true` 重送。 */
  createAssignment = (body: CardAssignmentCreateRequest) =>
    this.api.post('/api/v1/attendance-card-assignments', { body });
  updateAssignment = (id: string, body: CardAssignmentUpdateRequest) =>
    this.api.put('/api/v1/attendance-card-assignments/{id}', { path: { id }, body });
  deleteAssignment = (id: string) =>
    this.api.delete('/api/v1/attendance-card-assignments/{id}', { path: { id } });
  unattributed = (signal?: AbortSignal) =>
    this.api.get('/api/v1/attendance-unattributed-cards', { signal });
  /** 不列入出勤的人：HR 看全公司出勤時隱藏，不是權限。 */
  exclusions = (signal?: AbortSignal) =>
    this.api.get('/api/v1/attendance-roster-exclusions', { signal });
  /** 標為不列入，已標過時只更新備註。 */
  exclude = (userId: string, body: RosterExclusionRequest) =>
    this.api.put('/api/v1/attendance-roster-exclusions/{userId}', { path: { userId }, body });
  restore = (userId: string) =>
    this.api.delete('/api/v1/attendance-roster-exclusions/{userId}', { path: { userId } });

  imports = (filters: PunchImportFilters, take: number, before?: string, signal?: AbortSignal) =>
    this.api.get('/api/v1/attendance-imports', { query: { ...filters, take, before }, signal });
  /** 重匯檔名日期（`yyyyMMdd.txt` 的日期，不是出勤日）`from`～`to`，含頭尾；只重匯一個檔時起訖相同。 */
  reimport = (from: string, to: string) =>
    this.api.post('/api/v1/attendance-imports', { body: { from, to } });

  calendarDays = (from: string, to: string, signal?: AbortSignal) =>
    this.api.get('/api/v1/attendance-calendar-days', { query: { from, to }, signal });
  updateCalendarDay = (date: string, body: CalendarDayRequest) =>
    this.api.put('/api/v1/attendance-calendar-days/{date}', { path: { date }, body });
}
