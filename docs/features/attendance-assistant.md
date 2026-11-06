# 出勤助理

結論：在出勤紀錄頁打開頂端的助理，就能用自然語言問目前查詢條件下的整段結果（整段、全部人，與匯出相同），例如誰還沒補足、單停在誰那裡。也能問畫面以外的事，例如別的區間、某人某天為什麼是早退、彈性上班怎麼算、離職的人為什麼還有紀錄、某人為什麼不見，由模型呼叫出勤的工具去查。機制見 [助理](../architecture/assistant.md)；查詢與資料範圍見 [出勤紀錄](attendance-records.md)。

## 工具

工具在 [`Office/Attendance/Assistant/`](../../backend/src/AiNexus.Features/Office/Attendance/Assistant/)，參數與說明寫在各工具裡。

| 工具 | 用途 | 稽核 |
|---|---|---|
| `attendance_records` | 出勤名單。在出勤紀錄頁提問時先以畫面條件執行（畫面資料）；模型也能換區間、人員或補足篩選再查 | `attendance.assistant.read` |
| `attendance_day_explanation` | 某人某天的[判定過程](attendance-record-explanation.md)與用到的規則；人要能唯一辨識 | `attendance.assistant.read` |
| `attendance_person` | 某人的[名單設定](#名單設定)：帳號狀態、每段持卡期間、卡是否隱藏、已知問題與名單〈規則〉 | `attendance.assistant.read` |
| `attendance_rules` | 判定規則條文（與頁首「判定規則」同一份） | 不記（只讀設定） |

- 四個工具都屬於 `attendance.records`：有這個功能就能用，不另設動作。名單與判定過程的資料範圍跟著這個功能（以 `ListAttendanceRecords` 取資料），範圍外的人不會進到模型。
- 稽核的 `FeatureId` 是 `attendance.records`，明細是工具名與參數，不記提問與回答。

## 名單的事實

[AttendanceAssistant](../../backend/src/AiNexus.Features/Office/Attendance/Assistant/AttendanceAssistant.cs) 依重要性排序，放不下時從後面捨去：

1. 查詢範圍、今天、GD 是否可用。
2. 〈檢查〉：名單上帳號已停用、持卡卻沒結束的人（與帳號待辦的「仍持有出勤卡」同一個條件）。
3. 判定規則條文。
4. 每人彙總：各狀態天數，以及未補足／流程中／已補足的天數與共差時數。
5. 要追的日子：依序是未補足、流程中、其他異常、已補足。
   - 每天附刷卡與判定過程的結論。
   - 流程中的日子附表單停在誰那裡。

- 正常日只算進彙總。今天不算進彙總，也不逐日列出。
- 數字與結論都來自 `AttendanceEvaluator`／`AttendanceExplanation`，與表格同一份（`AttendanceAssistantTests`、`AskAssistantTests`）。

## 名單設定

「為什麼還在、為什麼不見」的原因有很多種組合：持有沒結束、卡設為隱藏、沒綁帳號、換卡空窗、調單位、從未持卡。所以 `attendance_person` 交出完整的設定與條文，由模型說明，不逐案寫規則。

- 找人：帳號、姓名或卡號完全相同；最多 5 人。資料範圍與出勤紀錄相同，但含隱藏的卡、不限期間。範圍內沒有任何持卡紀錄的使用者也會列出。
- 事實順序：〈檢查〉→ 每人的帳號狀態 → 每段持有期間（起訖、是否持有中、隱藏、備註）→ 名單〈規則〉。
- 〈檢查〉只有兩種，條件與 `CardAccountReview` 相同，處理頁是卡號對應（以卡號搜尋）：
  - 帳號已停用，持有期間沒結束。
  - 帳號啟用，但沒有持有中的卡，而且最後一張卡沒有隱藏。
- 名單〈規則〉寫在 `AttendanceRoster.Rules`，與名單的查詢條件放在一起。改條件時一起改。
