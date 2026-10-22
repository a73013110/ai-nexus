import { Injectable, inject } from '@angular/core';
import { ApiClient, type ApiQuery } from '@core/api/api-client';
import type {
  CalendarDayRequest,
  CardAssignmentCreateRequest,
  CardAssignmentUpdateRequest,
  CardRequest,
} from '@core/api/schema';

/** 出勤紀錄的查詢條件（`from`、`to` 必填，最長 31 天）。 */
export type AttendanceRecordFilters = ApiQuery<'/api/v1/attendance-records', 'get'>;
/** 匯入紀錄已套用的條件；分頁參數（`before`、`take`）每頁另外加上。 */
export type PunchImportFilters = Omit<
  ApiQuery<'/api/v1/attendance-imports', 'get'>,
  'before' | 'take'
>;

/**
 * 出勤模組的端點：出勤紀錄（`attendance.records`）、卡號對應（`attendance.cards`，寫入另需 `.maintain`）、
 * 打卡匯入紀錄（`attendance.imports`，重匯另需 `.reimport`）與假日設定（`attendance.calendar`，回寫另需 `.maintain`）。
 */
@Injectable({ providedIn: 'root' })
export class AttendanceApi {
  private readonly api = inject(ApiClient);
  records = (query: AttendanceRecordFilters, signal?: AbortSignal) =>
    this.api.get('/api/v1/attendance-records', { query, signal });

  cards = (query: ApiQuery<'/api/v1/attendance-cards', 'get'>, signal?: AbortSignal) =>
    this.api.get('/api/v1/attendance-cards', { query, signal });
  createCard = (body: CardRequest) => this.api.post('/api/v1/attendance-cards', { body });
  updateCard = (id: string, body: CardRequest) =>
    this.api.put('/api/v1/attendance-cards/{id}', { path: { id }, body });
  deleteCard = (id: string) => this.api.delete('/api/v1/attendance-cards/{id}', { path: { id } });
  /** 某張卡的持有歷史，新到舊。 */
  assignments = (cardId: string, signal?: AbortSignal) =>
    this.api.get('/api/v1/attendance-card-assignments', { query: { cardId }, signal });
  /** 已有人持有時回 409 `attendance_card_held`；使用者確認後以 `takeOver: true` 重送。 */
  createAssignment = (body: CardAssignmentCreateRequest) =>
    this.api.post('/api/v1/attendance-card-assignments', { body });
  updateAssignment = (id: string, body: CardAssignmentUpdateRequest) =>
    this.api.put('/api/v1/attendance-card-assignments/{id}', { path: { id }, body });
  deleteAssignment = (id: string) =>
    this.api.delete('/api/v1/attendance-card-assignments/{id}', { path: { id } });
  unattributed = (signal?: AbortSignal) =>
    this.api.get('/api/v1/attendance-unattributed-cards', { signal });

  imports = (filters: PunchImportFilters, take: number, before?: string, signal?: AbortSignal) =>
    this.api.get('/api/v1/attendance-imports', { query: { ...filters, take, before }, signal });
  /** `date` 是檔名日期（`yyyyMMdd.txt`），不是出勤日。 */
  reimport = (date: string) => this.api.post('/api/v1/attendance-imports', { body: { date } });

  calendarDays = (from: string, to: string, signal?: AbortSignal) =>
    this.api.get('/api/v1/attendance-calendar-days', { query: { from, to }, signal });
  updateCalendarDay = (date: string, body: CalendarDayRequest) =>
    this.api.put('/api/v1/attendance-calendar-days/{date}', { path: { date }, body });
}
