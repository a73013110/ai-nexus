# 出勤助理

結論：出勤紀錄頁的「問助理」用自然語言問目前查詢條件下的整段結果（整段、全部人，與匯出相同），例如誰還沒補足、單停在誰那裡、某天為什麼是早退。機制見 [頁內助理](../architecture/page-assistant.md)；查詢與資料範圍見 [出勤紀錄](attendance-records.md)。

## 交給模型的事實

- 端點：`POST /api/v1/attendance-records/assistant`，query 與 `GET /attendance-records` 相同（含補足篩選，不分頁）。handler 以 `ListAttendanceRecords` 取整份名單，所以範圍外的人不會進到模型。
- [AttendanceAssistant](../../backend/src/AiNexus.Features/Office/Attendance/Records/AttendanceAssistant.cs) 依重要性排序，放不下時從後面捨去：
  1. 查詢範圍、今天、GD 是否可用
  2. 判定規則條文（與頁首「判定規則」同一份）
  3. 每人彙總：各狀態天數、未補足／流程中／已補足的天數與共差時數
  4. 要追的日子：未補足、流程中、其他異常、已補足，各附刷卡、[判定過程](attendance-record-explanation.md)的結論，以及流程中表單停在誰那裡
- 正常日只算進彙總；今天不算進彙總，也不逐日列出。
- 數字與結論都來自 `AttendanceEvaluator`／`AttendanceExplanation`，與表格同一份（`AttendanceAssistantTests`、`AskAttendanceAssistantTests`）。

## 權限與稽核

- 有 `attendance.records` 就能用，不另設動作；模型清單與政策與對話相同。
- 稽核事件 `attendance.assistant.read`，明細是查詢的區間與人數（與查詢出勤紀錄相同），不記提問與回答。
