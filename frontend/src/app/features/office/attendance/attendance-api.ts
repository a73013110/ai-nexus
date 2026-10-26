import { Injectable, inject } from '@angular/core';
import { ApiClient, type ApiQuery } from '@core/api/api-client';
import type {
  CalendarDayRequest,
  CardAssignmentCreateRequest,
  CardAssignmentUpdateRequest,
  CardCreateRequest,
  CardUpdateRequest,
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
 * 出勤模組的端點：出勤紀錄（`attendance.records`）、卡號對應（`attendance.cards`，寫入另需 `.maintain`）、
 * 打卡匯入紀錄（`attendance.imports`，重匯另需 `.reimport`）與假日設定（`attendance.calendar`，回寫另需 `.maintain`）。
 */
@Injectable({ providedIn: 'root' })
export class AttendanceApi {
  private readonly api = inject(ApiClient);
  /** 依人分頁：`offset` 起的 `limit` 人，回應帶總人數。 */
  records = (query: AttendanceRecordFilters, offset: number, limit: number, signal?: AbortSignal) =>
    this.api.get('/api/v1/attendance-records', { query: { ...query, offset, limit }, signal });
  /** 人員選單：範圍看得到、持有過卡的人（含只有姓名、還沒綁帳號的持有人），不限期間；指定單位時只列該單位及下層的人。 */
  people = (unitId: string, signal?: AbortSignal) =>
    this.api.get('/api/v1/attendance-records/people', {
      query: { unitId: unitId || undefined },
      signal,
    });
  /** 與 `records` 同一組條件匯出 .xlsx；回應是檔案，檔名在 `Content-Disposition`。 */
  exportRecords = (query: AttendanceRecordFilters) =>
    this.api.open('/api/v1/attendance-records/export', { query });
  /** 判定規則的說明，時間是伺服器目前的設定值。 */
  rules = (signal?: AbortSignal) => this.api.get('/api/v1/attendance-rules', { signal });

  /** 卡號清單：打卡檔出現過的卡號匯入時自動建立，與 HR 新增的卡在同一份清單；`status` 篩待指派、持有中或隱藏。 */
  cards = (query: ApiQuery<'/api/v1/attendance-cards', 'get'>, signal?: AbortSignal) =>
    this.api.get('/api/v1/attendance-cards', { query, signal });
  createCard = (body: CardCreateRequest) => this.api.post('/api/v1/attendance-cards', { body });
  /** 卡號建立後不改；只改是否在出勤紀錄隱藏與備註。 */
  updateCard = (id: string, body: CardUpdateRequest) =>
    this.api.put('/api/v1/attendance-cards/{id}', { path: { id }, body });
  deleteCard = (id: string) => this.api.delete('/api/v1/attendance-cards/{id}', { path: { id } });
  /** 一張卡的刷卡，新到舊、每頁 50 筆；`before` 是上一頁最後一筆的 ID。 */
  cardPunches = (id: string, before?: number, signal?: AbortSignal) =>
    this.api.get('/api/v1/attendance-cards/{id}/punches', {
      path: { id },
      query: { before },
      signal,
    });
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
