# 出勤紀錄的判定過程

結論：出勤紀錄頁點某天的狀態，會看到那一天**怎麼判出來**。不用 AI：數字與文字都由判定本身產生，畫面只排版。查詢與資料範圍見 [出勤紀錄](attendance-records.md)，判定規則見 [出勤打卡](attendance.md#出勤判定)。

## 判定過程

- `GET /api/v1/attendance-records/explanation?date=2026-11-03&userId=…`（或 `card=` 給只有姓名的持有人，兩者恰好一個，否則 400 `attendance_records_filter_invalid`）。
- handler 直接呼叫 `ListAttendanceRecords` 查那一天（含放假日），所以資料範圍、名單、今天與 GD 連不上的處理都與表格同一份；範圍外或那天不在名單回 404 `attendance_record_not_found`，不透露那個人存在與否。
- 說明由 [AttendanceExplanation](../../backend/src/AiNexus.Features/Office/Attendance/Records/AttendanceExplanation.cs) 依 `AttendanceEvaluator` 的結果逐步寫出：刷卡、彈性時段、應下班、上班時數（扣午休）、差多少、哪些表單抵掉，最後一句結論。只敘述判定已算出的值，不另外推算，所以說明與表格不可能不一致（`ExplainAttendanceDayTests` 比對兩者）。
- 每一步標種類（刷卡、缺口、規則、表單、補充），畫面依種類畫實心、空心或短線圓點；結論附上引用的規則標題，畫面從 `GET /attendance-rules` 對到條文，改判定時條文與說明一起改。
- 今天只寫「今日未結算」，不提最新的刷卡（中午匯入的那筆不是下班）。
- 稽核：與查詢同一個事件 `attendance.record.read`，看一天就是讀一天的個人出勤。
